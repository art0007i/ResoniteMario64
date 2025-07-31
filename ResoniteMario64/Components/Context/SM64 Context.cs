using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                    _contextSlot.OnPrepareDestroy -= HandleSlotRemoved;
                    _contextSlot.OnPrepareDestroy += HandleSlotRemoved;
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
                _contextSlot.OnPrepareDestroy -= HandleSlotRemoved;
            }

            _contextSlot = value;

            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleSlotRemoved;
                _contextSlot.OnPrepareDestroy += HandleSlotRemoved;
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

        ResoniteMario64.KeyUseGamepad.OnChanged += _ =>
        {
            World.Input.InvalidateBindings();
        };

        InitContextWorld(world);

        if (!Interop.IsGlobalInit)
        {
            Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);
        }

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(world));
        ResoniteMario64.KeyMaxMeshColliderTris.OnChanged += _ => { _forceUpdate = true; };
        world.RunInUpdates(3, () =>
        {
            World.RootSlot.ForeachComponentInChildren<Collider>(c =>
            {
                if (Utils.IsGoodDynamicCollider(c) || Utils.IsGoodInteractable(c) || Utils.IsGoodWaterBox(c))
                {
                    AddCollider(c);
                }
            });
        });
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
                ContextSlot.RunInUpdates(ContextSlot.LocalUser.AllocationID * 3, () =>
                {
                    if (contextHost.Reference.Target == null)
                    {
                        ResoniteMod.Error("SM64Context host reference was set to null, resetting to local user.");
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
            scale.Value.Value = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMarioScaleFactor);
        }

        scale.Value.OnValueChange += val =>
        {
            SM64Context.Instance?.Dispose();
            SM64Context.EnsureInstanceExists(val.World);
        };

        ResoniteMario64.KeyMarioScaleFactor.OnChanged += value =>
        {
            float newScale = (float)(value ?? 0f);
            User host = ContextVariableSpace.TryReadValue(HostVarName, out User host1) ? host1 ?? ContextSlot.GetAllocatingUser() : null;
            if (host is { IsLocalUser: true })
            {
                ContextSlot.WriteDynamicVariable(ScaleVarName, newScale);
            }
        };

        // Water Level
        DynamicValueVariable<float> waterLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool waterAttached, x => x.VariableName.Value == WaterVarName);
        if (waterAttached)
        {
            waterLevel.VariableName.Value = WaterVarName;
            DynamicVariableSpace worldVar = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            waterLevel.Value.Value = worldVar.TryReadValue(WaterVarName, out float waterLevelValue) ? waterLevelValue : -100f;
        }

        waterLevel.Value.OnValueChange += val =>
        {
            List<SM64Mario> marios = Marios.Values.GetTempList();
            foreach (SM64Mario mario in marios)
            {
                Interop.SetWaterLevel(mario.MarioId, val.Value);
            }
        };

        // Setup Container Slots
        MarioContainersSlot = ContextSlot.FindChildOrAdd(MarioContainersSlotName, false);
        MarioContainersSlot.OrderOffset = 1000;
        MarioContainersSlot.Tag = null;

        MyMariosSlot = MarioContainersSlot.FindChildOrAdd($"{world.LocalUser.UserName}'s Marios", false);
        MyMariosSlot.OrderOffset = MyMariosSlot.LocalUser.AllocationID * 3;
        MyMariosSlot.Tag = null;

        MyMariosSlot.DestroyWhenUserLeaves(world.LocalUser);
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

        List<SM64DynamicCollider> dynamicColliders = _sm64DynamicColliders.Values.GetTempList();
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

        bool success = EnsureInstanceExists(slot.World);
        if (!success) return null;

        if (!Instance.Marios.ContainsKey(slot))
        {
            slot.Parent = Instance.MyMariosSlot;

            mario = new SM64Mario(slot);
            Instance.Marios.Add(slot, mario);
            if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayRandomMusic)) Interop.PlayRandomMusic();
        }

        ResoniteMod.Msg("Added mario for SlotID: " + slot.ReferenceID);

        slot.Parent.RunInUpdates(3, () =>
        {
            slot.Parent.Children.Where(x => x.Tag == MarioTag && !Instance.Marios.ContainsKey(x)).Do(root2 =>
            {
                ResoniteMod.Msg("Adding existing Mario for SlotID: " + root2.ReferenceID);
                var mario2 = new SM64Mario(root2);
                Instance.Marios.Add(root2, mario2);
            });
        });

        SM64Context.Instance._forceUpdate = true;

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

    public static bool EnsureInstanceExists(World world)
    {
        if (Instance != null && world != Instance.World)
        {
            bool destroy = world.Focus == World.WorldFocus.Focused;
            ResoniteMod.Error("Tried to create instance while one already exists." + (destroy ? " It will be replaced by a new one." : ""));
            if (destroy)
            {
                Instance.Dispose();
            }
            else
            {
                return false;
            }
        }

        if (Instance != null) return true;

        ResoniteMod.Debug("Ensuring SM64Context instance exists for world: " + world.Name);
        Instance = new SM64Context(world);
        ResoniteMod.Debug("Instance Created!");

        return Instance != null;
    }

    private static void HandleSlotRemoved(Slot slot) => SM64Context.Instance?.Dispose();

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
            List<SM64DynamicCollider> dynamicColliders = _sm64DynamicColliders.Values.GetTempList();
            foreach (SM64DynamicCollider col in dynamicColliders)
            {
                col?.Dispose();
            }

            _sm64DynamicColliders.Clear();

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