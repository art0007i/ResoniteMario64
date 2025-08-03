using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            Slot tempSlot = GetTempSlot(World);
            if (_contextSlot == null || _contextSlot.IsDestroyed && tempSlot is { IsDestroyed: false })
            {
                _contextSlot = tempSlot.FindChild(x => x.Tag == ContextTag);
                if (_contextSlot != null)
                {
                    _contextSlot.OnPrepareDestroy -= HandleInstanceRemoved;
                    _contextSlot.OnPrepareDestroy += HandleInstanceRemoved;
                }
            }

            return _contextSlot;
        }

        set
        {
            if (value != null && value.Tag != ContextTag)
            {
                ResoniteMod.Error($"[get_ContextSlot] Tried to set SM64 Context but tag was {value.Tag} instead of {ContextTag}.");
                return;
            }

            if (value == null)
            {
                _contextSlot.OnPrepareDestroy -= HandleInstanceRemoved;
            }

            _contextSlot = value;

            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleInstanceRemoved;
                _contextSlot.OnPrepareDestroy += HandleInstanceRemoved;
            }
        }
    }

    public Slot MarioContainersSlot { get; private set; }
    public Slot MyMariosSlot { get; private set; }

    public readonly Dictionary<Slot, SM64Mario> AllMarios = new Dictionary<Slot, SM64Mario>();
    public List<SM64Mario> MyMarios => AllMarios.Values.Where(x => x.IsLocal).GetTempList();

    public bool AnyControlledMarios => MyMarios.Count != 0;

    public World World { get; }

    internal double LastTick;

    private bool _staticColliderUpdate;

    private bool _disposed;
    // private int _maxMariosAnimatedPerPerson;

    private SM64Context(World world)
    {
        World = world;
        world.WorldDestroyed += _ =>
        {
            if (!_disposed)
            {
                Dispose();
            }
        };

        ResoniteMario64.KeyUseGamepad.OnChanged += _ =>
        {
            if (!_disposed)
            {
                World?.Input.InvalidateBindings();
            }
        };

        InitContextWorld(world);

        if (!Interop.IsGlobalInit)
        {
            Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);
        }

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(world));
        ResoniteMario64.KeyMaxMeshColliderTris.OnChanged += _ =>
        {
            if (!_disposed)
            {
                ReloadAllColliders();
            }
        };
        world.RunInUpdates(3, () => World.RootSlot.ForeachComponentInChildren<Collider>(c => HandleCollider(c)));

        // _maxMariosAnimatedPerPerson = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMaxMariosPerPerson);
        // ResoniteMario64.KeyMaxMariosPerPerson.OnChanged += newValue =>
        // {
        //     _maxMariosAnimatedPerPerson = (int)(newValue ?? 0);
        //     UpdatePlayerMariosState();
        // };
    }

    public void InitContextWorld(World world)
    {
        if (ContextSlot == null)
        {
            ResoniteMod.Msg("[InitContextWorld] Setting up ContextSlot");
            Slot contextSlot = GetTempSlot(world).AddSlot(ContextSlotName, false);
            contextSlot.OrderOffset = -1000;
            contextSlot.Tag = ContextTag;

            ContextSlot = contextSlot;
        }
        ResoniteMod.Msg($"[InitContextWorld] ContextSlot = {ContextSlot.Name} ({ContextSlot.ReferenceID})");

        // Variable Space
        ResoniteMod.Msg("[InitContextWorld] Setting up DynVarSpace");
        ContextVariableSpace = ContextSlot.GetComponentOrAttach<DynamicVariableSpace>(out bool spaceAttached);
        if (spaceAttached)
        {
            ContextVariableSpace.SpaceName.Value = ContextSpaceName;
        }

        // Context Host
        ResoniteMod.Msg("[InitContextWorld] Setting up Host DynVar");
        DynamicReferenceVariable<User> contextHost = ContextSlot.GetComponentOrAttach<DynamicReferenceVariable<User>>(out bool hostAttached);
        if (hostAttached)
        {
            contextHost.VariableName.Value = HostVarName;
            contextHost.Reference.Target = world.LocalUser;
        }

        contextHost.Reference.OnTargetChange += reference =>
        {
            if (reference.Target != null) return;

            ResoniteMod.Error("[HostContext TargetChange] SM64Context host reference was set to null, resetting to the next local user.");
            ContextSlot.RunInUpdates(ContextSlot.LocalUser.AllocationID * 3, () => { contextHost.Reference.Target ??= world.LocalUser; });
        };

        Slot configSlot = ContextSlot.FindChildOrAdd(ConfigSlotName, false);

        // Scale
        ResoniteMod.Msg("[InitContextWorld] Setting up World Scale DynVar");
        DynamicValueVariable<float> scale = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool scaleAttached, x => x.VariableName.Value == ScaleVarName);
        if (scaleAttached || contextHost.Reference.Target == null)
        {
            scale.VariableName.Value = ScaleVarName;
            DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            scale.Value.Value = worldVar?.TryReadValue(ScaleVarName, out float scaleValue) ?? false ? scaleValue : ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMarioScaleFactor);
        }

        scale.Value.OnValueChange += val =>
        {
            // We completely reset the Instance when the scale changes, so we can ensure that the scale is applied correctly for everyone.
            Dispose();
            SM64Context.EnsureInstanceExists(val.World, out _);
        };

        // Water Level
        ResoniteMod.Msg("[InitContextWorld] Setting up WaterLevel DynVar");
        DynamicValueVariable<float> waterLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool waterAttached, x => x.VariableName.Value == WaterVarName);
        if (waterAttached)
        {
            waterLevel.VariableName.Value = WaterVarName;
            DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            waterLevel.Value.Value = worldVar?.TryReadValue(WaterVarName, out float waterLevelValue) ?? false ? waterLevelValue : -100f;
        }

        waterLevel.Value.OnValueChange += val =>
        {
            List<SM64Mario> marios = AllMarios.Values.GetTempList();
            foreach (SM64Mario mario in marios)
            {
                Interop.SetWaterLevel(mario.MarioId, val.Value);
            }
        };

        // Gas Level
        ResoniteMod.Msg("[InitContextWorld] Setting up GasLevel DynVar");
        DynamicValueVariable<float> gasLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool gasAttached, x => x.VariableName.Value == GasVarName);
        if (gasAttached)
        {
            gasLevel.VariableName.Value = GasVarName;
            DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            gasLevel.Value.Value = worldVar?.TryReadValue(GasVarName, out float gasLevelValue) ?? false ? gasLevelValue : -100f;
        }

        gasLevel.Value.OnValueChange += val =>
        {
            List<SM64Mario> marios = AllMarios.Values.GetTempList();
            foreach (SM64Mario mario in marios)
            {
                Interop.SetGasLevel(mario.MarioId, val.Value);
            }
        };

        // Setup Container Slots
        ResoniteMod.Msg($"[InitContextWorld] Creating All Mario Container Slot");
        MarioContainersSlot = ContextSlot.FindChildOrAdd(MarioContainersSlotName, false);
        MarioContainersSlot.OrderOffset = 1000;
        MarioContainersSlot.Tag = MarioContainersTag;

        ResoniteMod.Msg($"[InitContextWorld] Creating My Mario Container Slot");;
        MyMariosSlot = MarioContainersSlot.FindChildOrAdd($"{world.LocalUser.UserName}'s Marios", false);
        MyMariosSlot.OrderOffset = MyMariosSlot.LocalUser.AllocationID * 3;
        MyMariosSlot.Tag = MarioContainerTag;

        ResoniteMod.Msg($"[InitContextWorld] Subscribing to ChildAdded for {MarioContainersSlot.Name}");
        MarioContainersSlot.ChildAdded -= HandleContainerAdded;
        MarioContainersSlot.ChildAdded += HandleContainerAdded;

        MarioContainersSlot.ForeachChild(child =>
        {
            if (child.Tag == MarioContainerTag && child != MyMariosSlot)
            {
                ResoniteMod.Msg($"[InitContextWorld] Found other MarioContainer - {child.Name}");
                child.ChildAdded -= HandleMarioAdded;
                child.ChildAdded += HandleMarioAdded;
            }
        });

        MyMariosSlot.DestroyWhenUserLeaves(world.LocalUser);
    }

    private void HandleMarioAdded(Slot slot, Slot child)
    {
        if (child.Tag == MarioTag && !AllMarios.ContainsKey(child))
        {
            ResoniteMod.Msg($"[HandleMarioAdded] Adding Mario for Slot: {slot.Name} ({slot.ReferenceID})");
            SM64Mario mario = new SM64Mario(child, this);
            AllMarios.Add(child, mario);
        }
    }

    private void HandleContainerAdded(Slot slot, Slot child)
    {
        if (child.Tag == MarioContainerTag)
        {
            ResoniteMod.Msg("[HandleContainerAdded] New Container Added Subscribing");
            child.ChildAdded -= HandleMarioAdded;
            child.ChildAdded += HandleMarioAdded;
        }
    }

    private static Slot _tempSlot;
    public static Slot GetTempSlot(World world)
    {
        World currentWorld = world ?? Engine.Current.WorldManager.FocusedWorld;
        if (_tempSlot == null || _tempSlot.IsDestroyed || _tempSlot.World != currentWorld)
        {
            if (currentWorld == null) return null;

            Slot newSlot = currentWorld.RootSlot?.FindChildOrAdd(TempSlotName, false);
            if (newSlot == null) return null;

            if (_tempSlot != null)
            {
                _tempSlot.ChildAdded -= HandleSlotAdded;
            }

            _tempSlot = newSlot;
            _tempSlot.ChildAdded += HandleSlotAdded;
        }
        
        return _tempSlot;
    }

    public static void HandleSlotAdded(Slot slot, Slot child)
    {
        if (child.Tag == ContextTag)
        {
            ResoniteMod.Msg($"[HandleSlotAdded] A ContextSlot was added");
            if (SM64Context.EnsureInstanceExists(child.World, out SM64Context context))
            {
                context.MarioContainersSlot.ForeachChild(marioSlot =>
                {
                    if (marioSlot.Tag == MarioTag)
                    {
                        SM64Context.TryAddMario(marioSlot);
                    }
                });
            }
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

    // public static void UpdatePlayerMariosState()
    // {
    //     if (Instance == null) return;
    // 
    //     int maxMarios = Instance._maxMariosAnimatedPerPerson;
    //     foreach (SM64Mario mario in Instance.AllMarios.Values)
    //     {
    //         if (mario.IsLocal) continue;
    // 
    //         bool isOverLimit = maxMarios-- <= 0;
    //         mario.SetIsOverMaxCount(isOverLimit);
    //     }
    // }

    public static bool TryAddMario(Slot slot) => AddMario(slot) != null;

    public static SM64Mario AddMario(Slot slot)
    {
        bool success = EnsureInstanceExists(slot.World, out SM64Context instance);
        if (!success) return null;

        SM64Mario mario = null;
        if (!instance.AllMarios.ContainsKey(slot))
        {
            slot.Parent = instance.MyMariosSlot;

            ResoniteMod.Msg($"[AddMario] Adding Mario for Slot: {slot.Name} ({slot.ReferenceID})");

            mario = new SM64Mario(slot, instance);
            instance.AllMarios.Add(slot, mario);
            if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayRandomMusic)) Interop.PlayRandomMusic();

            ResoniteMod.Msg($"[AddMario] Added mario for Slot: {slot.Name} ({slot.ReferenceID})");
        }

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

                        ResoniteMod.Msg($"[AddMario] Adding existing Mario for Slot: {child2.Name} ({child2.ReferenceID})");

                        SM64Mario mario2 = new SM64Mario(child2, instance);
                        instance.AllMarios.Add(child2, mario2);

                        ResoniteMod.Msg($"[AddMario] Added existing Mario for Slot: {child2.Name} ({child2.ReferenceID})");
                    }
                }

                instance.ReloadAllColliders(false);
            });
        }

        return mario;
    }

    public static void RemoveMario(SM64Mario mario)
    {
        Interop.MarioDelete(mario.MarioId);

        mario.Context.AllMarios.Remove(mario.MarioSlot);
        // UpdatePlayerMariosState();

        if (mario.Context.AllMarios.Count == 0)
        {
            Interop.StopMusic();
        }
    }

    public static bool EnsureInstanceExists(World world, out SM64Context instance)
    {
        // We don't have Instancing for LibSM64, so we can't have multiple instances for separate worlds.
        if (Instance != null && world != Instance.World)
        {
            bool destroy = world.Focus == World.WorldFocus.Focused;
            ResoniteMod.Error($"[EnsureInstanceExists] Tried to create instance while one already exists. Replace? - {(destroy ? "True" : "False")}");
            if (destroy)
            {
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

        ResoniteMod.Debug("[EnsureInstanceExists] Ensuring SM64Context instance exists for world: " + world.Name);
        Instance = new SM64Context(world);
        ResoniteMod.Debug("[EnsureInstanceExists] Instance Created!");
        instance = Instance;

        return true;
    }

    private void HandleInstanceRemoved(Slot slot) => Dispose();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            ResoniteMod.Debug("[SM64Context Dispose] Disposing SM64Context");

            // Explode Marios
            List<SM64Mario> marios = AllMarios.Values.GetTempList();
            foreach (SM64Mario mario in marios)
            {
                mario?.Dispose();
            }

            AllMarios.Clear();

            // Explode Colliders
            List<SM64DynamicCollider> dynamicColliders = DynamicColliders.Values.GetTempList();
            foreach (SM64DynamicCollider col in dynamicColliders)
            {
                col?.Dispose();
            }

            DynamicColliders.Clear();

            // Free Locomotion
            if ((!World.IsDestroyed || !World.IsDisposed) && World.CanCurrentThreadModify)
            {
                LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
                loco?.SupressSources?.RemoveAll(InputBlock);
            }

            // Explode AudioSlot if created by LocalUser
            if (AudioSlot is { IsRemoved: false } && AudioSlot.GetAllocatingUser() == AudioSlot.LocalUser)
            {
                AudioSlot?.Destroy();
                AudioSlot = null;
            }

            // Explode our Mario Container
            if (MyMariosSlot is { IsRemoved: false })
            {
                MyMariosSlot?.Destroy();
                MyMariosSlot = null;
            }

            // Explode ContextSlot if any no marios are left
            // This is DEBUG
            bool toRemove = true;
            foreach (Slot child in MarioContainersSlot.Children)
            {
                if (child.Children.Any())
                {
                    toRemove = false;
                }
            }

            if (toRemove)
            {
                ContextSlot?.Destroy();
            }

            MarioContainersSlot = null;

            ResoniteMod.Debug("[SM64Context Dispose] Finished disposing SM64Context");
        }

        Interop.GlobalTerminate();

        Instance = null;
    }
}