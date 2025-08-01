using System;
using System.Collections.Generic;
using System.Linq;
using FrooxEngine;
using HarmonyLib;
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

    private Slot TempSlot => World.RootSlot?.FindChildOrAdd(TempSlotName, false);

    private Slot _contextSlot;

    private Slot ContextSlot
    {
        get
        {
            if (_contextSlot == null || _contextSlot.IsDestroyed && TempSlot is { IsDestroyed: false })
            {
                _contextSlot = TempSlot.FindChild(x => x.Tag == ContextTag);
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
                ResoniteMod.Error($"Tried to set SM64 Context but tag was {value.Tag} instead of {ContextTag}.");
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

    public DynamicVariableSpace ContextVariableSpace { get; set; }

    public readonly Dictionary<Slot, SM64Mario> Marios = new Dictionary<Slot, SM64Mario>();

    public bool AnyControlledMarios => Marios.Values.Any(x => x.IsLocal);

    public World World { get; }

    internal double LastTick;

    private bool _forceUpdate;

    private bool _disposed;

    private SM64Context(World world)
    {
        World = world;
        world.WorldDestroyed += _ => Dispose();

        ResoniteMario64.KeyUseGamepad.OnChanged += _ => { World.Input.InvalidateBindings(); };

        InitContextWorld(world);

        if (!Interop.IsGlobalInit)
        {
            Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);
        }

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(world));
        ResoniteMario64.KeyMaxMeshColliderTris.OnChanged += _ => { _forceUpdate = true; };
        world.RunInUpdates(3, () => { World.RootSlot.ForeachComponentInChildren<Collider>(AddCollider); });
    }

    public void InitContextWorld(World world)
    {
        if (ContextSlot == null)
        {
            Slot contextSlot = TempSlot.AddSlot(ContextSlotName, false);
            contextSlot.OrderOffset = -1000;
            contextSlot.Tag = ContextTag;

            ContextSlot = contextSlot;
        }

        // Variable Space
        ContextVariableSpace = ContextSlot.GetComponentOrAttach<DynamicVariableSpace>(out bool spaceAttached);
        if (spaceAttached)
        {
            ContextVariableSpace.SpaceName.Value = ContextSpaceName;
        }

        // Context Host
        DynamicReferenceVariable<User> contextHost = ContextSlot.GetComponentOrAttach<DynamicReferenceVariable<User>>(out bool hostAttached);
        if (hostAttached)
        {
            contextHost.VariableName.Value = HostVarName;
            contextHost.Reference.Target = world.LocalUser;
        }

        contextHost.Reference.OnTargetChange += reference =>
        {
            if (reference.Target == null)
            {
                ResoniteMod.Error("SM64Context host reference was set to null, resetting to the next local user.");
                ContextSlot.RunInUpdates(ContextSlot.LocalUser.AllocationID * 3, () =>
                {
                    if (contextHost.Reference.Target == null)
                    {
                        contextHost.Reference.Target = world.LocalUser;
                    }
                });
            }
        };

        Slot configSlot = ContextSlot.FindChildOrAdd(ConfigSlotName, false);

        // Scale
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
        DynamicValueVariable<float> waterLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool waterAttached, x => x.VariableName.Value == WaterVarName);
        if (waterAttached)
        {
            waterLevel.VariableName.Value = WaterVarName;
            DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            waterLevel.Value.Value = worldVar?.TryReadValue(WaterVarName, out float waterLevelValue) ?? false ? waterLevelValue : -100f;
        }

        waterLevel.Value.OnValueChange += val =>
        {
            List<SM64Mario> marios = Marios.Values.GetTempList();
            foreach (SM64Mario mario in marios)
            {
                Interop.SetWaterLevel(mario.MarioId, val.Value);
            }
        };

        // Gas Level
        DynamicValueVariable<float> gasLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool gasAttached, x => x.VariableName.Value == GasVarName);
        if (gasAttached)
        {
            gasLevel.VariableName.Value = GasVarName;
            DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            gasLevel.Value.Value = worldVar?.TryReadValue(GasVarName, out float gasLevelValue) ?? false ? gasLevelValue : -100f;
        }

        gasLevel.Value.OnValueChange += val =>
        {
            List<SM64Mario> marios = Marios.Values.GetTempList();
            foreach (SM64Mario mario in marios)
            {
                Interop.SetGasLevel(mario.MarioId, val.Value);
            }
        };

        // Setup Container Slots
        MarioContainersSlot = ContextSlot.FindChildOrAdd(MarioContainersSlotName, false);
        MarioContainersSlot.OrderOffset = 1000;
        MarioContainersSlot.Tag = MarioContainersTag;

        MyMariosSlot = MarioContainersSlot.FindChildOrAdd($"{world.LocalUser.UserName}'s Marios", false);
        MyMariosSlot.OrderOffset = MyMariosSlot.LocalUser.AllocationID * 3;
        MyMariosSlot.Tag = MarioContainerTag;

        MarioContainersSlot.ChildAdded -= HandleContainerAdded;
        MarioContainersSlot.ChildAdded += HandleContainerAdded;

        MarioContainersSlot.ForeachChild(c =>
        {
            if (c.Tag == MarioTag && !Marios.ContainsKey(c))
            {
                ResoniteMod.Msg("Adding existing Mario for SlotID: " + c.ReferenceID);
                var mario = new SM64Mario(c, this);
                Marios.Add(c, mario);
            }
        });

        MyMariosSlot.DestroyWhenUserLeaves(world.LocalUser);
    }

    private void HandleMarioAdded(Slot slot, Slot child)
    {
        if (child.Tag == MarioTag && !Marios.ContainsKey(child))
        {
            ResoniteMod.Msg("Adding existing Mario for SlotID: " + child.ReferenceID);
            var mario = new SM64Mario(child, this);
            Marios.Add(child, mario);
        }
    }

    private void HandleContainerAdded(Slot slot, Slot child)
    {
        if (child.Tag == MarioContainerTag)
        {
            child.ChildAdded -= HandleMarioAdded;
            child.ChildAdded += HandleMarioAdded;
        }
    }

    public void OnCommonUpdate()
    {
        if (_disposed) return;

        if (World.InputInterface.GetKeyDown(Key.Semicolon))
        {
            _forceUpdate = true;
        }

        if (_forceUpdate)
        {
            StaticTerrainUpdate();
            _forceUpdate = false;
        }

        HandleInputs();

        if (World.Time.WorldTime - LastTick >= ResoniteMario64.Config.GetValue(ResoniteMario64.KeyGameTickMs) / 1000f)
        {
            SM64GameTick();
            LastTick = World.Time.WorldTime;
        }

        ProcessAudio();

        List<SM64Mario> marios = Marios.Values.GetTempList();
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

        List<SM64Mario> marios = Marios.Values.GetTempList();
        foreach (SM64Mario mario in marios)
        {
            mario?.ContextFixedUpdateSynced();
        }
    }

    public static bool TryAddMario(Slot slot) => AddMario(slot) != null;

    public static SM64Mario AddMario(Slot slot)
    {
        ResoniteMod.Msg($"Adding Mario for SlotID: {slot.ReferenceID}");

        SM64Mario mario = null;

        bool success = EnsureInstanceExists(slot.World, out SM64Context instance);
        if (!success) return null;

        if (!instance.Marios.ContainsKey(slot))
        {
            slot.Parent = instance.MyMariosSlot;

            mario = new SM64Mario(slot, instance);
            instance.Marios.Add(slot, mario);
            if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayRandomMusic)) Interop.PlayRandomMusic();
        }

        ResoniteMod.Msg("Added mario for SlotID: " + slot.ReferenceID);

        slot.Parent.RunInUpdates(3, () =>
        {
            slot.Parent.Children.Where(x => x.Tag == MarioTag && !instance.Marios.ContainsKey(x)).Do(root2 =>
            {
                ResoniteMod.Msg("Adding existing Mario for SlotID: " + root2.ReferenceID);
                var mario2 = new SM64Mario(root2, instance);
                instance.Marios.Add(root2, mario2);
            });
        });

        instance._forceUpdate = true;

        return mario;
    }

    public static void RemoveMario(SM64Mario mario)
    {
        Interop.MarioDelete(mario.MarioId);

        mario.Context.Marios.Remove(mario.MarioSlot);

        if (mario.Context.Marios.Count == 0)
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
            ResoniteMod.Error("Tried to create instance while one already exists." + (destroy ? " It will be replaced by a new one." : ""));
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

        ResoniteMod.Debug("Ensuring SM64Context instance exists for world: " + world.Name);
        Instance = new SM64Context(world);
        ResoniteMod.Debug("Instance Created!");
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
            ResoniteMod.Debug("Disposing SM64Context");

            // Explode Marios
            List<SM64Mario> marios = Marios.Values.GetTempList();
            foreach (SM64Mario mario in marios)
            {
                mario?.Dispose();
            }

            Marios.Clear();

            // Explode Colliders
            List<SM64DynamicCollider> dynamicColliders = DynamicColliders.Values.GetTempList();
            foreach (SM64DynamicCollider col in dynamicColliders)
            {
                col?.Dispose();
            }

            DynamicColliders.Clear();

            // Free Locomotion
            LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
            loco?.SupressSources?.RemoveAll(InputBlock);

            // Explode AudioSlot if created by LocalUser
            if (AudioSlot is { IsRemoved: false } && AudioSlot.GetAllocatingUser() == AudioSlot.LocalUser)
            {
                AudioSlot?.Destroy();
                AudioSlot = null;
            }

            // Explode out Mario Container
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

            ResoniteMod.Debug("Finished disposing SM64Context");
        }

        Interop.GlobalTerminate();

        Instance = null;
    }
}