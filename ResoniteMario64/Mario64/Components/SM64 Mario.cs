using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using Renderite.Shared;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.Components.Objects;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Mario64.Components;

public sealed class SM64Mario : ISM64Object
{

    #region Fields & Properties

    #region Constants & Static Members

    private static float MarioScale => 1000.0f / SM64Interop.ScaleFactor;
    private static float _skipFarMarioDistance;
    private static int _marioCollisionSampleCount;

    #endregion

    #region State Flags & Core Properties

    public int MarioId { get; }
    private bool _enabled;
    private bool _isDying;
    private bool _isNuked;
    private bool _initialized;
    private int _buffIndex; // Used for state double-buffering

    public bool IsDisposed { get; private set; }

    #endregion

    #region Resonite Components & Properties

    public Slot MarioSlot { get; private set; }
    public User MarioUser { get; private set; }
    public DynamicVariableSpace MarioSpace { get; private set; }
    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public bool IsLocal => MarioUser.IsLocalUser;
    private readonly Slider _marioGrabbable;
    private readonly CapsuleCollider _marioCollider;
    public Collider Collider => _marioCollider;

    #endregion

    #region Mario State & Physics

    private readonly SM64MarioState[] _states = new SM64MarioState[2];
    public SM64MarioState CurrentState => _states[1 - _buffIndex];
    public SM64MarioState PreviousState => _states[_buffIndex];

    private bool _wasPickedUp;
    public bool IsBeingGrabbed => _marioGrabbable.IsGrabbed;

    public bool IsTeleporting { get; set; }

    internal SM64Teleporter LastTeleportDestination;

    public float3 MarioSpawn { get; private set; }

    #endregion

    #region Environment Interaction

    private float _waterLevel;
    private float _gasLevel;

    #endregion

    #region Culling & Optimization

    private bool _isOverMaxCount;
    private bool _isOverMaxDistance;
    private bool _wasBypassed;

    #endregion

    #region Rendering & Mesh Buffers

    // Renderer Slots
    private Slot _marioRendererSlot;
    private Slot _marioNonModdedRendererSlot;

    // Renderer Components
    private MeshRenderer _marioMeshRenderer;
    private MeshX _marioMesh;
    private LocalMeshProvider _marioMeshProvider;

    // Materials
    private bool _isMatSwitching;
    private bool _isMat2Switching;
    private PBS_DualSidedMetallic _marioMaterial;
    private PBS_VertexColorMetallic _marioMaterialClipped;
    private XiexeToonMaterial _marioMaterialMetal;
    private PBS_Metallic _marioMaterialVanish;

    // Geo Buffers
    private float3[][] _positionBuffers;
    private float3[][] _normalBuffers;
    private float3[] _lerpPositionBuffer;
    private float3[] _lerpNormalBuffer;
    private float2[] _uvBuffer;
    private float3[] _colorBuffer;
    private color[] _colorBufferColors;
    private ushort _numTrianglesUsed;
    private ushort _previousNumTrianglesUsed;

    #endregion

    #region Material Properties

    private IAssetProvider<Material> CurrentMaterial
    {
        get => _marioMeshRenderer.Materials.Count > 0 ? _marioMeshRenderer.Materials[0] : null;
        set
        {
            SyncAssetList<Material> mats = _marioMeshRenderer.Materials;
            if (_isMatSwitching || mats.Count <= 0 || mats[0] == value) return;
            _isMatSwitching = true;
            _marioMeshRenderer.RunInUpdates(2, () =>
            {
                _marioMeshRenderer.Materials[0] = value;
                _isMatSwitching = false;
            });
        }
    }

    private IAssetProvider<Material> CurrentFaceMaterial
    {
        get => _marioMeshRenderer.Materials.Count > 1 ? _marioMeshRenderer.Materials[1] : null;
        set
        {
            SyncAssetList<Material> mats = _marioMeshRenderer.Materials;
            if (_isMat2Switching || mats.Count <= 1 || mats[1] == value) return;
            _isMat2Switching = true;
            _marioMeshRenderer.RunInUpdates(2, () =>
            {
                _marioMeshRenderer.Materials[1] = value;
                _isMat2Switching = false;
            });
        }
    }

    #endregion

    #region Input Properties & Streams

    // Input Properties
    private float2 Joystick
    {
        get => MarioSpace.TryReadValue(JoystickVarName, out IValue<float2> joystick) ? joystick?.Value ?? float2.Zero : float2.Zero;
        set => JoystickStream.Value = value;
    }

    private bool Jump
    {
        get => MarioSpace.TryReadValue(JumpVarName, out IValue<bool> jump) && (jump?.Value ?? false);
        set => JumpStream.Value = value;
    }

    private bool Punch
    {
        get => MarioSpace.TryReadValue(PunchVarName, out IValue<bool> kick) && (kick?.Value ?? false);
        set => PunchStream.Value = value;
    }

    private bool Crouch
    {
        get => MarioSpace.TryReadValue(CrouchVarName, out IValue<bool> stomp) && (stomp?.Value ?? false);
        set => CrouchStream.Value = value;
    }

    // Input Streams

    private ValueStream<float2> JoystickStream
    {
        get
        {
            if (field == null || field.IsRemoved)
            {
                field = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<float2>>(MarioSlot.LocalUser, $"SM64 {JoystickVarName} {Context.MyMarios.Count}", out bool created);
                if (created)
                {
                    field.Group = "SM64";
                    field.Encoding = ValueEncoding.Full;
                    field.SetUpdatePeriod(2, 0);
                    field.SetInterpolation();
                }
            }

            return field;
        }
    }

    private ValueStream<bool> JumpStream
    {
        get
        {
            if (field == null || field.IsRemoved)
            {
                field = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {JumpVarName} {Context.MyMarios.Count}", out bool created);
                if (created)
                {
                    field.Group = "SM64";
                    field.Encoding = ValueEncoding.Full;
                    field.SetUpdatePeriod(2, 0);
                    field.SetInterpolation();
                }
            }

            return field;
        }
    }

    private ValueStream<bool> PunchStream
    {
        get
        {
            if (field == null || field.IsRemoved)
            {
                field = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {PunchVarName} {Context.MyMarios.Count}", out bool created);
                if (created)
                {
                    field.Group = "SM64";
                    field.Encoding = ValueEncoding.Full;
                    field.SetUpdatePeriod(2, 0);
                    field.SetInterpolation();
                }
            }

            return field;
        }
    }

    private ValueStream<bool> CrouchStream
    {
        get
        {
            if (field == null || field.IsRemoved)
            {
                field = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {CrouchVarName} {Context.MyMarios.Count}", out bool created);
                if (created)
                {
                    field.Group = "SM64";
                    field.Encoding = ValueEncoding.Full;
                    field.SetUpdatePeriod(2, 0);
                    field.SetInterpolation();
                }
            }

            return field;
        }
    }

    #endregion

    #region Synced Variables & State

    public bool IsControlled => !Context.MyMarios.Any(x => x.SyncedControlled) || SyncedControlled;

    public bool SyncedControlled => MarioSpace.TryReadValue("IsControlled", out bool isControlled) && isControlled;

    public bool SyncedIsShown
    {
        get => MarioSpace.TryReadValue(IsShownVarName, out bool isShown) && isShown;
        set => MarioSpace.TryWriteValue(IsShownVarName, value);
    }

    public float SyncedHealthPoints
    {
        get => MarioSpace.TryReadValue(HealthPointsVarName, out float healthPoints) ? healthPoints : 0;
        set => MarioSpace.TryWriteValue(HealthPointsVarName, MathX.Clamp(value, 0, 8.5f));
    }

    public short SyncedHealthPointsRaw
    {
        get => MarioSpace.TryReadValue(HealthPointsVarName, out short healthPoints) ? healthPoints : (short)0;
        set => MarioSpace.TryWriteValue(HealthPointsVarName, MathX.Clamp(value, 0, 0x880));
    }

    public ActionFlag SyncedActionFlags
    {
        get => MarioSpace.TryReadValue(ActionFlagsVarName, out uint actionFlags) ? (ActionFlag)actionFlags : 0;
        set => MarioSpace.TryWriteValue(ActionFlagsVarName, (uint)value);
    }

    public StateFlag SyncedStateFlags
    {
        get => MarioSpace.TryReadValue(StateFlagsVarName, out uint stateFlags) ? (StateFlag)stateFlags : 0;
        set => MarioSpace.TryWriteValue(StateFlagsVarName, (uint)value);
    }

    public int SyncedStarCounter
    {
        get => MarioSpace.TryReadValue(StarVarName, out int starCounter) ? starCounter : 0;
        set => MarioSpace.TryWriteValue(StarVarName, MathX.Clamp(value, 0, 1000));
    }

    public int SyncedLives
    {
        get => MarioSpace.TryReadValue(LiveVarName, out int liveCounter) ? liveCounter : 0;
        set => MarioSpace.TryWriteValue(LiveVarName, MathX.Clamp(value, 0, 1000));
    }

    public int SyncedCoinCounter
    {
        get => MarioSpace.TryReadValue(CoinsVarName, out int coinCounter) ? coinCounter : 0;
        set => MarioSpace.TryWriteValue(CoinsVarName, MathX.Clamp(value, 0, 1000));
    }

    public int SyncedRedCoinCounter
    {
        get => MarioSpace.TryReadValue(RedCoinVarName, out int redCoinCounter) ? redCoinCounter : 0;
        set => MarioSpace.TryWriteValue(RedCoinVarName, MathX.Clamp(value, 0, 1000));
    }

    public MarioAnimationID SyncedAnimID
    {
        get => MarioSpace.TryReadValue(AnimIDVarName, out short animId) ? (MarioAnimationID)animId : 0;
        set => MarioSpace.TryWriteValue(AnimIDVarName, (short)value);
    }

    public short SyncedAnimFrame
    {
        get => MarioSpace.TryReadValue(AnimFrameVarName, out short animFrame) ? animFrame : (short)0;
        set => MarioSpace.TryWriteValue(AnimFrameVarName, value);
    }

    public AnimationFlags SyncedAnimFlags
    {
        get => MarioSpace.TryReadValue(AnimFlagVarName, out short animFlag) ? (AnimationFlags)animFlag : AnimationFlags.NoLoop;
        set => MarioSpace.TryWriteValue(AnimFlagVarName, (short)value);
    }

    public short SyncedStartFrame
    {
        get => MarioSpace.TryReadValue(StartFrameVarName, out short animId) ? animId : (short)0;
        set => MarioSpace.TryWriteValue(StartFrameVarName, value);
    }

    public short SyncedLoopStart
    {
        get => MarioSpace.TryReadValue(LoopStartVarName, out short animId) ? animId : (short)0;
        set => MarioSpace.TryWriteValue(LoopStartVarName, value);
    }

    public short SyncedLoopEnd
    {
        get => MarioSpace.TryReadValue(LoopEndVarName, out short animId) ? animId : (short)0;
        set => MarioSpace.TryWriteValue(LoopEndVarName, value);
    }

    public bool SyncedIsGrabbed
    {
        get => MarioSpace.TryReadValue(IsGrabbedVarName, out bool isGrabbed) && isGrabbed;
        set => MarioSpace.TryWriteValue(IsGrabbedVarName, value);
    }

    private readonly DynamicValueVariable<float> _marioAlphaVar;
    public float SyncedMarioAlpha
    {
        get
        {
            if (_marioAlphaVar == null || _marioAlphaVar.IsRemoved)
                return 1f;

            return _marioAlphaVar.Value;
        }
        set
        {
            if (_marioAlphaVar == null || _marioAlphaVar.IsRemoved || Math.Abs(_marioAlphaVar.Value.Value - value) < 0.001)
                return;

            _marioAlphaVar.RunSynchronously(() => _marioAlphaVar.Value.Value = value, true);
        }
    }

    public ActionFlag CurrentActionFlags => CurrentState.ActionFlags;
    public StateFlag CurrentStateFlags => CurrentState.StateFlags;

    private ActionFlag _lastActionFlags;
    // private StateFlag _lastStateFlags;

    #endregion

    #endregion

    static SM64Mario()
    {
        _skipFarMarioDistance = Config.MarioCullDistance.Value;
        Config.MarioCullDistance.SettingChanged += (_, _) => _skipFarMarioDistance = Config.MarioCullDistance.Value;

        _marioCollisionSampleCount = Config.MarioCollisionChecks.Value;
        Config.MarioCollisionChecks.SettingChanged += (_, _) => _marioCollisionSampleCount = Config.MarioCollisionChecks.Value;
    }

    public SM64Mario(Slot slot, SM64Context instance)
    {
        const string caller = nameof(SM64Mario);

        MarioUser = slot.GetAllocatingUser();

        World = instance.World;
        Context = instance;
        MarioSlot = slot;
        MarioSlot.Tag = MarioTag;

        MarioSlot.GetComponentOrAttach<ObjectRoot>();

        if (IsLocal)
        {
            int count = Context.AllMarios.Count(x => x.Value.IsLocal);
            MarioSlot.Name += $" #{count}";
        }

        MarioSpace = MarioSlot.GetComponentOrAttach<DynamicVariableSpace>();
        MarioSpace.SpaceName.Value = MarioSpaceName;

        _marioGrabbable = MarioSlot.GetComponentOrAttach<Slider>();
        if (IsLocal)
        {
            _marioGrabbable.DontDrive.Value = true;
            _marioGrabbable.Rotatable.Value = true;
        }

        _marioCollider = MarioSlot.GetComponentOrAttach<CapsuleCollider>();
        if (IsLocal)
        {
            _marioCollider.Offset.Value = new float3(0, 0.075f * MarioScale);
            _marioCollider.Radius.Value = 0.05f * MarioScale;
            _marioCollider.Height.Value = 0.15f * MarioScale;
        }

        MarioSlot.OnPrepareDestroy += HandleSlotDestroyed;

        float3 initPos = MarioSlot.GlobalPosition;
        MarioSpawn = initPos;
        MarioId = SM64Interop.MarioCreate(new float3(-initPos.x, initPos.y, initPos.z) * SM64Interop.ScaleFactor);

        if (MarioId == int.MaxValue || MarioId == int.MinValue || MarioId == -1)
        {
            Logger.Error("Failed to create Mario, Interop returned int.MaxValue", caller);
            return;
        }

        _waterLevel = Context.ContextVariableSpace.TryReadValue(WaterVarName, out float waterLevel) ? waterLevel : -100f;
        SM64Interop.SetWaterLevel(MarioId, _waterLevel);

        _gasLevel = Context.ContextVariableSpace.TryReadValue(GasVarName, out float gasLevel) ? gasLevel : -200f;
        SM64Interop.SetGasLevel(MarioId, _gasLevel);

        CreateMarioRenderer();

        MarioSlot.RunInUpdates(3, CreateNonModdedRenderer);

        if (IsLocal)
        {
            DynamicValueVariable<bool> isShown = MarioSlot.AttachComponent<DynamicValueVariable<bool>>();
            isShown.VariableName.Value = IsShownVarName;
            ValueUserOverride<bool> @override = isShown.Value.OverrideForUser(MarioUser, true);
            @override.CreateOverrideOnWrite.Value = true;

            Slot inputsSlot = MarioSlot.AddSlot("Inputs");
            inputsSlot.Tag = null;

            DynamicReferenceVariable<IValue<float2>> joystick1 = inputsSlot.AttachComponent<DynamicReferenceVariable<IValue<float2>>>();
            joystick1.VariableName.Value = JoystickVarName;
            joystick1.Reference.Target = JoystickStream;

            DynamicReferenceVariable<IValue<bool>> jump1 = inputsSlot.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
            jump1.VariableName.Value = JumpVarName;
            jump1.Reference.Target = JumpStream;

            DynamicReferenceVariable<IValue<bool>> kick1 = inputsSlot.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
            kick1.VariableName.Value = PunchVarName;
            kick1.Reference.Target = PunchStream;

            DynamicReferenceVariable<IValue<bool>> stomp1 = inputsSlot.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
            stomp1.VariableName.Value = CrouchVarName;
            stomp1.Reference.Target = CrouchStream;

            Slot varsSlot = MarioSlot.AddSlot("Vars");
            varsSlot.Tag = null;

            DynamicReferenceVariable<User> owner = varsSlot.AttachComponent<DynamicReferenceVariable<User>>();
            owner.VariableName.Value = MarioOwnerVarName;
            owner.Reference.Target = MarioUser;

            DynamicValueVariable<float> healthPoints = varsSlot.AttachComponent<DynamicValueVariable<float>>();
            healthPoints.VariableName.Value = HealthPointsVarName;

            DynamicValueVariable<short> healthPointsRaw = varsSlot.AttachComponent<DynamicValueVariable<short>>();
            healthPointsRaw.VariableName.Value = HealthPointsVarName;

            DynamicValueVariable<uint> actionFlags = varsSlot.AttachComponent<DynamicValueVariable<uint>>();
            actionFlags.VariableName.Value = ActionFlagsVarName;

            DynamicValueVariable<uint> stateFlags = varsSlot.AttachComponent<DynamicValueVariable<uint>>();
            stateFlags.VariableName.Value = StateFlagsVarName;

            DynamicValueVariable<int> coinCounter = varsSlot.AttachComponent<DynamicValueVariable<int>>();
            coinCounter.VariableName.Value = CoinsVarName;

            DynamicValueVariable<int> redCoinCounter = varsSlot.AttachComponent<DynamicValueVariable<int>>();
            redCoinCounter.VariableName.Value = RedCoinVarName;

            DynamicValueVariable<int> starCounter = varsSlot.AttachComponent<DynamicValueVariable<int>>();
            starCounter.VariableName.Value = StarVarName;

            DynamicValueVariable<int> liveCounter = varsSlot.AttachComponent<DynamicValueVariable<int>>();
            liveCounter.VariableName.Value = LiveVarName;
            liveCounter.Value.Value = 4;

            DynamicValueVariable<bool> isGrabbed = varsSlot.AttachComponent<DynamicValueVariable<bool>>();
            isGrabbed.VariableName.Value = IsGrabbedVarName;

            Slot animVarsSlot = varsSlot.AddSlot("Animation");

            DynamicValueVariable<short> animFrame = animVarsSlot.AttachComponent<DynamicValueVariable<short>>();
            animFrame.VariableName.Value = AnimFrameVarName;

            DynamicValueVariable<short> animId = animVarsSlot.AttachComponent<DynamicValueVariable<short>>();
            animId.VariableName.Value = AnimIDVarName;

            DynamicValueVariable<short> animFlags = animVarsSlot.AttachComponent<DynamicValueVariable<short>>();
            animFlags.VariableName.Value = AnimFlagVarName;

            DynamicValueVariable<short> startFrame = animVarsSlot.AttachComponent<DynamicValueVariable<short>>();
            startFrame.VariableName.Value = StartFrameVarName;

            DynamicValueVariable<short> loopStart = animVarsSlot.AttachComponent<DynamicValueVariable<short>>();
            loopStart.VariableName.Value = LoopStartVarName;

            DynamicValueVariable<short> loopEnd = animVarsSlot.AttachComponent<DynamicValueVariable<short>>();
            loopEnd.VariableName.Value = LoopEndVarName;

            _marioAlphaVar = varsSlot.AttachComponent<DynamicValueVariable<float>>();
            _marioAlphaVar.VariableName.Value = MarioAlphaVarName;
            _marioAlphaVar.Value.Value = 1f;

            slot.RunInUpdates(2, () =>
            {
                slot.SetParent(instance.MyMariosSlot);
                slot.GlobalScale = float3.One;
            });
        }

        Context.UpdatePlayerMariosState();

        _initialized = true;

        slot.RunInUpdates(3, () => SyncedIsShown = !_wasBypassed);
    }

    private void CreateMarioRenderer()
    {
        _states[0] = new SM64MarioState();
        _states[1] = new SM64MarioState();

        const int bufferSize = 3 * SM64Interop.SM64GeoMaxTriangles;
        _lerpPositionBuffer = new float3[bufferSize];
        _lerpNormalBuffer = new float3[bufferSize];
        _positionBuffers = new[] { new float3[bufferSize], new float3[bufferSize] };
        _normalBuffers = new[] { new float3[bufferSize], new float3[bufferSize] };
        _colorBuffer = new float3[bufferSize];
        _colorBufferColors = new color[bufferSize];
        _uvBuffer = new float2[bufferSize];

        if (!Config.RenderSlotPublic.Value)
        {
            _marioRendererSlot = MarioSlot.World.AddLocalSlot($"{MarioSlot.Name} Renderer - {MarioSlot.LocalUser.UserName}");
        }
        else
        {
            _marioRendererSlot = MarioSlot.World.AddSlot($"{MarioSlot.Name} Renderer - {MarioSlot.LocalUser.UserName}", false);
        }

        _marioMeshRenderer = _marioRendererSlot.AttachComponent<MeshRenderer>();
        _marioMeshProvider = _marioRendererSlot.AttachComponent<LocalMeshProvider>();
        _marioMaterial = _marioRendererSlot.AttachComponent<PBS_DualSidedMetallic>();
        _marioMaterialClipped = _marioRendererSlot.AttachComponent<PBS_VertexColorMetallic>();
        _marioMaterialMetal = _marioRendererSlot.AttachComponent<XiexeToonMaterial>();
        _marioMaterialVanish = _marioRendererSlot.AttachComponent<PBS_Metallic>();

        StaticTexture2D marioTextureClipped = _marioRendererSlot.AttachComponent<StaticTexture2D>();
        marioTextureClipped.DirectLoad.Value = true;
        marioTextureClipped.URL.Value = new Uri("resdb:///52c6ac7b3c623bc46b380a6655c0bd20988b4937918b428093ec04e8240316ba.png");
        marioTextureClipped.WrapModeU.Value = TextureWrapMode.Clamp;
        marioTextureClipped.WrapModeV.Value = TextureWrapMode.Clamp;
        _marioMaterialClipped.AlbedoTexture.Target = marioTextureClipped;
        _marioMaterialClipped.AlphaHandling.Value = FrooxEngine.AlphaHandling.AlphaClip;
        _marioMaterialClipped.AlphaClip.Value = 0.25f;
        _marioMaterialClipped.Culling.Value = Culling.Off;

        StaticTexture2D marioTexture = _marioRendererSlot.AttachComponent<StaticTexture2D>();
        marioTexture.DirectLoad.Value = true;
        marioTexture.URL.Value = new Uri("resdb:///f05ee58da859926aa5652bb92a07ad0d5ce5fb33979fd7ead9bc5ed78eb5b7d7.webp");
        marioTexture.WrapModeU.Value = TextureWrapMode.Clamp;
        marioTexture.WrapModeV.Value = TextureWrapMode.Clamp;

        _marioMaterial.AlbedoTexture.Target = marioTexture;
        _marioMaterial.AlphaHandling.Value = FrooxEngine.AlphaHandling.AlphaClip;
        _marioMaterial.AlphaClip.Value = 1f;
        _marioMaterial.Culling.Value = Culling.Off;
        _marioMaterial.OffsetUnits.Value = -1f;

        _marioMaterialVanish.AlbedoTexture.Target = marioTexture;
        _marioMaterialVanish.AlbedoColor.Value = Utils.VanishCapColor;
        _marioMaterialVanish.BlendMode.Value = BlendMode.Alpha;
        _marioMaterialVanish.AlphaCutoff.Value = 1f;
        _marioMaterialVanish.OffsetUnits.Value = -1f;

        StaticTexture2D marioTextureMetal = _marioRendererSlot.AttachComponent<StaticTexture2D>();
        marioTextureMetal.DirectLoad.Value = true;
        marioTextureMetal.URL.Value = new Uri("resdb:///648a620d521fdf0c2cfca1d89198155136dbe22051f7e0c64d8787bb7849a8a5.webp");
        marioTextureMetal.WrapModeU.Value = TextureWrapMode.Clamp;
        marioTextureMetal.WrapModeV.Value = TextureWrapMode.Clamp;

        _marioMaterialMetal.Matcap.Target = marioTextureMetal;
        _marioMaterialMetal.Color.Value = colorX.Black;
        _marioMaterialMetal.MatcapTint.Value = colorX.White * 1.5f;
        _marioMaterialMetal.OffsetUnits.Value = -2f;

        _marioMeshRenderer.Materials.Add();
        _marioMeshRenderer.Materials.Add(_marioMaterial);

        _marioMeshRenderer.Mesh.Target = _marioMeshProvider;
        _marioMesh = new MeshX();

        _marioRendererSlot.LocalScale = new float3(-1, 1, 1) / SM64Interop.ScaleFactor;
        _marioRendererSlot.LocalPosition = float3.Zero;

        _marioMesh.AddVertices(_lerpPositionBuffer.Length);
        TriangleSubmesh marioTris = _marioMesh.AddSubmesh<TriangleSubmesh>();
        for (int i = 0; i < SM64Interop.SM64GeoMaxTriangles; i++)
        {
            int idx = i * 3;
            marioTris.AddTriangle(idx, idx + 1, idx + 2);
        }

        _marioMeshProvider.Mesh = _marioMesh;
        _marioMeshProvider.LocalManualUpdate = true;
        _marioMeshProvider.HighPriorityIntegration.Value = true;

        _enabled = true;
    }

    private void CreateNonModdedRenderer()
    {
        Uri uri = Config.MarioUrl.Value ?? new Uri("resdb:///3ac6f2e37deb52573a3dbd4630f6e20eff9e8eb6db5d1f0c9dd4dfd84e99c107.brson");

        _marioNonModdedRendererSlot = MarioSlot.Children.FirstOrDefault(x => x.Tag == MarioNonMRendererTag);
        if (_marioNonModdedRendererSlot == null && IsLocal)
        {
            _marioNonModdedRendererSlot = MarioSlot.AddSlot("Non-Modded Renderer", false);
            _marioNonModdedRendererSlot.Tag = MarioNonMRendererTag;
            _marioNonModdedRendererSlot.LocalScale *= MarioScale;

            Slot tempSlot = _marioNonModdedRendererSlot.AddSlot("TempSlot", false);
            tempSlot.StartTask(async () =>
            {
                await tempSlot.LoadObjectAsync(uri);
                tempSlot.GetComponent<InventoryItem>()?.Unpack(true);

                foreach (Slot child in _marioNonModdedRendererSlot.Children)
                    child.SetIdentityTransform();
            });
        }
    }

    // Game Tick
    internal void ContextFixedUpdateSynced()
    {
        if (!_enabled || !_initialized || _isNuked || IsDisposed) return;

        UpdateIsOverMaxDistance();

        if (_wasBypassed) return;

        SM64MarioInputs inputs = new SM64MarioInputs
        {
            camLookX = -GetCameraLookDirection().x,
            camLookZ = GetCameraLookDirection().z
        };

        if (IsLocal)
        {
            bool isControlled = IsControlled;

            // Send Data to the streams
            Joystick = isControlled ? GetJoystickAxes() : float2.Zero;
            Jump = isControlled && GetButtonHeld(Button.Jump);
            Punch = isControlled && GetButtonHeld(Button.Kick);
            Crouch = isControlled && GetButtonHeld(Button.Stomp);
        }

        inputs.stickX = Joystick.x;
        inputs.stickY = -Joystick.y;
        inputs.buttonA = (byte)(Jump ? 1 : 0);
        inputs.buttonB = (byte)(Punch ? 1 : 0);
        inputs.buttonZ = (byte)(Crouch ? 1 : 0);

        _states[_buffIndex] = SM64Interop.MarioTick(MarioId, inputs, _positionBuffers[_buffIndex], _normalBuffers[_buffIndex], _colorBuffer, _uvBuffer, out _numTrianglesUsed);

        // If the tris count changes, reset the buffers
        if (_previousNumTrianglesUsed != _numTrianglesUsed)
        {
            for (int i = _numTrianglesUsed * 3; i < _positionBuffers[_buffIndex].Length; i++)
            {
                _positionBuffers[_buffIndex][i] = float3.Zero;
                _normalBuffers[_buffIndex][i] = float3.Zero;
            }

            _positionBuffers[_buffIndex].CopyTo(_positionBuffers[1 - _buffIndex], 0);
            _normalBuffers[_buffIndex].CopyTo(_normalBuffers[1 - _buffIndex], 0);
            _positionBuffers[_buffIndex].CopyTo(_lerpPositionBuffer, 0);
            _normalBuffers[_buffIndex].CopyTo(_lerpNormalBuffer, 0);

            _previousNumTrianglesUsed = _numTrianglesUsed;
        }

        _buffIndex = 1 - _buffIndex;

        if (IsLocal)
        {
            SyncedStateFlags = CurrentStateFlags;
            SyncedActionFlags = CurrentActionFlags;
            SM64AnimInfo info = CurrentState.AnimInfo;
            SM64Animation anim = info.CurrentAnim;
            SyncedAnimID = info.AnimID;
            SyncedAnimFrame = info.AnimFrame;
            SyncedAnimFlags = anim.Flags;
            SyncedStartFrame = anim.StartFrame;
            SyncedLoopStart = anim.LoopStart;
            SyncedLoopEnd = anim.LoopEnd;
            
            if (_marioGrabbable is { IsRemoved: false })
            {
                SyncedIsGrabbed = _marioGrabbable.IsGrabbed;
            }

            foreach (SM64Interactable interactable in Context.Interactables.Values.GetTempList())
            {
                interactable.Handle(this);
            }

            foreach (SM64Teleporter teleporter in Context.Teleporters.Values.GetTempList())
            {
                teleporter.Handle(this);
            }

            foreach (var thing in Context.FakeObjects.Values.GetTempList())
            {
                thing.ContextFixedUpdate();
            }
            
            // Check for deaths, so we delete mario
            bool isQuickSandDeath = (SyncedActionFlags & ActionFlag.QuicksandDeath) == ActionFlag.QuicksandDeath;
            bool isDeathPlaneDeath = false;

            if (!isQuickSandDeath)
            {
                float floorHeight = SM64Interop.FindFloor(MarioSlot.GlobalPosition, out SM64SurfaceCollisionData? floorData);
                if (floorData is { } floor)
                {
                    if (floor.type == SurfaceType.DeathPlane || floor.type == SurfaceType.VerticalWind)
                    {
                        isDeathPlaneDeath = MarioSlot.GlobalPosition.Y < floorHeight + 3072f.FromMarioFloat();
                    }
                }
            }

            if (!_isDying && (isQuickSandDeath || isDeathPlaneDeath))
            {
                SetHealthPoints(0);
            }

            if (!_isDying && CurrentState.IsDead)
            {
                _isDying = true;
                MarioSlot.RunSynchronously(() => _marioGrabbable.Enabled = false, true);

                float laughDelay = isQuickSandDeath ? 0.8f : isDeathPlaneDeath ? 0.2f : 2.5f;
                MarioSlot.RunInSeconds(laughDelay, () => SM64Interop.PlaySoundGlobal(Sounds.Menu_BowserLaugh));

                if (isDeathPlaneDeath || isQuickSandDeath)
                {
                    float posDelay = isDeathPlaneDeath ? 1f : 2.2f;
                    MarioSlot.RunInSeconds(posDelay, () =>
                    {
                        float3 pos = MarioSlot.GlobalPosition;
                        SetPosition(new float3(pos.X - 10000, pos.Y - 10000, pos.Z - 10000));
                    });
                }

                MarioSlot.RunInSeconds(6.5f, () => SetMarioAsNuked());
            }
        }
        else
        {
            // This seems to be kinda broken, maybe revisit syncing the WHOLE state instead
            UpdateFlagsIfChanged();

            // Trigger the cap if the synced values have cap (if we already have the cap, it will ignore)
            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.VanishCap))
            {
                WearCap(MarioCapType.VanishCap, 15f, false);
            }

            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.MetalCap))
            {
                WearCap(MarioCapType.MetalCap, 15f, false);
            }

            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.WingCap))
            {
                WearCap(MarioCapType.WingCap, 40f, false);
            }

            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.NormalCap))
            {
                WearCap(MarioCapType.NormalCap, 15f, false);
            }

            if (Utils.IsTeleporting(SyncedStateFlags) /* && !Utils.IsTeleporting(CurrentStateFlags)*/ && !IsTeleporting)
            {
                TeleportTo(float3.Zero);
            }
        }

        // Grabbable
        if (_marioGrabbable is { IsRemoved: false })
        {
            bool pickup = IsBeingGrabbed;

            if (_wasPickedUp != pickup)
            {
                if (_wasPickedUp)
                {
                    Throw();
                }
                else
                {
                    Hold();
                }
            }

            _wasPickedUp = pickup;
        }

        // Water Level
        float waterSurface = float.NaN;
        float3 marioPos = _marioCollider.GlobalBoundingBox.Center;

        foreach (SM64WaterBox waterBox in Context.WaterBoxes.Values.GetTempList())
        {
            waterSurface = waterBox.Handle(marioPos);
        }

        float newWaterLevel = Context.ContextVariableSpace.TryReadValue(WaterVarName, out float fallbackLevel) ? fallbackLevel : -100f;

        if (waterSurface.IsValid())
        {
            newWaterLevel = MathX.Min(marioPos.y + 0.5f, waterSurface);
        }

        if (!MathX.Approximately(_waterLevel, newWaterLevel))
        {
            _waterLevel = newWaterLevel;
            SM64Interop.SetWaterLevel(MarioId, _waterLevel);
        }

        // Materials
        if (Utils.HasCapType(CurrentStateFlags, MarioCapType.MetalCap))
        {
            if (CurrentMaterial != _marioMaterialMetal)
            {
                CurrentMaterial = _marioMaterialMetal;
            }
        }
        else if (Utils.HasCapType(CurrentStateFlags, MarioCapType.VanishCap))
        {
            if (_marioMaterialClipped.AlbedoColor.Value != Utils.VanishCapColor)
            {
                _marioMaterialClipped.AlbedoColor.Value = Utils.VanishCapColor;
            }

            if (_marioMaterialClipped.RenderQueue.Value != 1)
            {
                _marioMaterialClipped.RenderQueue.Value = 1;
            }

            if (_marioMaterialClipped.AlphaHandling.Value != FrooxEngine.AlphaHandling.AlphaBlend)
            {
                _marioMaterialClipped.AlphaHandling.Value = FrooxEngine.AlphaHandling.AlphaBlend;
            }

            if (CurrentFaceMaterial != _marioMaterialVanish)
            {
                CurrentFaceMaterial = _marioMaterialVanish;
            }
        }
        else if (!IsTeleporting)
        {
            if (Math.Abs(_marioAlphaVar.Value.Value - 1f) > 0.001f)
            {
                _marioAlphaVar.Value.Value = 1f;
            }

            if (_marioMaterialClipped.AlbedoColor.Value != colorX.White)
            {
                _marioMaterialClipped.AlbedoColor.Value = colorX.White;
            }

            if (_marioMaterialClipped.RenderQueue.Value != -1)
            {
                _marioMaterialClipped.RenderQueue.Value = -1;
            }

            if (_marioMaterialClipped.AlphaHandling.Value != FrooxEngine.AlphaHandling.AlphaClip)
            {
                _marioMaterialClipped.AlphaHandling.Value = FrooxEngine.AlphaHandling.AlphaClip;
            }

            if (CurrentMaterial != _marioMaterialClipped)
            {
                CurrentMaterial = _marioMaterialClipped;
            }

            if (CurrentFaceMaterial != _marioMaterial)
            {
                CurrentFaceMaterial = _marioMaterial;
            }

            if (_marioMaterialVanish.AlbedoColor.Value != Utils.VanishCapColor)
            {
                _marioMaterialVanish.AlbedoColor.Value = Utils.VanishCapColor;
            }

            if (_marioMaterialClipped.Smoothness.Value == 0f)
            {
                _marioMaterialClipped.Smoothness.Value = 0.25f;
            }
        }

        // Just for now until Collider Shenanigans is implemented
        SM64Mario attackingMario = Context.AllMarios.Values.GetTempList().FirstOrDefault(mario => mario != this && mario.CurrentState.IsAttacking && MathX.Distance(mario.MarioSlot.GlobalPosition, MarioSlot.GlobalPosition) <= 0.1f * MarioScale);
        if (attackingMario != null)
        {
            TakeDamage(attackingMario.MarioSlot.GlobalPosition, 1);
        }

        // Mario Mesh Colors
        for (int i = 0; i < _colorBuffer.Length; ++i)
        {
            _colorBufferColors[i] = new color(_colorBuffer[i].x, _colorBuffer[i].y, _colorBuffer[i].z);
        }

        if (_marioMesh != null)
        {
            for (int i = 0; i < _marioMesh.VertexCount; i++)
            {
                _marioMesh.SetColor(i, _colorBufferColors[i]);
                _marioMesh.SetUV(i, 0, _uvBuffer[i]);
            }
        }
    }

    // Engine Tick
    internal void ContextUpdateSynced()
    {
        if (!_enabled || !_initialized || _isNuked || IsDisposed) return;

        // lerp from previous state to current (this means when you make an input it's delayed by one frame, but it means we can have nice interpolation)
        float t = (float)((MarioSlot.Time.WorldTime - Context.LastTick) / (Config.GameTickMs.Value / 1000f));

        int j = 1 - _buffIndex;

        for (int i = 0; i < _numTrianglesUsed * 3; ++i)
        {
            _lerpPositionBuffer[i] = MathX.LerpUnclamped(_positionBuffers[_buffIndex][i], _positionBuffers[j][i], t);
            _lerpNormalBuffer[i] = MathX.LerpUnclamped(_normalBuffers[_buffIndex][i], _normalBuffers[j][i], t);
        }

        // Handle the position and rotation
        if (IsLocal && !IsBeingGrabbed)
        {
            MarioSlot.GlobalPosition = MathX.LerpUnclamped(_states[_buffIndex].ScaledPosition, _states[j].ScaledPosition, t);
            MarioSlot.GlobalRotation = MathX.LerpUnclamped(_states[_buffIndex].ScaledRotation, _states[j].ScaledRotation, t);
        }
        else
        {
            SetPosition(MarioSlot.GlobalPosition);
            SetFaceAngle(MarioSlot.GlobalRotation);
        }

        if (IsLocal)
        {
            SyncedHealthPoints = CurrentState.HealthPoints;
            SyncedHealthPointsRaw = CurrentState.Health;
        }
        else
        {
            SetHealthPoints(SyncedHealthPoints);
        }

        if (_marioMesh != null)
        {
            for (int i = 0; i < _marioMesh.VertexCount; i++)
            {
                _marioMesh.SetVertex(i, _lerpPositionBuffer[i]);
                _marioMesh.SetNormal(i, _lerpNormalBuffer[i]);
            }

            _marioMeshProvider.Mesh = _marioMesh;
            _marioMeshProvider.Update();
        }
    }

    private void UpdateFlagsIfChanged()
    {
        ActionFlag currentActionFlags = SyncedActionFlags;
        // StateFlag currentStateFlags = SyncedStateFlags;

        // if (currentStateFlags != _lastStateFlags)
        // {
        //     _lastStateFlags = currentStateFlags;
        //     if (currentStateFlags != 0) SetState(currentStateFlags);
        // }

        if (currentActionFlags != _lastActionFlags)
        {
            _lastActionFlags = currentActionFlags;
            if (currentActionFlags != 0) SetAction(currentActionFlags);
        }
    }

    private float3 GetCameraLookDirection()
    {
        floatQ rot = MarioUser?.Root?.ViewRotation ?? floatQ.Identity;
        // add new camerapos here
        // if (something)
        // {
        //      rot = newCameraRotation;
        // }
        return (rot * float3.Forward).SetY(0).Normalized;
    }

    private float2 GetJoystickAxes() => Context?.Joystick ?? float2.Zero;

    private bool GetButtonHeld(Button button)
    {
        if (Context == null) return false;

        return button switch
        {
            Button.Jump  => Context.Jump,
            Button.Kick  => Context.Kick,
            Button.Stomp => Context.Stomp,
            _            => false
        };
    }

    public void SetPosition(float3 pos) => SM64Interop.MarioSetPosition(MarioId, pos);

    public void SetRotation(floatQ rot) => SM64Interop.MarioSetRotation(MarioId, rot);

    public void SetFaceAngle(floatQ rot) => SM64Interop.MarioSetFaceAngle(MarioId, rot);

    public void SetHealthPoints(float healthPoints) => SM64Interop.MarioSetHealthPoints(MarioId, healthPoints);

    public void SetFullHealth() => SM64Interop.MarioSetFullHealth(MarioId);

    public void SetInvicibleTimer(float timeMs) => SM64Interop.MarioSetInvincibility(MarioId, timeMs);

    public void SetAction(ActionFlag actionFlag) => SM64Interop.MarioSetAction(MarioId, actionFlag);

    public void SetAction(uint actionFlags) => SM64Interop.MarioSetAction(MarioId, actionFlags);

    public void SetState(StateFlag stateFlag) => SM64Interop.MarioSetState(MarioId, stateFlag);

    public void SetState(uint stateFlags) => SM64Interop.MarioSetState(MarioId, stateFlags);

    public void SetVelocity(float3 frooxVelocity) => SM64Interop.MarioSetVelocity(MarioId, frooxVelocity);

    public void SetForwardVelocity(float frooxVelocity) => SM64Interop.MarioSetForwardVelocity(MarioId, frooxVelocity);

    public void Heal(byte healthPoints)
    {
        if (CurrentState.IsDead || !IsLocal) return;

        SM64Interop.MarioHeal(MarioId, healthPoints);
    }

    public void TakeDamage(float3 worldPosition, uint damage)
    {
        if (CurrentState.IsDead || !IsLocal) return;

        SM64Interop.MarioTakeDamage(MarioId, worldPosition, damage, (uint)0);
    }

    public void WearCap(MarioCapType capType, float duration = 15f, bool playMusic = true)
    {
        if (Utils.HasCapType(CurrentStateFlags, capType)) return;

        playMusic &= Config.PlayCapMusic.Value;

        switch (capType)
        {
            case MarioCapType.VanishCap:
            case MarioCapType.MetalCap:
            case MarioCapType.WingCap:
            case MarioCapType.NormalCap:
                // Prevent Vanish and Wing from being active at the same time - This prevents a crash
                // TODO: Look into why this actually happens....
                if (capType == MarioCapType.VanishCap && Utils.HasCapType(SyncedStateFlags, MarioCapType.WingCap) || capType == MarioCapType.WingCap && Utils.HasCapType(SyncedStateFlags, MarioCapType.VanishCap))
                {
                    break;
                }

                SM64Interop.MarioCap(MarioId, (uint)capType, duration, playMusic);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(capType), capType, null);
        }
    }

    private void Hold()
    {
        if (CurrentState.IsDead) return;

        SetAction(ActionFlag.Idle);

        SetAction(ActionFlag.Grabbed);
    }

    private void Throw()
    {
        if (CurrentState.IsDead) return;

        float3 throwVelocityFlat = CurrentState.ScaledPosition - PreviousState.ScaledPosition;
        if (throwVelocityFlat.Magnitude > 0.01f)
        {
            if (IsLocal) SetFaceAngle(floatQ.LookRotation(throwVelocityFlat));
            bool hasWingCap = Utils.HasCapType(SyncedStateFlags, MarioCapType.WingCap);
            SetAction(hasWingCap ? ActionFlag.Flying : ActionFlag.ThrownForward);
            if (IsLocal)
            {
                SetVelocity(throwVelocityFlat);
                SetForwardVelocity(throwVelocityFlat.Magnitude);
            }
        }
        else
        {
            if (IsLocal)
            {
                SetFaceAngle(floatQ.LookRotation(MarioSlot.LocalRotation * float3.Forward));
                SetVelocity(float3.Zero);
                SetForwardVelocity(0f);
            }

            SetAction(ActionFlag.Freefall);
        }
    }

    public void TeleportTo(float3 position)
    {
        TeleportStart();
        MarioSlot.RunInSeconds(1.5f, () =>
        {
            if (IsLocal) SetPosition(position);

            MarioSlot.RunInSeconds(0.5f, TeleportEnd);
        });
    }

    private void TeleportStart()
    {
        if (CurrentState.IsDead) return;
        IsTeleporting = true;
        SetAction(ActionFlag.TeleportFadeOut);

        if (_marioMaterialClipped.AlbedoColor.Value != Utils.VanishCapColor)
        {
            _marioMaterialClipped.AlbedoColor.Value = Utils.VanishCapColor;
        }

        if (_marioMaterialClipped.AlphaHandling.Value != FrooxEngine.AlphaHandling.AlphaBlend)
        {
            _marioMaterialClipped.AlphaHandling.Value = FrooxEngine.AlphaHandling.AlphaBlend;
        }

        if (Math.Abs(_marioMaterialClipped.Smoothness.Value - 0.25f) < 0.001f)
        {
            _marioMaterialClipped.Smoothness.Value = 0f;
        }

        if (CurrentFaceMaterial != _marioMaterialVanish)
        {
            CurrentFaceMaterial = _marioMaterialVanish;
        }

        MarioSlot.RunInSeconds(1f, () => _marioMeshRenderer.Enabled = false);
        _marioAlphaVar.Value.TweenTo(0f, 1f);
        _marioMaterialClipped.AlbedoColor.TweenTo(new colorX(1f, 1f, 1f, 0f), 1f);
        _marioMaterialVanish.AlbedoColor.TweenTo(new colorX(1f, 1f, 1f, 0f), 1f);
    }

    private void TeleportEnd()
    {
        if (CurrentState.IsDead) return;
        SetAction(ActionFlag.TeleportFadeIn);

        _marioMeshRenderer.Enabled = true;
        _marioAlphaVar.Value.TweenTo(1f, 1f);
        _marioMaterialClipped.AlbedoColor.TweenTo(colorX.White, 1f);
        _marioMaterialVanish.AlbedoColor.TweenTo(colorX.White, 1f);
        MarioSlot.RunInSeconds(1f, () => IsTeleporting = false);
    }

    public bool IsInCollider(ISM64Object obj)
    {
        if (obj?.Collider == null) return false;

        Collider col = obj.Collider;
        BoundingBox colliderBox = col.LocalBoundingBox;

        float3 localMarioCenterPos = col.Slot.GlobalPointToLocal(_marioCollider.GlobalBoundingBox.Center);
        float3 localMarioFootPos = col.Slot.GlobalPointToLocal(MarioSlot.GlobalPosition);
        float3 localMarioHeadPos = localMarioCenterPos + (localMarioCenterPos - localMarioFootPos);

        bool anyPointInside = false;
        for (int i = 0; i <= _marioCollisionSampleCount; i++)
        {
            float t = i / (float)_marioCollisionSampleCount;
            float3 pointOnLine = MathX.Lerp(localMarioFootPos, localMarioHeadPos, t);
            if (!colliderBox.Contains(pointOnLine)) continue;

            anyPointInside = true;
            break;
        }
        return anyPointInside;
    }

    public void SetMarioAsNuked(bool forceDelete = false)
    {
        _isNuked = true;

        bool shouldDelete = Config.DeleteAfterDeath.Value || forceDelete;
        if (!shouldDelete)
        {
            if (Revive())
            {
                Logger.Debug("One of our Marios died, so revive the mario.");
                return;
            }
        }

        Logger.Debug("One of our Marios died, so delete the mario.");
        Dispose();
    }

    public bool Revive(bool force = false)
    {
        if (--SyncedLives < 0 && !force) return false;

        SM64Interop.FindFloor(MarioSpawn, out SM64SurfaceCollisionData? data);
        if (data == null) return false;

        SetInvicibleTimer(30);
        SetFullHealth();

        SetPosition(MarioSpawn);
        SetVelocity(float3.Zero);
        SetForwardVelocity(0f);

        _isNuked = false;
        _isDying = false;

        SM64Interop.StopCapMusic();

        SetAction(ActionFlag.SpawnSpinAirborne);
        SetState(StateFlag.CapOnHead | StateFlag.NormalCap);
        MarioSlot.RunSynchronously(() => _marioGrabbable.Enabled = true, true);
        return true;
    }

    public void SetIsOverMaxCount(bool isOverTheMaxCount)
    {
        _isOverMaxCount = isOverTheMaxCount;
        UpdateIsBypassed();
    }

    private void UpdateIsOverMaxDistance()
    {
        if (MarioSlot.LocalUser.Root == null) return;

        // Check the distance to see if we should ignore the updates
        _isOverMaxDistance = !IsLocal && MarioSlot.DistanceFromUserHead() > _skipFarMarioDistance;
        UpdateIsBypassed();
    }

    private void UpdateIsBypassed()
    {
        if (!_initialized || IsDisposed) return;

        bool isBypassed = _isOverMaxDistance || _isOverMaxCount;
        // if (isBypassed == _wasBypassed) return;
        _wasBypassed = isBypassed;

        // Enable/Disable the mario's mesh renderer
        if (!IsTeleporting) _marioMeshRenderer.Enabled = !isBypassed;
        SyncedIsShown = !isBypassed;
    }

    private void HandleSlotDestroyed(Slot slot)
    {
        if (IsDisposed) return;

        Dispose();
    }

    ~SM64Mario()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (IsDisposed) return;

        if (disposing)
        {
            SM64Context.RemoveMario(this);

            if (MarioSlot is { IsDestroyed: false })
            {
                MarioSlot.OnPrepareDestroy -= HandleSlotDestroyed;
            }

            if (_marioRendererSlot is { IsDestroyed: false })
            {
                _marioRendererSlot.SafeDestroy();
            }

            if (IsLocal && _marioNonModdedRendererSlot is { IsDestroyed: false })
            {
                _marioNonModdedRendererSlot.SafeDestroy();
            }

            if (IsLocal && MarioSlot is { IsDestroyed: false })
            {
                MarioSlot.SafeDestroy();
            }

            World = null;
            Context = null;
            MarioSlot = null;
            MarioUser = null;
            MarioSpace = null;

            _marioRendererSlot = null;
            _marioNonModdedRendererSlot = null;
            _marioMeshRenderer = null;
            _marioMesh = null;
            _marioMeshProvider = null;
            _marioMaterial = null;
            _marioMaterialClipped = null;
            _marioMaterialMetal = null;
            _marioMaterialVanish = null;

            _positionBuffers = null;
            _normalBuffers = null;
            _lerpPositionBuffer = null;
            _lerpNormalBuffer = null;
            _uvBuffer = null;
            _colorBuffer = null;
            _colorBufferColors = null;
        }

        if (SM64Interop.IsGlobalInit)
        {
            SM64Interop.MarioDelete(MarioId);
        }

        _enabled = false;
        _initialized = false;
        IsDisposed = true;
    }

    private enum Button
    {
        Jump,
        Kick,
        Stomp
    }
}