using System;
using System.Collections.Generic;
using System.Linq;
using FrooxEngine;
using ResoniteMario64.Components.Objects;
using ResoniteMario64.libsm64;
using static ResoniteMario64.Constants;
#if IsNet9
using Renderite.Shared;
#endif

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context : IDisposable
{
    public static SM64Context Instance { get; private set; }

    public DynamicVariableSpace ContextVariableSpace { get; private set; }

    private Slot _contextSlot;

    private Slot ContextSlot
    {
        get
        {
            Slot tempSlot = SM64Context.GetTempSlot(World);
            if (_contextSlot != null && (!_contextSlot.IsDestroyed || tempSlot is not { IsDestroyed: false })) return _contextSlot;
            
            Logger.Msg("ContextSlot is null/destroyed, attempting to refresh from tempSlot");
            _contextSlot = tempSlot.FindChild(x => x.Tag == ContextTag);
            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleRemoved;
                _contextSlot.OnPrepareDestroy += HandleRemoved;
                Logger.Msg($"ContextSlot found and event subscribed: {_contextSlot.Name} ({_contextSlot.ReferenceID})");
            }
            else
            {
                Logger.Msg("ContextSlot not found in tempSlot");
            }

            return _contextSlot;
        }

        set
        {
            if (value != null && value.Tag != ContextTag)
            {
                Logger.Error($"Tried to set SM64 Context but tag was {value.Tag} instead of {ContextTag}.");
                return;
            }

            if (value == null)
            {
                if (_contextSlot != null)
                {
                    _contextSlot.OnPrepareDestroy -= HandleRemoved;
                    Logger.Msg("Unsubscribed from old ContextSlot OnPrepareDestroy event");
                }
            }

            _contextSlot = value;

            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleRemoved;
                _contextSlot.OnPrepareDestroy += HandleRemoved;
                Logger.Msg($"ContextSlot set and event subscribed: {_contextSlot.Name} ({_contextSlot.ReferenceID})");
            }
            else
            {
                Logger.Msg("ContextSlot set to null");
            }
        }
    }
    
    public Slot MarioContainersSlot { get; private set; }
    public Slot MyMariosSlot { get; private set; }
    
    public readonly Dictionary<Slot, SM64Mario> AllMarios = new Dictionary<Slot, SM64Mario>();
    
    public List<SM64Mario> MyMarios => AllMarios.Values.Where(x => x.IsLocal).GetTempList();

    private bool AnyControlledMarios => AllMarios.Values.Any(x => x.IsLocal);
    
    public World World { get; }
    public DynamicVariableSpace WorldVariableSpace { get; private set; }

    internal double LastTick;

    private bool _staticColliderUpdate;

    private bool _disposed;
    private int _maxMariosAnimatedPerPerson;

    private SM64Context(World world)
    {
        const string caller = nameof(SM64Context);
        Logger.Msg($"Context ctor started for world: {world.Name}", caller);

        World = world;
        world.WorldDestroyed += HandleRemoved;

        InitContextWorld(world);

        if (!Interop.IsGlobalInit)
        {
            Logger.Msg("Init SM64", caller);
            Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);
        }
        else
        {
            Logger.Msg("SM64 already globally initialized", caller);
        }

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(world));
        ResoniteMario64.KeyMaxMeshColliderTris.OnChanged += HandleMaxMeshColliderTrisChanged;

        _maxMariosAnimatedPerPerson = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMaxMariosPerPerson);
        ResoniteMario64.KeyMaxMariosPerPerson.OnChanged += HandleMaxMariosPerPersonChanged;

        ResoniteMario64.KeyUseGamepad.OnChanged += HandleKeyUseGamepadChanged;

        world.RunInUpdates(3, () =>
        {
            Logger.Msg("Running initial collider handling on World.RootSlot children", caller);
            World.RootSlot.ForeachComponentInChildren<Collider>(c => HandleCollider(c));
        });
    }

    private void InitContextWorld(World world)
    {
        try
        {
            if (ContextSlot == null)
            {
                Logger.Msg("Setting up ContextSlot");
                Slot contextSlot = SM64Context.GetTempSlot(world).AddSlot(ContextSlotName, false);
                contextSlot.OrderOffset = -1000;
                contextSlot.Tag = ContextTag;

                ContextSlot = contextSlot;
            }

            ContextSlot.GetComponentOrAttach<ObjectRoot>();

            Logger.Msg($"ContextSlot = {ContextSlot.Name} ({ContextSlot.ReferenceID})");

            // Variable Space
            Logger.Msg("Setting up DynVarSpace");
            ContextVariableSpace = ContextSlot.GetComponentOrAttach<DynamicVariableSpace>(out bool spaceAttached);
            if (spaceAttached)
            {
                ContextVariableSpace.SpaceName.Value = ContextSpaceName;
                Logger.Msg($"DynamicVariableSpace attached and named {ContextSpaceName}");
            }
            
            WorldVariableSpace = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            if (WorldVariableSpace == null)
            {
                WorldVariableSpace = World.RootSlot.AttachComponent<DynamicVariableSpace>();
                WorldVariableSpace.SpaceName.Value = "World";
            }
            
            // Context Host
            Logger.Msg("Setting up Host DynVar");
            DynamicReferenceVariable<User> contextHost = ContextSlot.GetComponentOrAttach<DynamicReferenceVariable<User>>(out bool hostAttached);
            if (hostAttached)
            {
                contextHost.VariableName.Value = HostVarName;
                contextHost.Reference.Target = world.LocalUser;
                Logger.Msg("Host reference variable attached and set to local user");
            }

            contextHost.Reference.OnTargetChange += reference =>
            {
                if (reference.Target != null) return;

                Logger.Error("[HostContext TargetChange] SM64Context host reference was set to null, resetting to local user");
                ContextSlot.RunInUpdates(ContextSlot.LocalUser.AllocationID * 3, () =>
                {
                    contextHost.Reference.Target ??= world.LocalUser;
                    Logger.Msg("Host reference reset to local user");
                });
            };

            Slot configSlot = ContextSlot.FindChildOrAdd(ConfigSlotName, false);
            configSlot.Destroyed -= HandleRemoved;
            configSlot.Destroyed += HandleRemoved;

            // Scale
            Logger.Msg("Setting up World Scale DynVar");
            DynamicValueVariable<float> scale = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool scaleAttached, x => x.VariableName.Value == ScaleVarName);
            if (scaleAttached || contextHost.Reference.Target == null)
            {
                scale.VariableName.Value = ScaleVarName;
                scale.Value.Value = WorldVariableSpace?.TryReadValue(ScaleVarName, out float scaleValue) ?? false
                        ? scaleValue
                        : ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMarioScaleFactor);

                Logger.Msg($"Scale variable attached/set with value {scale.Value.Value}");
            }

            scale.Value.OnValueChange += val =>
            {
                Logger.Msg("Scale value changed, disposing and reinitializing SM64Context");
                Dispose();
                SM64Context.EnsureInstanceExists(val.World, out _);
            };

            // Water Level
            Logger.Msg("Setting up WaterLevel DynVar");
            DynamicValueVariable<float> waterLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool waterAttached, x => x.VariableName.Value == WaterVarName);
            if (waterAttached)
            {
                waterLevel.VariableName.Value = WaterVarName;
                waterLevel.Value.Value = WorldVariableSpace?.TryReadValue(WaterVarName, out float waterLevelValue) ?? false ? waterLevelValue : -100f;

                Logger.Msg($"WaterLevel variable attached/set with value {waterLevel.Value.Value}");
            }

            waterLevel.Value.OnValueChange += val =>
            {
                Logger.Msg($"WaterLevel changed to {val.Value}, updating all Marios");
                foreach (SM64Mario mario in AllMarios.Values.GetTempList())
                {
                    Interop.SetWaterLevel(mario.MarioId, val.Value);
                }
            };

            // Gas Level
            Logger.Msg("Setting up GasLevel DynVar");
            DynamicValueVariable<float> gasLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool gasAttached, x => x.VariableName.Value == GasVarName);
            if (gasAttached)
            {
                gasLevel.VariableName.Value = GasVarName;
                gasLevel.Value.Value = WorldVariableSpace?.TryReadValue(GasVarName, out float gasLevelValue) ?? false ? gasLevelValue : -100f;

                Logger.Msg($"GasLevel variable attached/set with value {gasLevel.Value.Value}");
            }

            gasLevel.Value.OnValueChange += val =>
            {
                Logger.Msg($"GasLevel changed to {val.Value}, updating all Marios");
                foreach (SM64Mario mario in AllMarios.Values.GetTempList())
                {
                    Interop.SetGasLevel(mario.MarioId, val.Value);
                }
            };

            // Setup Container Slots
            Logger.Msg("Creating All Mario Container Slot");
            if (MarioContainersSlot != null)
            {
                MarioContainersSlot.Destroyed -= HandleRemoved;
                MarioContainersSlot.ChildAdded -= HandleContainerAdded;
            }

            MarioContainersSlot = ContextSlot.FindChildOrAdd(MarioContainersSlotName, false);
            MarioContainersSlot.OrderOffset = 1000;
            MarioContainersSlot.Tag = MarioContainersTag;
            MarioContainersSlot.Destroyed += HandleRemoved;

            Logger.Msg("Creating My Mario Container Slot");
            MyMariosSlot = MarioContainersSlot.FindChild(x => x.Name == $"{world.LocalUser.UserName}'s Marios") ?? SM64Context.GetTempSlot(world).AddSlot($"{world.LocalUser.UserName}'s Marios", false);
            MyMariosSlot.OrderOffset = MyMariosSlot.LocalUser.AllocationID * 3;
            MyMariosSlot.Tag = MarioContainerTag;
            MyMariosSlot.DestroyWhenUserLeaves(world.LocalUser);

            world.RunInUpdates(1, () => MyMariosSlot.SetParent(MarioContainersSlot));

            Logger.Msg($"Subscribing to ChildAdded for {MarioContainersSlot.Name}");
            MarioContainersSlot.ChildAdded += HandleContainerAdded;

            MarioContainersSlot.ForeachChild(child =>
            {
                if (child.Tag != MarioContainerTag || child == MyMariosSlot) return;
                
                Logger.Msg($"Found other MarioContainer - {child.Name}");
                child.ChildAdded -= HandleMarioAdded;
                child.ChildAdded += HandleMarioAdded;
            });
        }
        catch (Exception e)
        {
            Logger.Error($"Exception during InitContextWorld: {e}");
            Dispose();
        }
    }

    private void HandleMarioAdded(Slot slot, Slot child)
    {
        if (child.Tag != MarioTag || AllMarios.ContainsKey(child)) return;

        child.RunSynchronously(() => SM64Context.TryAddMario(child, false));
    }

    private void HandleContainerAdded(Slot slot, Slot child)
    {
        if (child.Tag != MarioContainerTag) return;
        
        Logger.Msg($"New Container ({child.Name}) Added - {slot.Name}. Subscribing...");
        child.ChildAdded -= HandleMarioAdded;
        child.ChildAdded += HandleMarioAdded;

        child.ForeachChild(child2 =>
        {
            if (child2.Tag != MarioTag || AllMarios.ContainsKey(child2)) return;

            Logger.Msg($"New Mario ({child2.Name}) Found in new Container - {slot.Name}");
            child2.RunSynchronously(() => SM64Context.TryAddMario(child2, false));
        });
    }

    public void OnCommonUpdate()
    {
        if (_disposed) return;

        if (World.InputInterface.GetKeyDown(Key.Semicolon))
        {
            ReloadAllColliders();
        }

        if (World.InputInterface.GetKeyDown(Key.Backslash))
        {
            GetAllColliders(true, out _);
        }

        if (_staticColliderUpdate)
        {
            _staticColliderUpdate = false;
            Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
        }

        HandleInputs();

        if (World.Time.WorldTime - LastTick >= ResoniteMario64.Config.GetValue(ResoniteMario64.KeyGameTickMs) / 1000f)
        {
            SM64GameTick();
            LastTick = World.Time.WorldTime;
        }

        ProcessAudio();

        foreach (SM64Mario mario in AllMarios.Values.GetTempList())
        {
            mario.ContextUpdateSynced();
        }
    }

    private void SM64GameTick()
    {
        if (_disposed) return;

        foreach (SM64DynamicCollider dynamicCol in DynamicColliders.Values.GetTempList())
        {
            dynamicCol?.ContextFixedUpdateSynced();
        }

        foreach (SM64Mario mario in AllMarios.Values.GetTempList())
        {
            mario?.ContextFixedUpdateSynced();
        }
    }
    
    private void HandleRemoved(Slot slot)
    {
        if (_disposed) return;

        Dispose();
    }

    private void HandleRemoved(IDestroyable destroyable)
    {
        if (_disposed) return;

        Dispose();
    }

    private void HandleRemoved(World world)
    {
        if (_disposed) return;

        Dispose();
    }

    private void HandleKeyUseGamepadChanged(object newValue)
    {
        if (_disposed) return;

        World?.Input.InvalidateBindings();
    }

    private void HandleMaxMeshColliderTrisChanged(object newValue)
    {
        if (_disposed) return;

        ReloadAllColliders();
    }

    private void HandleMaxMariosPerPersonChanged(object newValue)
    {
        _maxMariosAnimatedPerPerson = (int)(newValue ?? 0);
        UpdatePlayerMariosState();
    }
    
    public void UpdatePlayerMariosState()
    {
        int maxPerPerson = _maxMariosAnimatedPerPerson;

        foreach (User user in Instance.World.AllUsers.Where(x => !x.IsLocalUser))
        {
            List<SM64Mario> userMarios = AllMarios.GetFilteredSortedList(m => m.MarioUser == user && !m.IsLocal, m2 => m2.MarioId, false);
            for (int i = 0; i < userMarios.Count; i++)
            {
                SM64Mario mario = userMarios[i];
                bool isOverLimit = i >= maxPerPerson;
                mario.SetIsOverMaxCount(isOverLimit);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        // GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Logger.Msg("Disposing managed resources for SM64Context");

            // Unsubscribe from all events to prevent memory leaks
            World.WorldDestroyed -= HandleRemoved;
            ResoniteMario64.KeyUseGamepad.OnChanged -= HandleKeyUseGamepadChanged;
            ResoniteMario64.KeyMaxMeshColliderTris.OnChanged -= HandleMaxMeshColliderTrisChanged;
            ResoniteMario64.KeyMaxMariosPerPerson.OnChanged -= HandleMaxMariosPerPersonChanged;
            ResoniteMario64.KeyLocalAudio.OnChanged -= HandleLocalAudioChange;
            ResoniteMario64.KeyDisableAudio.OnChanged -= HandleDisableChange;
            ResoniteMario64.KeyAudioVolume.OnChanged -= HandleVolumeChange;

            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleRemoved;
            }

            // Dispose of all Lists
            foreach (var mario in AllMarios.Values.GetTempList())
            {
                mario?.Dispose();
            }
            
            foreach (var col in DynamicColliders.Values.GetTempList())
            {
                col?.Dispose();
            }
            
            foreach (var interactable in Interactables.Values.GetTempList())
            {
                interactable?.Dispose();
            }

            // Clear lists
            AllMarios.Clear();
            DynamicColliders.Clear();
            Interactables.Clear();
            StaticColliders.Clear();
            WaterBoxes.Clear();

            // Stop and dispose the timer for static collider updates
            _staticUpdateTimer?.Stop();
            _staticUpdateTimer?.Dispose();
            _staticUpdateTimer = null;

            World.RunSynchronously(() =>
            {
                // Release the locomotion input block
                LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
                if (loco != null && _inputBlock != null)
                {
                    loco.SupressSources?.Remove(_inputBlock);
                }

                // Clean up audio resources
                if (_audioSlot != null)
                {
                    _audioSlot.OnPrepareDestroy -= HandleAudioDestroy;
                    if (_audioSlot.IsLocalElement && !_audioSlot.IsDestroyed)
                    {
                        _audioSlot.Destroy();
                    }
                }
            }, true, null, true);

            // Nullify references to Resonite objects
            _contextSlot = null;
            MyMariosSlot = null;
            MarioContainersSlot = null;
            _marioAudioStream = null;
            _marioAudioOutput = null;
            _audioSlot = null;
            _inputBlock = null;
        }

        // Free unmanaged resources (from the C++ library)
        Logger.Msg("Terminating libsm64 global instance.");
        Interop.GlobalTerminate();

        // Finally, nullify the static instance
        if (Instance == this)
        {
            Instance = null;
        }

        _disposed = true;
        Logger.Msg("SM64Context disposed.");
    }
}