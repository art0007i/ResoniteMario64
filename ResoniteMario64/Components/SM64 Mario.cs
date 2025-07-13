using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using ResoniteMario64.Components.Context;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.Constants;
using static ResoniteMario64.libsm64.SM64Constants;
#if IsNet9
using Renderite.Shared;
#endif

namespace ResoniteMario64.Components;

public class SM64Mario : IDisposable
{
    private bool _enabled;
    private bool _isDying;
    private bool _isNuked;

    public readonly uint MarioId;

    private static float MarioScale => 1000.0f / Interop.ScaleFactor;

    public Slot MarioSlot { get; }
    public User MarioUser { get; }
    public DynamicVariableSpace MarioSpace { get; }
    public bool IsLocal => MarioUser.IsLocalUser;

#region Renderer

    // Renderer/Mesh
    private Slot _marioRendererObject;
    private MeshRenderer _marioMeshRenderer;
    private MeshX _marioMesh;
    private LocalMeshProvider _marioMeshProvider;

    // Materials
    private bool IsMatSwitching { get; set; }

    private PBS_DualSidedMetallic _marioMaterial;
    private PBS_VertexColorMetallic _marioMaterialClipped;
    private XiexeToonMaterial _marioMaterialMetal;

    private IAssetProvider<Material> CurrentMaterial
    {
        get => _marioMeshRenderer.Materials.Count > 0 ? _marioMeshRenderer.Materials[0] : null;
        set
        {
            if (IsMatSwitching) return;
            IsMatSwitching = true;
            _marioMeshRenderer.RunInUpdates(2, () =>
            {
                if (_marioMeshRenderer.Materials.Count > 0)
                {
                    _marioMeshRenderer.Materials[0] = value;
                }

                IsMatSwitching = false;
            });
        }
    }

    // GeoBuffers
    private float3[][] _positionBuffers;
    private float3[][] _normalBuffers;
    private float3[] _lerpPositionBuffer;
    private float3[] _lerpNormalBuffer;
    private float2[] _uvBuffer;
    private float3[] _colorBuffer;
    private color[] _colorBufferColors;

    // Buffer Mgmt
    private int _buffIndex;
    private ushort _numTrianglesUsed;
    private ushort _previousNumTrianglesUsed;

#endregion

    // Mario State
    public SM64MarioState CurrentState => _states[1 - _buffIndex];
    public SM64MarioState PreviousState => _states[_buffIndex];

    private SM64MarioState[] _states;
    private readonly Grabbable _marioGrabbable;
    private bool _wasPickedUp;

    public SM64Mario(Slot slot)
    {
        slot.ReferenceID.ExtractIDs(out _, out byte userByte);
        MarioUser = slot.World.GetUserByAllocationID(userByte);

        MarioSlot = slot;
        MarioSlot.DestroyWhenUserLeaves(MarioUser);
        MarioSlot.Tag = MarioTag;

        MarioSpace = MarioSlot.GetComponentOrAttach<DynamicVariableSpace>();
        MarioSpace.SpaceName.Value = MarioTag;

        _marioGrabbable = MarioSlot.GetComponentOrAttach<Grabbable>();

        CapsuleCollider coll = MarioSlot.GetComponentOrAttach<CapsuleCollider>();
        coll.Offset.Value = new float3(0, 0.075f * MarioScale);
        coll.Radius.Value = 0.05f * MarioScale;
        coll.Height.Value = 0.15f * MarioScale;

        MarioSlot.OnPrepareDestroy += _ => Dispose();
        float3 initPos = MarioSlot.GlobalPosition;
        MarioId = Interop.MarioCreate(new float3(-initPos.x, initPos.y, initPos.z) * Interop.ScaleFactor);

        CreateMarioRenderer();

        if (!IsLocal) return;

        Slot vars = MarioSlot.AddSlot("Inputs");
        vars.Tag = MarioTag;

        DynamicReferenceVariable<IValue<float2>> joystick1 = vars.AttachComponent<DynamicReferenceVariable<IValue<float2>>>();
        joystick1.VariableName.Value = JoystickTag;
        joystick1.Reference.Target = JoystickStream;

        DynamicReferenceVariable<IValue<bool>> jump1 = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        jump1.VariableName.Value = JumpTag;
        jump1.Reference.Target = JumpStream;

        DynamicReferenceVariable<IValue<bool>> kick1 = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        kick1.VariableName.Value = PunchTag;
        kick1.Reference.Target = PunchStream;

        DynamicReferenceVariable<IValue<bool>> stomp1 = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        stomp1.VariableName.Value = CrouchTag;
        stomp1.Reference.Target = CrouchStream;

        DynamicValueVariable<float> healthPoints = vars.AttachComponent<DynamicValueVariable<float>>();
        healthPoints.VariableName.Value = HealthPointsTag;

        DynamicValueVariable<uint> actionFlags = vars.AttachComponent<DynamicValueVariable<uint>>();
        actionFlags.VariableName.Value = ActionFlagsTag;

        DynamicValueVariable<uint> stateFlags = vars.AttachComponent<DynamicValueVariable<uint>>();
        stateFlags.VariableName.Value = StateFlagsTag;
    }

    // Inputs
    private float2 Joystick => MarioSpace.TryReadValue(JoystickTag, out IValue<float2> joystick) ? joystick?.Value ?? float2.Zero : float2.Zero;
    private ValueStream<float2> _joystickStream;
    private ValueStream<float2> JoystickStream
    {
        get
        {
            if (_joystickStream == null || _joystickStream.IsRemoved)
            {
                _joystickStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<float2>>(MarioSlot.LocalUser, $"SM64 {JoystickTag}", out bool created);
                if (created)
                {
                    _joystickStream.Group = "SM64";
                    _joystickStream.Encoding = ValueEncoding.Full;
                    _joystickStream.SetUpdatePeriod(2, 0);
                    _joystickStream.SetInterpolation();
                }
            }

            return _joystickStream;
        }
        set => _joystickStream = value;
    }

    private bool Jump => MarioSpace.TryReadValue(JumpTag, out IValue<bool> jump) && jump?.Value is true;
    private ValueStream<bool> _jumpStream;
    private ValueStream<bool> JumpStream
    {
        get
        {
            if (_jumpStream == null || _jumpStream.IsRemoved)
            {
                _jumpStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {JumpTag}", out bool created);
                if (created)
                {
                    _jumpStream.Group = "SM64";
                    _jumpStream.Encoding = ValueEncoding.Full;
                    _jumpStream.SetUpdatePeriod(2, 0);
                    _jumpStream.SetInterpolation();
                }
            }

            return _jumpStream;
        }
        set => _jumpStream = value;
    }

    private bool Punch => MarioSpace.TryReadValue(PunchTag, out IValue<bool> kick) && kick?.Value is true;
    private ValueStream<bool> _punchStream;
    private ValueStream<bool> PunchStream
    {
        get
        {
            if (_punchStream == null || _punchStream.IsRemoved)
            {
                _punchStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {PunchTag}", out bool created);
                if (created)
                {
                    _punchStream.Group = "SM64";
                    _punchStream.Encoding = ValueEncoding.Full;
                    _punchStream.SetUpdatePeriod(2, 0);
                    _punchStream.SetInterpolation();
                }
            }

            return _punchStream;
        }
        set => _punchStream = value;
    }

    private bool Crouch => MarioSpace.TryReadValue(CrouchTag, out IValue<bool> stomp) && stomp?.Value is true;
    private ValueStream<bool> _crouchStream;
    private ValueStream<bool> CrouchStream
    {
        get
        {
            if (_crouchStream == null || _crouchStream.IsRemoved)
            {
                _crouchStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {CrouchTag}", out bool created);
                if (created)
                {
                    _crouchStream.Group = "SM64";
                    _crouchStream.Encoding = ValueEncoding.Full;
                    _crouchStream.SetUpdatePeriod(2, 0);
                    _crouchStream.SetInterpolation();
                }
            }

            return _crouchStream;
        }
        set => _crouchStream = value;
    }

    public float SyncedHealthPoints => MarioSpace.TryReadValue(HealthPointsTag, out float healthPoints) ? healthPoints : 255;
    public uint SyncedActionFlags => MarioSpace.TryReadValue(ActionFlagsTag, out uint actionFlags) ? actionFlags : 0;
    public uint SyncedStateFlags => MarioSpace.TryReadValue(StateFlagsTag, out uint stateFlags) ? stateFlags : 0;

    public uint CurrentActionFlags => CurrentState.ActionFlags;
    public uint CurrentStateFlags => CurrentState.StateFlags;

    public bool IsBeingGrabbed => _marioGrabbable.IsGrabbed;

    private void CreateMarioRenderer()
    {
        // Initialize Buffers
        _states = new SM64MarioState[]
        {
            new SM64MarioState(),
            new SM64MarioState()
        };

        _lerpPositionBuffer = new float3[3 * Interop.SM64GeoMaxTriangles];
        _lerpNormalBuffer = new float3[3 * Interop.SM64GeoMaxTriangles];
        _positionBuffers = new[] { new float3[3 * Interop.SM64GeoMaxTriangles], new float3[3 * Interop.SM64GeoMaxTriangles] };
        _normalBuffers = new[] { new float3[3 * Interop.SM64GeoMaxTriangles], new float3[3 * Interop.SM64GeoMaxTriangles] };
        _colorBuffer = new float3[3 * Interop.SM64GeoMaxTriangles];
        _colorBufferColors = new color[3 * Interop.SM64GeoMaxTriangles];
        _uvBuffer = new float2[3 * Interop.SM64GeoMaxTriangles];

        // Create Mario Slot
        _marioRendererObject = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyRenderSlotLocal)
                ? MarioSlot.World.AddLocalSlot("MarioRenderer")
                : MarioSlot.World.AddSlot("MarioRenderer", false);

        _marioMeshRenderer = _marioRendererObject.AttachComponent<MeshRenderer>();
        _marioMeshProvider = _marioRendererObject.AttachComponent<LocalMeshProvider>();

        _marioMaterial = _marioRendererObject.AttachComponent<PBS_DualSidedMetallic>();
        _marioMaterialClipped = _marioRendererObject.AttachComponent<PBS_VertexColorMetallic>();
        _marioMaterialMetal = _marioRendererObject.AttachComponent<XiexeToonMaterial>();

        // I generated this texture inside Interop.cs (look for 'mario.png')
        // then uploaded it and saved it to my inventory. I think it's better this way, because it gets cached
        StaticTexture2D marioTextureClipped = _marioRendererObject.AttachComponent<StaticTexture2D>();
        marioTextureClipped.DirectLoad.Value = true;
        marioTextureClipped.URL.Value = new Uri("resdb:///52c6ac7b3c623bc46b380a6655c0bd20988b4937918b428093ec04e8240316ba.png");
        marioTextureClipped.WrapModeU.Value = TextureWrapMode.Clamp;
        marioTextureClipped.WrapModeV.Value = TextureWrapMode.Clamp;
        _marioMaterialClipped.AlbedoTexture.Target = marioTextureClipped;
        _marioMaterialClipped.AlphaHandling.Value = FrooxEngine.AlphaHandling.AlphaClip;
        _marioMaterialClipped.AlphaClip.Value = 0.25f;
        _marioMaterialClipped.Culling.Value = Culling.Off;

        StaticTexture2D marioTexture = _marioRendererObject.AttachComponent<StaticTexture2D>();
        marioTexture.DirectLoad.Value = true;
        marioTexture.URL.Value = new Uri("resdb:///f05ee58da859926aa5652bb92a07ad0d5ce5fb33979fd7ead9bc5ed78eb5b7d7.webp");
        marioTexture.WrapModeU.Value = TextureWrapMode.Clamp;
        marioTexture.WrapModeV.Value = TextureWrapMode.Clamp;
        _marioMaterial.AlbedoTexture.Target = marioTexture;
        _marioMaterial.AlphaHandling.Value = FrooxEngine.AlphaHandling.AlphaClip;
        _marioMaterial.AlphaClip.Value = 1f;
        _marioMaterial.Culling.Value = Culling.Off;

        StaticTexture2D marioTextureMetal = _marioRendererObject.AttachComponent<StaticTexture2D>();
        marioTextureMetal.DirectLoad.Value = true;
        marioTextureMetal.URL.Value = new Uri("resdb:///648a620d521fdf0c2cfca1d89198155136dbe22051f7e0c64d8787bb7849a8a5.webp");
        marioTextureMetal.WrapModeU.Value = TextureWrapMode.Clamp;
        marioTextureMetal.WrapModeV.Value = TextureWrapMode.Clamp;
        _marioMaterialMetal.Matcap.Target = marioTextureMetal;
        _marioMaterialMetal.Color.Value = colorX.Black;
        _marioMaterialMetal.MatcapTint.Value = colorX.White * 1.5f;
        _marioMaterialMetal.OffsetUnits.Value = -1f;

        _marioMeshRenderer.Materials.Add();
        _marioMeshRenderer.Materials.Add(_marioMaterial);

        _marioMeshRenderer.Mesh.Target = _marioMeshProvider;
        _marioMesh = new MeshX();

        _marioRendererObject.LocalScale = new float3(-1, 1, 1) / Interop.ScaleFactor;
        _marioRendererObject.LocalPosition = float3.Zero;

        _marioMesh.AddVertices(_lerpPositionBuffer.Length);
        TriangleSubmesh marioTris = _marioMesh.AddSubmesh<TriangleSubmesh>();
        for (int i = 0; i < Interop.SM64GeoMaxTriangles; i++)
        {
            marioTris.AddTriangle(i * 3, i * 3 + 1, i * 3 + 2);
        }

        _marioMeshProvider.LocalManualUpdate = true;
        _marioMeshProvider.HighPriorityIntegration.Value = true;

        _enabled = true;
    }

    // Game Tick
    public void ContextFixedUpdateSynced()
    {
        if (!_enabled || _isNuked) return;

        SM64MarioInputs inputs = new SM64MarioInputs();
        float3 look = GetCameraLookDirection();
        look = look.SetY(0).Normalized;

        inputs.camLookX = -look.x;
        inputs.camLookZ = look.z;

        if (IsLocal)
        {
            // Send Data to the streams
            JoystickStream.Value = GetJoystickAxes();
            JumpStream.Value = GetButtonHeld(Button.Jump);
            PunchStream.Value = GetButtonHeld(Button.Kick);
            CrouchStream.Value = GetButtonHeld(Button.Stomp);
        }

        inputs.stickX = Joystick.x;
        inputs.stickY = -Joystick.y;
        inputs.buttonA = (byte)(Jump ? 1 : 0);
        inputs.buttonB = (byte)(Punch ? 1 : 0);
        inputs.buttonZ = (byte)(Crouch ? 1 : 0);

        _states[_buffIndex] = Interop.MarioTick(MarioId, inputs, _positionBuffers[_buffIndex], _normalBuffers[_buffIndex], _colorBuffer, _uvBuffer, out _numTrianglesUsed);

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
            MarioSpace.TryWriteValue(ActionFlagsTag, CurrentActionFlags);
            MarioSpace.TryWriteValue(StateFlagsTag, CurrentStateFlags);

            if (_marioGrabbable != null)
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

            // Check for deaths, so we delete the prop
            float floorHeight = Interop.FindFloor(MarioSlot.GlobalPosition, out SM64SurfaceCollisionData data);
            bool isDeathPlane = (data.type & (short)SM64SurfaceType.DeathPlane) == (short)SM64SurfaceType.DeathPlane;
            if (!_isDying && CurrentState.IsDead || !_isDying && MathX.Distance(floorHeight, MarioSlot.GlobalPosition.Y) < 50 && isDeathPlane) /* Find a better value for the distance check */
            {
                _isDying = true;
                bool isQuickSand = (SyncedActionFlags & (uint)ActionFlag.QuicksandDeath) == (uint)ActionFlag.QuicksandDeath;
                MarioSlot.RunInSeconds(isQuickSand ? 1f : isDeathPlane ? 0.5f : 3f, () => Interop.PlaySound(Sounds.Menu_BowserLaugh, MarioSlot.GlobalPosition));
                MarioSlot.RunInSeconds(isQuickSand ? 3f : isDeathPlane ? 3f : 15f, () => SetMarioAsNuked(true));
            }
        }
        else
        {
            // This seems to be kinda broken, maybe revisit syncing the WHOLE state instead
            // if (currentStateFlags != syncedFlags) SetState(syncedAction);
            // if (currentStateAction != syncedAction) SetAction(syncedAction);

            // Trigger the cap if the synced values have cap (if we already have the cape it will ignore)
            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.VanishCap))
            {
                WearCap(MarioCapType.VanishCap);
            }

            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.MetalCap))
            {
                WearCap(MarioCapType.MetalCap);
            }

            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.WingCap))
            {
                WearCap(MarioCapType.WingCap, 40f);
            }

            // Trigger teleport for remotes
            // if (Utils.IsTeleporting(SyncedStateFlags) && Time.time > _startedTeleporting + 5 * CVRSM64Teleporter.TeleportDuration)
            // {
            //     _startedTeleporting = Time.time;
            // }
        }

        // Just for now until Collider Shenanigans is implemented
        Dictionary<RefID, SM64Mario> marios = SM64Context.Instance.Marios.GetTempDictionary();
        SM64Mario attackingMario = marios.Values.FirstOrDefault(mario => mario != this && mario.CurrentState.IsAttacking && MathX.Distance(mario.MarioSlot.GlobalPosition, this.MarioSlot.GlobalPosition) <= 0.1f * MarioScale);
        if (attackingMario != null)
        {
            TakeDamage(attackingMario.MarioSlot.GlobalPosition, 1);
        }

        if (CurrentState.IsWearingCap(MarioCapType.MetalCap))
        {
            if (CurrentMaterial != _marioMaterialMetal)
            {
                CurrentMaterial = _marioMaterialMetal;
            }
        }
        else
        {
            if (CurrentMaterial != _marioMaterialClipped)
            {
                CurrentMaterial = _marioMaterialClipped;
            }
        }

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
    public void ContextUpdateSynced()
    {
        if (!_enabled || _isNuked) return;

        // lerp from previous state to current (this means when you make an input it's delayed by one frame, but it means we can have nice interpolation)
        float t = (float)((MarioSlot.Time.WorldTime - SM64Context.Instance.LastTick) / (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyGameTickMs) / 1000f));

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
            MarioSlot.WriteDynamicVariable(HealthPointsTag, CurrentState.HealthPoints);
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

    private float3 GetCameraLookDirection() => (MarioUser?.Root?.ViewRotation ?? floatQ.Identity) * float3.Forward;

    private static float2 GetJoystickAxes() => SM64Context.Instance == null ? float2.Zero : SM64Context.Instance.Joystick;

    private static bool GetButtonHeld(Button button)
    {
        if (SM64Context.Instance == null) return false;

        return button switch
        {
            Button.Jump  => SM64Context.Instance.Jump,
            Button.Kick  => SM64Context.Instance.Kick,
            Button.Stomp => SM64Context.Instance.Stomp,
            _            => false
        };
    }

    public void SetPosition(float3 pos) => Interop.MarioSetPosition(MarioId, pos);

    public void SetRotation(floatQ rot) => Interop.MarioSetRotation(MarioId, rot);

    public void SetFaceAngle(floatQ rot) => Interop.MarioSetFaceAngle(MarioId, rot);

    public void SetHealthPoints(float healthPoints) => Interop.MarioSetHealthPoints(MarioId, healthPoints);

    // TODO: allow mario to collide with things using a normal active collider.
    public void TakeDamage(float3 worldPosition, uint damage) => Interop.MarioTakeDamage(MarioId, worldPosition, damage);

    public void WearCap(MarioCapType capType, float duration = 15f, bool playMusic = false)
    {
        switch (capType)
        {
            case MarioCapType.VanishCap:
            case MarioCapType.MetalCap:
            case MarioCapType.WingCap:
                if (CurrentState.IsWearingCap(capType))
                {
                    Interop.MarioCapExtend(MarioId, 15f);
                }
                else
                {
                    StateFlag flag = capType switch
                    {
                        MarioCapType.VanishCap => StateFlag.VanishCap,
                        MarioCapType.MetalCap  => StateFlag.MetalCap,
                        MarioCapType.WingCap   => StateFlag.WingCap,
                        _                      => throw new ArgumentOutOfRangeException(nameof(capType), capType, null)
                    };

                    Interop.MarioCap(MarioId, flag, duration, playMusic);
                }

                break;

            case MarioCapType.NormalCap:
                SetState(CurrentState.StateFlags & ~(uint)(
                             StateFlag.VanishCap |
                             StateFlag.MetalCap |
                             StateFlag.WingCap));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(capType), capType, null);
        }
    }

    public void SetMarioAsNuked(bool delete = false)
    {
        _isNuked = true;
        bool deleteMario = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyDeleteAfterDeath) || delete;

        ResoniteMod.Debug($"One of our Marios died, it has been 15 seconds and we're going to {(deleteMario ? "delete the mario" : "stop its engine updates")}.");

        if (deleteMario) Dispose();
    }

    private void SetAction(ActionFlag actionFlag) => Interop.MarioSetAction(MarioId, actionFlag);

    private void SetAction(uint actionFlags) => Interop.MarioSetAction(MarioId, actionFlags);

    private void SetState(uint flags) => Interop.MarioSetState(MarioId, flags);

    private void SetVelocity(float3 unityVelocity) => Interop.MarioSetVelocity(MarioId, unityVelocity);

    private void SetForwardVelocity(float unityVelocity) => Interop.MarioSetForwardVelocity(MarioId, unityVelocity);

    private void Hold()
    {
        if (CurrentState.IsDead) return;
        SetAction(ActionFlag.Grabbed);
    }

    public void TeleportStart()
    {
        if (CurrentState.IsDead) return;
        SetAction(ActionFlag.TeleportFadeOut);
    }

    public void TeleportEnd()
    {
        if (CurrentState.IsDead) return;
        SetAction(ActionFlag.TeleportFadeIn);
    }

    private void Throw()
    {
        if (CurrentState.IsDead) return;
        float3 throwVelocityFlat = CurrentState.ScaledPosition - PreviousState.ScaledPosition;
        SetFaceAngle(floatQ.LookRotation(throwVelocityFlat));
        bool hasWingCap = CurrentState.IsWearingCap(MarioCapType.WingCap);
        SetAction(hasWingCap ? ActionFlag.Flying : ActionFlag.ThrownForward);
        SetVelocity(throwVelocityFlat);
        SetForwardVelocity(throwVelocityFlat.Magnitude);
    }

    public void Heal(byte healthPoints)
    {
        if (CurrentState.IsDead)
        {
            // Revive (not working)
            Interop.MarioSetHealthPoints(MarioId, healthPoints + 1);
            SetAction(ActionFlag.Idle);
        }
        else
        {
            Interop.MarioHeal(MarioId, healthPoints);
        }
    }

    /*
    public void PickupCoin(CVRSM64InteractableParticles.ParticleType coinType) {
        if (!_enabled) return;

        switch (coinType) {
            case CVRSM64InteractableParticles.ParticleType.GoldCoin:
                Interop.MarioHeal(MarioId, 1);
                Interop.PlaySoundGlobal(SoundBitsKeys.SOUND_GENERAL_COIN);
                break;
            case CVRSM64InteractableParticles.ParticleType.BlueCoin:
                Interop.PlaySoundGlobal(SoundBitsKeys.SOUND_GENERAL_COIN);
                Interop.MarioHeal(MarioId, 5);
                break;
            case CVRSM64InteractableParticles.ParticleType.RedCoin:
                Interop.PlaySoundGlobal(SoundBitsKeys.SOUND_GENERAL_RED_COIN);
                Interop.MarioHeal(MarioId, 2);
                break;
        }
    }
    */

    private enum Button
    {
        Jump,
        Kick,
        Stomp
    }

    public void Dispose()
    {
        if (IsLocal)
        {
            if (MarioSlot is { IsRemoved: false })
            {
                MarioSlot.Destroy();
            }
        }

        if (_marioRendererObject is { IsRemoved: false })
        {
            _marioRendererObject.Destroy();
        }

        if (Interop.IsGlobalInit)
        {
            SM64Context.RemoveMario(this);
        }

        _enabled = false;
    }
}