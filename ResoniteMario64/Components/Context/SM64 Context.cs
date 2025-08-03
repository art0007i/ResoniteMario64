using System;
using System.Collections.Generic;
using System.Linq;
using FrooxEngine;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
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
            const string method = $"{nameof(ContextSlot)}_get";
            Slot tempSlot = GetTempSlot(World);
            if (_contextSlot == null || _contextSlot.IsDestroyed && tempSlot is { IsDestroyed: false })
            {
                ResoniteMod.Msg($"[{method}] ContextSlot is null/destroyed, attempting to refresh from tempSlot");
                _contextSlot = tempSlot.FindChild(x => x.Tag == ContextTag);
                if (_contextSlot != null)
                {
                    _contextSlot.OnPrepareDestroy -= HandleRemoved;
                    _contextSlot.OnPrepareDestroy += HandleRemoved;
                    ResoniteMod.Msg($"[{method}] ContextSlot found and event subscribed: {_contextSlot.Name} ({_contextSlot.ReferenceID})");
                }
                else
                {
                    ResoniteMod.Msg($"[{method}] ContextSlot not found in tempSlot");
                }
            }

            return _contextSlot;
        }

        set
        {
            const string method = $"{nameof(ContextSlot)}_set";
            if (value != null && value.Tag != ContextTag)
            {
                ResoniteMod.Error($"[{method}] Tried to set SM64 Context but tag was {value.Tag} instead of {ContextTag}.");
                return;
            }

            if (value == null)
            {
                if (_contextSlot != null)
                {
                    _contextSlot.OnPrepareDestroy -= HandleRemoved;
                    ResoniteMod.Msg($"[{method}] Unsubscribed from old ContextSlot OnPrepareDestroy event");
                }
            }

            _contextSlot = value;

            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleRemoved;
                _contextSlot.OnPrepareDestroy += HandleRemoved;
                ResoniteMod.Msg($"[{method}] ContextSlot set and event subscribed: {_contextSlot.Name} ({_contextSlot.ReferenceID})");
            }
            else
            {
                ResoniteMod.Msg($"[{method}] ContextSlot set to null");
            }
        }
    }

    public static Slot TempSlot;

    public static Slot GetTempSlot(World world)
    {
        const string method = nameof(GetTempSlot);

        World currentWorld = world ?? Engine.Current.WorldManager.FocusedWorld;
        if (TempSlot == null || TempSlot.IsDestroyed || TempSlot.World != currentWorld)
        {
            if (currentWorld == null)
            {
                ResoniteMod.Msg($"[{method}] Current world is null, returning null");
                return null;
            }

            Slot newSlot = currentWorld.RootSlot?.FindChildOrAdd(TempSlotName, false);
            if (newSlot == null)
            {
                ResoniteMod.Msg($"[{method}] Could not find or add temp slot '{TempSlotName}'");
                return null;
            }

            if (TempSlot != null)
            {
                TempSlot.ChildAdded -= HandleSlotAdded;
                ResoniteMod.Msg($"[{method}] Removed ChildAdded event from previous tempSlot");
            }

            TempSlot = newSlot;
            TempSlot.ChildAdded += HandleSlotAdded;
            ResoniteMod.Msg($"[{method}] New tempSlot set and ChildAdded event subscribed: {TempSlot.Name} ({TempSlot.ReferenceID})");
        }

        return TempSlot;
    }

    public Slot MarioContainersSlot { get; private set; }
    public Slot MyMariosSlot { get; private set; }

    public readonly Dictionary<Slot, SM64Mario> AllMarios = new Dictionary<Slot, SM64Mario>();
    public List<SM64Mario> MyMarios => AllMarios.Values.Where(x => x.IsLocal).GetTempList();

    public bool AnyControlledMarios => AllMarios.Values.Any(x => x.IsLocal);

    public World World { get; }

    internal double LastTick;

    private bool _staticColliderUpdate;

    private bool _disposed;
    private int _maxMariosAnimatedPerPerson;

    private SM64Context(World world)
    {
        const string method = nameof(SM64Context);
        ResoniteMod.Msg($"[{method}] Constructor started for world: {world.Name}");

        World = world;
        world.WorldDestroyed += HandleRemoved;

        ResoniteMario64.KeyUseGamepad.OnChanged += HandleKeyUseGamepadChanged;

        InitContextWorld(world);

        if (!Interop.IsGlobalInit)
        {
            ResoniteMod.Msg($"[{method}] Init SM64");
            Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);
        }
        else
        {
            ResoniteMod.Msg($"[{method}] SM64 already globally initialized");
        }

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(world));
        ResoniteMario64.KeyMaxMeshColliderTris.OnChanged += HandleMaxMeshColliderTrisChanged;

        world.RunInUpdates(3, () =>
        {
            ResoniteMod.Msg($"[{method}] Running initial collider handling on World.RootSlot children");
            World.RootSlot.ForeachComponentInChildren<Collider>(c => HandleCollider(c));
        });

        _maxMariosAnimatedPerPerson = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMaxMariosPerPerson);
        ResoniteMario64.KeyMaxMariosPerPerson.OnChanged += HandleMaxMariosPerPersonChanged;
    }

    public void InitContextWorld(World world)
    {
        const string method = nameof(InitContextWorld);
        try
        {
            if (ContextSlot == null)
            {
                ResoniteMod.Msg($"[{method}] Setting up ContextSlot");
                Slot contextSlot = GetTempSlot(world).AddSlot(ContextSlotName, false);
                contextSlot.OrderOffset = -1000;
                contextSlot.Tag = ContextTag;

                ContextSlot = contextSlot;
            }

            ContextSlot.GetComponentOrAttach<ObjectRoot>();

            ResoniteMod.Msg($"[{method}] ContextSlot = {ContextSlot.Name} ({ContextSlot.ReferenceID})");

            // Variable Space
            ResoniteMod.Msg($"[{method}] Setting up DynVarSpace");
            ContextVariableSpace = ContextSlot.GetComponentOrAttach<DynamicVariableSpace>(out bool spaceAttached);
            if (spaceAttached)
            {
                ContextVariableSpace.SpaceName.Value = ContextSpaceName;
                ResoniteMod.Msg($"[{method}] DynamicVariableSpace attached and named {ContextSpaceName}");
            }

            // Context Host
            ResoniteMod.Msg($"[{method}] Setting up Host DynVar");
            DynamicReferenceVariable<User> contextHost = ContextSlot.GetComponentOrAttach<DynamicReferenceVariable<User>>(out bool hostAttached);
            if (hostAttached)
            {
                contextHost.VariableName.Value = HostVarName;
                contextHost.Reference.Target = world.LocalUser;
                ResoniteMod.Msg($"[{method}] Host reference variable attached and set to local user");
            }

            contextHost.Reference.OnTargetChange += reference =>
            {
                if (reference.Target != null) return;

                ResoniteMod.Error($"[{method}] [HostContext TargetChange] SM64Context host reference was set to null, resetting to local user");
                ContextSlot.RunInUpdates(ContextSlot.LocalUser.AllocationID * 3, () =>
                {
                    contextHost.Reference.Target ??= world.LocalUser;
                    ResoniteMod.Msg($"[{method}] Host reference reset to local user");
                });
            };

            Slot configSlot = ContextSlot.FindChildOrAdd(ConfigSlotName, false);
            configSlot.Destroyed -= HandleRemoved;
            configSlot.Destroyed += HandleRemoved;

            // Scale
            ResoniteMod.Msg($"[{method}] Setting up World Scale DynVar");
            DynamicValueVariable<float> scale = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool scaleAttached, x => x.VariableName.Value == ScaleVarName);
            if (scaleAttached || contextHost.Reference.Target == null)
            {
                scale.VariableName.Value = ScaleVarName;
                DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
                scale.Value.Value = worldVar?.TryReadValue(ScaleVarName, out float scaleValue) ?? false
                        ? scaleValue
                        : ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMarioScaleFactor);

                ResoniteMod.Msg($"[{method}] Scale variable attached/set with value {scale.Value.Value}");
            }

            scale.Value.OnValueChange += val =>
            {
                ResoniteMod.Msg($"[{method}] Scale value changed, disposing and reinitializing SM64Context");
                Dispose();
                SM64Context.EnsureInstanceExists(val.World, out _);
            };

            // Water Level
            ResoniteMod.Msg($"[{method}] Setting up WaterLevel DynVar");
            DynamicValueVariable<float> waterLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool waterAttached, x => x.VariableName.Value == WaterVarName);
            if (waterAttached)
            {
                waterLevel.VariableName.Value = WaterVarName;
                DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
                waterLevel.Value.Value = worldVar?.TryReadValue(WaterVarName, out float waterLevelValue) ?? false ? waterLevelValue : -100f;

                ResoniteMod.Msg($"[{method}] WaterLevel variable attached/set with value {waterLevel.Value.Value}");
            }

            waterLevel.Value.OnValueChange += val =>
            {
                ResoniteMod.Msg($"[{method}] WaterLevel changed to {val.Value}, updating all Marios");
                List<SM64Mario> marios = AllMarios.Values.GetTempList();
                foreach (SM64Mario mario in marios)
                {
                    Interop.SetWaterLevel(mario.MarioId, val.Value);
                }
            };

            // Gas Level
            ResoniteMod.Msg($"[{method}] Setting up GasLevel DynVar");
            DynamicValueVariable<float> gasLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool gasAttached, x => x.VariableName.Value == GasVarName);
            if (gasAttached)
            {
                gasLevel.VariableName.Value = GasVarName;
                DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
                gasLevel.Value.Value = worldVar?.TryReadValue(GasVarName, out float gasLevelValue) ?? false ? gasLevelValue : -100f;

                ResoniteMod.Msg($"[{method}] GasLevel variable attached/set with value {gasLevel.Value.Value}");
            }

            gasLevel.Value.OnValueChange += val =>
            {
                ResoniteMod.Msg($"[{method}] GasLevel changed to {val.Value}, updating all Marios");
                List<SM64Mario> marios = AllMarios.Values.GetTempList();
                foreach (SM64Mario mario in marios)
                {
                    Interop.SetGasLevel(mario.MarioId, val.Value);
                }
            };

            // Setup Container Slots
            ResoniteMod.Msg($"[{method}] Creating All Mario Container Slot");
            if (MarioContainersSlot != null)
            {
                MarioContainersSlot.Destroyed -= HandleRemoved;
                MarioContainersSlot.ChildAdded -= HandleContainerAdded;
            }

            MarioContainersSlot = ContextSlot.FindChildOrAdd(MarioContainersSlotName, false);
            MarioContainersSlot.OrderOffset = 1000;
            MarioContainersSlot.Tag = MarioContainersTag;
            MarioContainersSlot.Destroyed += HandleRemoved;

            ResoniteMod.Msg($"[{method}] Creating My Mario Container Slot");
            MyMariosSlot = MarioContainersSlot.FindChild(x => x.Name == $"{world.LocalUser.UserName}'s Marios") ?? GetTempSlot(world).AddSlot($"{world.LocalUser.UserName}'s Marios", false);
            ;
            MyMariosSlot.OrderOffset = MyMariosSlot.LocalUser.AllocationID * 3;
            MyMariosSlot.Tag = MarioContainerTag;
            MyMariosSlot.DestroyWhenUserLeaves(world.LocalUser);

            world.RunInUpdates(1, () => MyMariosSlot.SetParent(MarioContainersSlot));

            ResoniteMod.Msg($"[{method}] Subscribing to ChildAdded for {MarioContainersSlot.Name}");
            MarioContainersSlot.ChildAdded += HandleContainerAdded;

            MarioContainersSlot.ForeachChild(child =>
            {
                if (child.Tag == MarioContainerTag && child != MyMariosSlot)
                {
                    ResoniteMod.Msg($"[{method}] Found other MarioContainer - {child.Name}");
                    child.ChildAdded -= HandleMarioAdded;
                    child.ChildAdded += HandleMarioAdded;
                }
            });
        }
        catch (Exception e)
        {
            ResoniteMod.Error($"[{method}] Exception during InitContextWorld: {e}");
            throw;
        }
    }

    private void HandleMarioAdded(Slot slot, Slot child)
    {
        if (child.Tag != MarioTag || AllMarios.ContainsKey(child)) return;
        
        child.RunSynchronously(() => TryAddMario(child, false));
    }

    private void HandleContainerAdded(Slot slot, Slot child)
    {
        const string method = nameof(HandleContainerAdded);

        if (child.Tag == MarioContainerTag)
        {
            ResoniteMod.Msg($"[{method}] New Container ({child.Name}) Added - {slot.Name}. Subscribing...");
            child.ChildAdded -= HandleMarioAdded;
            child.ChildAdded += HandleMarioAdded;

            child.ForeachChild(child2 =>
            {
                if (child2.Tag == MarioTag && !AllMarios.ContainsKey(child2))
                {
                    ResoniteMod.Msg($"[{method}] New Mario ({child2.Name}) Found in new Container - {slot.Name}");
                    child2.RunSynchronously(() => TryAddMario(child2, false));
                }
            });
        }
    }

    public static void HandleSlotAdded(Slot slot, Slot child)
    {
        const string method = nameof(HandleSlotAdded);

        if (child.Tag == ContextTag)
        {
            child.RunSynchronously(() =>
            {
                ResoniteMod.Msg($"[{method}] A ContextSlot ({child.Name}) was Added - {slot.Name}");
                if (SM64Context.EnsureInstanceExists(child.World, out SM64Context context))
                {
                    context.MarioContainersSlot.ForeachChild(marioSlot =>
                    {
                        if (marioSlot.Tag == MarioTag)
                        {
                            ResoniteMod.Msg($"[{method}] Found Mario slot in new context: {marioSlot.Name} ({marioSlot.ReferenceID})");
                            SM64Context.TryAddMario(marioSlot, false);
                        }
                    });
                }
            });
        }
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

        List<SM64Mario> marios = AllMarios.Values.GetTempList();
        foreach (SM64Mario mario in marios)
        {
            mario.ContextUpdateSynced();
        }
    }

    private void SM64GameTick()
    {
        if (_disposed) return;

        List<SM64DynamicCollider> dynamicColliders = DynamicColliders.Values.GetTempList();
        foreach (SM64DynamicCollider dynamicCol in dynamicColliders)
        {
            dynamicCol?.ContextFixedUpdateSynced();
        }

        List<SM64Mario> marios = AllMarios.Values.GetTempList();
        foreach (SM64Mario mario in marios)
        {
            mario?.ContextFixedUpdateSynced();
        }
    }

    public static void UpdatePlayerMariosState()
    {
        if (Instance == null) return;

        int maxPerPerson = Instance._maxMariosAnimatedPerPerson;
        List<SM64Mario> marios = Instance.AllMarios.Values.GetTempList();

        Dictionary<User, int> remainingByUser = new Dictionary<User, int>();

        foreach (SM64Mario mario in marios)
        {
            if (mario.IsLocal) continue;

            User user = mario.MarioUser;
            if (user == null) continue;

            int remaining = remainingByUser.GetValueOrDefault(user, maxPerPerson);

            bool isOverLimit = remaining-- <= 0;
            mario.SetIsOverMaxCount(isOverLimit);

            remainingByUser[user] = remaining;
        }
    }

    public static bool TryAddMario(Slot slot, bool manual = true) => AddMario(slot, manual) != null;

    public static SM64Mario AddMario(Slot slot, bool manual = true)
    {
        const string method = nameof(AddMario);

        bool success = EnsureInstanceExists(slot.World, out SM64Context instance);
        if (!success)
        {
            ResoniteMod.Error($"[{method}] Failed to ensure SM64Context instance for world: {slot.World?.Name}");
            return null;
        }

        // if (instance.MyMarios.Count >= 5)
        // {
        //     ResoniteMod.Error($"[{method}] You have too many marios!");
        //     slot.RunSynchronously(slot.Destroy);
        //     return null;
        // }

        SM64Mario mario = null;
        if (!instance.AllMarios.ContainsKey(slot))
        {
            ResoniteMod.Msg($"[{method}] Adding Mario for Slot: {slot.Name} ({slot.ReferenceID})");

            mario = new SM64Mario(slot, instance);
            instance.AllMarios.Add(slot, mario);
            if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayRandomMusic))
            {
                ResoniteMod.Msg($"[{method}] Playing random music due to config");
                Interop.PlayRandomMusic();
            }

            ResoniteMod.Msg($"[{method}] Added mario for Slot: {slot.Name} ({slot.ReferenceID})");
        }

        if (!manual) return mario;
        
        Slot containerSlot = instance.MarioContainersSlot;
        if (containerSlot != null)
        {
            containerSlot.RunInUpdates(3, () =>
            {
                foreach (Slot child1 in containerSlot.Children.GetTempList())
                {
                    foreach (Slot child2 in child1.Children.GetTempList())
                    {
                        if (child2.Tag != MarioTag) continue;
                        if (instance.AllMarios.ContainsKey(child2)) continue;

                        ResoniteMod.Msg($"[{method}] Adding existing Mario for Slot: {child2.Name} ({child2.ReferenceID})");

                        SM64Mario mario2 = new SM64Mario(child2, instance);
                        instance.AllMarios.Add(child2, mario2);

                        ResoniteMod.Msg($"[{method}] Added existing Mario for Slot: {child2.Name} ({child2.ReferenceID})");
                    }
                }

                instance.ReloadAllColliders(false);
                ResoniteMod.Msg($"[{method}] Reloaded all colliders after adding existing Marios");
            });
        }
        else
        {
            ResoniteMod.Msg($"[{method}] MarioContainersSlot is null, skipping adding existing Marios");
        }

        return mario;
    }

    public static void RemoveMario(SM64Mario mario)
    {
        const string method = nameof(RemoveMario);
        ResoniteMod.Msg($"[{method}] Removing Mario with ID {mario.MarioId}");

        mario.Context.AllMarios.Remove(mario.MarioSlot);
        UpdatePlayerMariosState();

        if (mario.Context.AllMarios.Count == 0)
        {
            ResoniteMod.Msg($"[{method}] No more Marios left, stopping music");
            Interop.StopMusic();
        }
    }

    public static bool EnsureInstanceExists(World world, out SM64Context instance)
    {
        const string method = nameof(EnsureInstanceExists);
        // We don't have Instancing for LibSM64, so we can't have multiple instances for separate worlds.
        if (Instance != null && world != Instance.World)
        {
            bool destroy = world.Focus == World.WorldFocus.Focused;
            ResoniteMod.Error($"[{method}] Tried to create instance while one already exists. Replace? - {(destroy ? "True" : "False")}");
            if (destroy)
            {
                ResoniteMod.Msg($"[{method}] Disposing existing instance");
                Instance.Dispose();
                Instance = null;
            }
            else
            {
                instance = Instance;
                return false;
            }
        }

        if (Instance != null)
        {
            instance = Instance;
            return true;
        }

        ResoniteMod.Msg($"[{method}] Ensuring SM64Context instance exists for world: {world.Name}");
        Instance = new SM64Context(world);
        ResoniteMod.Msg($"[{method}] Instance Created!");

        instance = Instance;

        return true;
    }

    private void HandleRemoved(Slot slot)
    {
        if (!_disposed)
        {
            Dispose();
        }
    }

    private void HandleRemoved(IDestroyable destroyable)
    {
        if (!_disposed)
        {
            Dispose();
        }
    }

    private void HandleRemoved(World world)
    {
        if (!_disposed)
        {
            Dispose();
        }
    }

    private void HandleKeyUseGamepadChanged(object newValue)
    {
        if (!_disposed)
        {
            World?.Input.InvalidateBindings();
        }
    }

    private void HandleMaxMeshColliderTrisChanged(object newValue)
    {
        if (!_disposed)
        {
            ReloadAllColliders();
        }
    }

    private void HandleMaxMariosPerPersonChanged(object newValue)
    {
        _maxMariosAnimatedPerPerson = (int)(newValue ?? 0);
        UpdatePlayerMariosState();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        const string method = nameof(Dispose);
        if (_disposed) return;

        if (disposing)
        {
            ResoniteMod.Msg($"[{method}] Disposing managed resources for SM64Context");

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

            // Dispose of all Mario instances
            foreach (var mario in AllMarios.Values)
            {
                mario?.Dispose();
            }

            AllMarios.Clear();

            // Dispose of all dynamic colliders
            foreach (var col in DynamicColliders.Values)
            {
                col?.Dispose();
            }

            DynamicColliders.Clear();

            // Dispose of all interactables
            foreach (var interactable in Interactables.Values)
            {
                interactable?.Dispose();
            }

            Interactables.Clear();

            // Clear lists of other colliders
            StaticColliders.Clear();
            WaterBoxes.Clear();

            // Stop and dispose the timer for static collider updates
            _staticUpdateTimer?.Stop();
            _staticUpdateTimer?.Dispose();
            _staticUpdateTimer = null;

            // Release the locomotion input block
            if (World is { IsDestroyed: false, CanCurrentThreadModify: true })
            {
                LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
                if (loco != null && InputBlock != null)
                {
                    loco.SupressSources?.Remove(InputBlock);
                }
            }

            // Clean up audio resources
            if (AudioSlot != null)
            {
                AudioSlot.OnPrepareDestroy -= HandleAudioDestroy;
                if (AudioSlot.IsLocalElement && !AudioSlot.IsDestroyed)
                {
                    AudioSlot.Destroy();
                }
            }

            // Nullify references to Resonite objects
            _contextSlot = null;
            MyMariosSlot = null;
            MarioContainersSlot = null;
            _marioAudioStream = null;
            _marioAudioOutput = null;
            AudioSlot = null;
            InputBlock = null;
        }

        // Free unmanaged resources (from the C++ library)
        ResoniteMod.Msg($"[{method}] Terminating libsm64 global instance.");
        Interop.GlobalTerminate();

        // Finally, nullify the static instance
        if (Instance == this)
        {
            Instance = null;
        }

        _disposed = true;
        ResoniteMod.Msg($"[{method}] SM64Context disposed.");
    }
}