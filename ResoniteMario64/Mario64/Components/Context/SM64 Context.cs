using FrooxEngine;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.Components.Objects;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Mario64.Components.Context;

public sealed partial class SM64Context : IDisposable
{
    public static SM64Context Instance { get; private set; }

    public DynamicVariableSpace ContextVariableSpace { get; private set; }

    private Slot _contextSlot;
    internal Slot ContextSlot
    {
        get
        {
            Slot tempSlot = SM64Context.GetTempSlot(World);
            if (_contextSlot != null && (!_contextSlot.IsDestroyed || tempSlot is not { IsDestroyed: false })) return _contextSlot;

            _contextSlot = tempSlot.FindChild(x => x.Tag == ContextTag);
            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleRemoved;
                _contextSlot.OnPrepareDestroy += HandleRemoved;
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
                }
            }

            _contextSlot = value;

            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleRemoved;
                _contextSlot.OnPrepareDestroy += HandleRemoved;
            }
        }
    }

    public Slot MarioContainersSlot { get; private set; }
    public Slot MyMariosSlot { get; private set; }

    public SM64ContextInputs Inputs { get; private set; }
    public SM64ContextTerrain Terrain { get; private set; }
    public SM64ContextAudio Audio { get; private set; }

    public readonly Dictionary<Slot, SM64Mario> AllMarios = new Dictionary<Slot, SM64Mario>();

    public List<SM64Mario> MyMarios => AllMarios.Values.Where(x => x.IsLocal).GetTempList();

    public bool AnyControlledMarios => AllMarios.Values.Any(x => x.IsLocal);

    public World World { get; }
    public DynamicVariableSpace WorldVariableSpace { get; private set; }

    internal double LastTick;
    public bool IsDisposed { get; private set; }

    private int _maxMariosAnimatedPerPerson;

    private SM64Context(World world)
    {
        const string caller = nameof(SM64Context);

        World = world;
        world.WorldDestroyed += HandleRemoved;

        InitContextWorld(world);

        if (!SM64Interop.IsGlobalInit)
        {
            Logger.Debug("Init SM64", caller);
            if (!SM64Interop.GlobalInit(Mario64Manager.RomBytes))
            {
                throw new Exception("Mario64 Global Init Failed...");
            }
        }

        Inputs = new SM64ContextInputs(this);
        Terrain = new SM64ContextTerrain(this);
        Audio = new SM64ContextAudio(this);

        // Update context's colliders
        SM64Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(world));

        _maxMariosAnimatedPerPerson = Config.MaxMariosPerPerson.Value;
        Config.MaxMariosPerPerson.SettingChanged += HandleMaxMariosPerPersonChanged;

        world.RunInUpdates(3, () => { World.RootSlot.ForeachComponentInChildren<Collider>(c => Terrain.HandleCollider(c)); });
    }

    private void InitContextWorld(World world)
    {
        try
        {
            if (ContextSlot == null)
            {
                Slot contextSlot = SM64Context.GetTempSlot(world).AddSlot(ContextSlotName, false);
                contextSlot.OrderOffset = -1000;
                contextSlot.Tag = ContextTag;

                ContextSlot = contextSlot;
            }

            ContextSlot.GetComponentOrAttach<ObjectRoot>();

            // Variable Space
            ContextVariableSpace = ContextSlot.GetComponentOrAttach<DynamicVariableSpace>(out bool spaceAttached);
            if (spaceAttached)
            {
                ContextVariableSpace.SpaceName.Value = ContextSpaceName;
            }

            WorldVariableSpace = World.RootSlot.GetComponent<DynamicVariableSpace>(x => x.SpaceName.Value == "World");
            if (WorldVariableSpace == null)
            {
                WorldVariableSpace = World.RootSlot.AttachComponent<DynamicVariableSpace>();
                WorldVariableSpace.SpaceName.Value = "World";
            }

            DynamicReferenceVariable<Slot> contextWorldVeriable = ContextSlot.GetComponentOrAttach<DynamicReferenceVariable<Slot>>();
            contextWorldVeriable.Reference.Target = ContextSlot;
            contextWorldVeriable.VariableName.Value = $"World/{ContextSpaceName}";

            // Context Host
            DynamicReferenceVariable<User> contextHost = ContextSlot.GetComponentOrAttach<DynamicReferenceVariable<User>>(out bool hostAttached);
            if (hostAttached)
            {
                contextHost.VariableName.Value = HostVarName;
                contextHost.Reference.Target = world.LocalUser;
            }

            contextHost.Reference.OnTargetChange += reference =>
            {
                if (reference.Target != null) return;

                Logger.Warn("[HostContext TargetChange] SM64Context host reference was set to null, resetting to local user");
                ContextSlot.RunInUpdates(ContextSlot.LocalUser.AllocationID * 3, () => { contextHost.Reference.Target ??= world.LocalUser; });
            };

            Slot configSlot = ContextSlot.FindChildOrAdd(ConfigSlotName, false);
            configSlot.Destroyed -= HandleRemoved;
            configSlot.Destroyed += HandleRemoved;

            // Scale
            DynamicValueVariable<float> scale = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool scaleAttached, x => x.VariableName.Value == ScaleVarName);
            if (scaleAttached || contextHost.Reference.Target == null)
            {
                scale.VariableName.Value = ScaleVarName;
                scale.Value.Value = WorldVariableSpace?.TryReadValue(ScaleVarName, out float scaleValue) ?? false ? scaleValue : Config.MarioScaleFactor.Value;
            }

            scale.Value.OnValueChange += val =>
            {
                Dispose();
                SM64Context.EnsureInstanceExists(val.World, out _);
            };

            // Water Level
            DynamicValueVariable<float> waterLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool waterAttached, x => x.VariableName.Value == WaterVarName);
            if (waterAttached)
            {
                waterLevel.VariableName.Value = WaterVarName;
                waterLevel.Value.Value = WorldVariableSpace?.TryReadValue(WaterVarName, out float waterLevelValue) ?? false ? waterLevelValue : -100f;
            }

            waterLevel.Value.OnValueChange += val =>
            {
                foreach (SM64Mario mario in AllMarios.Values.GetTempList())
                {
                    SM64Interop.SetWaterLevel(mario.MarioId, val.Value);
                }
            };

            // Gas Level
            DynamicValueVariable<float> gasLevel = configSlot.GetComponentOrAttach<DynamicValueVariable<float>>(out bool gasAttached, x => x.VariableName.Value == GasVarName);
            if (gasAttached)
            {
                gasLevel.VariableName.Value = GasVarName;
                gasLevel.Value.Value = WorldVariableSpace?.TryReadValue(GasVarName, out float gasLevelValue) ?? false ? gasLevelValue : -100f;
            }

            gasLevel.Value.OnValueChange += val =>
            {
                foreach (SM64Mario mario in AllMarios.Values.GetTempList())
                {
                    SM64Interop.SetGasLevel(mario.MarioId, val.Value);
                }
            };

            // Setup Container Slots
            if (MarioContainersSlot != null)
            {
                MarioContainersSlot.Destroyed -= HandleRemoved;
                MarioContainersSlot.ChildAdded -= HandleContainerAdded;
            }

            MarioContainersSlot = ContextSlot.FindChildOrAdd(MarioContainersSlotName, false);
            MarioContainersSlot.OrderOffset = 1000;
            MarioContainersSlot.Tag = MarioContainersTag;
            MarioContainersSlot.Destroyed += HandleRemoved;

            MyMariosSlot = MarioContainersSlot.FindChild(x => x.Name == $"{world.LocalUser.UserName}'s Marios") ?? SM64Context.GetTempSlot(world).AddSlot($"{world.LocalUser.UserName}'s Marios", false);
            MyMariosSlot.OrderOffset = MyMariosSlot.LocalUser.AllocationID * 3;
            MyMariosSlot.Tag = MarioContainerTag;

            MyMariosSlot.GetComponentOrAttach<DestroyOnUserLeave>().TargetUser.Target = world.LocalUser;

            DynamicReferenceVariable<Slot> myMarioDynvar = MyMariosSlot.GetComponentOrAttach<DynamicReferenceVariable<Slot>>();
            myMarioDynvar.VariableName.Value = $"{world.LocalUser.UserID ?? world.LocalUser.UserName.MakeValidDynvarName()}-Marios";
            myMarioDynvar.Reference.Target = MyMariosSlot;

            world.RunInUpdates(2, () => MyMariosSlot.SetParent(MarioContainersSlot));

            MarioContainersSlot.ChildAdded += HandleContainerAdded;

            MarioContainersSlot.ForeachChild(child =>
            {
                if (child.Tag != MarioContainerTag || child == MyMariosSlot) return;

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

        child.ChildAdded -= HandleMarioAdded;
        child.ChildAdded += HandleMarioAdded;

        child.ForeachChild(child2 =>
        {
            if (child2.Tag != MarioTag || AllMarios.ContainsKey(child2)) return;

            child2.RunSynchronously(() => SM64Context.TryAddMario(child2, false));
        });
    }

    public void OnCommonUpdate()
    {
        if (IsDisposed) return;

        if (World.InputInterface.GetKeyDown(Config.RefreshCollidersKey.Value) && Config.DebugEnabled.Value)
        {
            ReloadAllColliders();
        }

        if (World.InputInterface.GetKeyDown(Config.LogColliderKey.Value) && Config.DebugEnabled.Value)
        {
            GetAllColliders(true);
        }

        Terrain.OnCommonUpdate();
        Inputs.OnCommonUpdate();

        if (World.Time.WorldTime - LastTick >= Config.GameTickMs.Value / 1000f)
        {
            SM64GameTick();
            LastTick = World.Time.WorldTime;
        }

        foreach (SM64Mario mario in AllMarios.Values.GetTempList())
        {
            mario.ContextUpdateSynced();
        }
    }

    private void SM64GameTick()
    {
        if (IsDisposed) return;

        foreach (SM64DynamicCollider dynamicCol in Terrain.DynamicColliders.Values.GetTempList())
        {
            dynamicCol?.ContextFixedUpdateSynced();
        }

        foreach (SM64FakeObject obj in Terrain.FakeObjects.Values.GetTempList())
        {
            obj?.ContextFixedUpdateSynced();
        }

        foreach (SM64Mario mario in AllMarios.Values.GetTempList())
        {
            mario?.ContextFixedUpdateSynced();
        }
    }

    private void HandleRemoved(Slot slot)
    {
        if (IsDisposed) return;

        Dispose();
    }

    private void HandleRemoved(IDestroyable destroyable)
    {
        if (IsDisposed) return;

        Dispose();
    }

    private void HandleRemoved(World world)
    {
        if (IsDisposed) return;

        Dispose();
    }

    private void HandleMaxMariosPerPersonChanged(object sender, EventArgs args)
    {
        _maxMariosAnimatedPerPerson = Config.MaxMariosPerPerson.Value;
        UpdatePlayerMariosState();
    }

    public void UpdatePlayerMariosState()
    {
        int maxPerPerson = _maxMariosAnimatedPerPerson;

        foreach (User user in World.AllUsers.Where(x => !x.IsLocalUser))
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

    public void ReloadAllColliders(bool log = true) => Terrain.ReloadAllColliders(log);

    public Dictionary<ColliderCategory, List<ISM64Object>> GetAllColliders(bool log) => Terrain.GetAllColliders(log);

    public void HandleCollider(Collider collider, bool log = true) => Terrain.HandleCollider(collider, log);

    public static void VibrateCallback(int marioId, short level, short time) => SM64ContextInputs.VibrateCallback(marioId, level, time);

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (disposing)
        {
            // Unsubscribe from all events to prevent memory leaks
            if (World != null) World.WorldDestroyed -= HandleRemoved;
            Config.MaxMariosPerPerson.SettingChanged -= HandleMaxMariosPerPersonChanged;

            if (_contextSlot != null)
            {
                _contextSlot.OnPrepareDestroy -= HandleRemoved;
            }

            Terrain?.Dispose();
            Inputs?.Dispose();
            Audio?.Dispose();

            // Dispose of all Lists
            foreach (var mario in AllMarios.Values.GetTempList())
            {
                mario?.Dispose();
            }

            AllMarios.Clear();

            // Nullify references to Resonite objects
            _contextSlot = null;
            MyMariosSlot = null;
            MarioContainersSlot = null;
            ContextVariableSpace = null;
            WorldVariableSpace = null;
            Inputs = null;
            Terrain = null;
            Audio = null;
        }

        // Free unmanaged resources (from the C++ library)
        SM64Interop.GlobalTerminate();

        // Finally, nullify the static instance
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
