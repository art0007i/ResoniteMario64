using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

public sealed class SM64Mario : IDisposable
{
    private bool _enabled;
    private bool _isDying;
    private bool _isNuked;
    private bool _disposed;

    public readonly uint MarioId;

    private static float MarioScale => 1000.0f / Interop.ScaleFactor;

    public Slot MarioSlot { get; }
    public User MarioUser { get; }
    public DynamicVariableSpace MarioSpace { get; }
    public bool IsLocal => MarioUser.IsLocalUser;

    public World World { get; }
    public SM64Context Context { get; }

#region Renderer

    // Renderer/Mesh
    private Slot _marioRendererSlot;
    private Slot _marioNonModdedRendererSlot;
    private MeshRenderer _marioMeshRenderer;
    private MeshX _marioMesh;
    private LocalMeshProvider _marioMeshProvider;

    // Materials
    private bool IsMatSwitching { get; set; }
    private bool IsMat2Switching { get; set; }

    private PBS_DualSidedMetallic _marioMaterial;
    private PBS_VertexColorMetallic _marioMaterialClipped;
    private XiexeToonMaterial _marioMaterialMetal;
    private PBS_Metallic _marioMaterialVanish;

    private IAssetProvider<Material> CurrentMaterial
    {
        get => _marioMeshRenderer.Materials.Count > 0 ? _marioMeshRenderer.Materials[0] : null;
        set
        {
            if (IsMatSwitching) return;
            IsMatSwitching = true;

            _marioMeshRenderer.RunInUpdates(2, () =>
            {
                if (_marioMeshRenderer.Materials.Count > 0 && _marioMeshRenderer.Materials[0] != value)
                {
                    _marioMeshRenderer.Materials[0] = value;
                }

                IsMatSwitching = false;
            });
        }
    }

    private IAssetProvider<Material> CurrentFaceMaterial
    {
        get => _marioMeshRenderer.Materials.Count > 1 ? _marioMeshRenderer.Materials[1] : null;
        set
        {
            if (IsMat2Switching) return;
            IsMat2Switching = true;

            _marioMeshRenderer.RunInUpdates(2, () =>
            {
                if (_marioMeshRenderer.Materials.Count > 1 && _marioMeshRenderer.Materials[1] != value)
                {
                    _marioMeshRenderer.Materials[1] = value;
                }

                IsMat2Switching = false;
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
    private readonly CapsuleCollider _marioCollider;
    private bool _wasPickedUp;

    private float _waterLevel;

    public SM64Mario(Slot slot, SM64Context instance)
    {
        ResoniteMod.Debug("Constructing mario");
        MarioUser = slot.GetAllocatingUser();
        ResoniteMod.Debug("is local " + IsLocal);

        World = slot.World;
        Context = instance;

        MarioSlot = slot;
        MarioSlot.Tag = MarioTag;
        if (IsLocal) MarioSlot.Name += $" #{Context.AllMarios.Count(x => x.Value.IsLocal)}";

        MarioSpace = MarioSlot.GetComponentOrAttach<DynamicVariableSpace>();
        MarioSpace.SpaceName.Value = MarioSpaceName;

        _marioGrabbable = MarioSlot.GetComponentOrAttach<Grabbable>();

        _marioCollider = MarioSlot.GetComponentOrAttach<CapsuleCollider>();
        if (IsLocal)
        {
            _marioCollider.Offset.Value = new float3(0, 0.075f * MarioScale);
            _marioCollider.Radius.Value = 0.05f * MarioScale;
            _marioCollider.Height.Value = 0.15f * MarioScale;
        }

        ResoniteMod.Debug("got comp refs");

        MarioSlot.OnPrepareDestroy += _ => Dispose();
        float3 initPos = MarioSlot.GlobalPosition;

        ResoniteMod.Debug("adding interop");
        MarioId = Interop.MarioCreate(new float3(-initPos.x, initPos.y, initPos.z) * Interop.ScaleFactor);
        _waterLevel = Context.ContextVariableSpace.TryReadValue(WaterVarName, out float waterLevel) ? waterLevel : -100f;
        Interop.SetWaterLevel(MarioId, _waterLevel);

        ResoniteMod.Debug("adding renderer");
        CreateMarioRenderer();
        MarioSlot.RunInUpdates(3, CreateNonModdedRenderer);

        if (!IsLocal) return;

        ResoniteMod.Debug("setting up streams");
        Slot vars = MarioSlot.AddSlot("Inputs");

        DynamicReferenceVariable<IValue<float2>> joystick1 = vars.AttachComponent<DynamicReferenceVariable<IValue<float2>>>();
        joystick1.VariableName.Value = JoystickVarName;
        joystick1.Reference.Target = JoystickStream;

        DynamicReferenceVariable<IValue<bool>> jump1 = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        jump1.VariableName.Value = JumpVarName;
        jump1.Reference.Target = JumpStream;

        DynamicReferenceVariable<IValue<bool>> kick1 = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        kick1.VariableName.Value = PunchVarName;
        kick1.Reference.Target = PunchStream;

        DynamicReferenceVariable<IValue<bool>> stomp1 = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        stomp1.VariableName.Value = CrouchVarName;
        stomp1.Reference.Target = CrouchStream;

        DynamicValueVariable<float> healthPoints = vars.AttachComponent<DynamicValueVariable<float>>();
        healthPoints.VariableName.Value = HealthPointsVarName;

        DynamicValueVariable<uint> actionFlags = vars.AttachComponent<DynamicValueVariable<uint>>();
        actionFlags.VariableName.Value = ActionFlagsVarName;

        DynamicValueVariable<uint> stateFlags = vars.AttachComponent<DynamicValueVariable<uint>>();
        stateFlags.VariableName.Value = StateFlagsVarName;

        ResoniteMod.Debug("huge success");
    }

    // Inputs
    private float2 Joystick => MarioSpace.TryReadValue(JoystickVarName, out IValue<float2> joystick) ? joystick?.Value ?? float2.Zero : float2.Zero;
    private ValueStream<float2> _joystickStream;

    private ValueStream<float2> JoystickStream
    {
        get
        {
            if (_joystickStream == null || _joystickStream.IsRemoved)
            {
                _joystickStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<float2>>(MarioSlot.LocalUser, $"SM64 {JoystickVarName}", out bool created);
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

    private bool Jump => MarioSpace.TryReadValue(JumpVarName, out IValue<bool> jump) && jump?.Value is true;
    private ValueStream<bool> _jumpStream;

    private ValueStream<bool> JumpStream
    {
        get
        {
            if (_jumpStream == null || _jumpStream.IsRemoved)
            {
                _jumpStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {JumpVarName}", out bool created);
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

    private bool Punch => MarioSpace.TryReadValue(PunchVarName, out IValue<bool> kick) && kick?.Value is true;
    private ValueStream<bool> _punchStream;

    private ValueStream<bool> PunchStream
    {
        get
        {
            if (_punchStream == null || _punchStream.IsRemoved)
            {
                _punchStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {PunchVarName}", out bool created);
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

    private bool Crouch => MarioSpace.TryReadValue(CrouchVarName, out IValue<bool> stomp) && stomp?.Value is true;
    private ValueStream<bool> _crouchStream;

    private ValueStream<bool> CrouchStream
    {
        get
        {
            if (_crouchStream == null || _crouchStream.IsRemoved)
            {
                _crouchStream = CommonAvatarBuilder.GetStreamOrAdd<ValueStream<bool>>(MarioSlot.LocalUser, $"SM64 {CrouchVarName}", out bool created);
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

    public float SyncedHealthPoints => MarioSpace.TryReadValue(HealthPointsVarName, out float healthPoints) ? healthPoints : 255;
    public uint SyncedActionFlags => MarioSpace.TryReadValue(ActionFlagsVarName, out uint actionFlags) ? actionFlags : 0;
    public uint SyncedStateFlags => MarioSpace.TryReadValue(StateFlagsVarName, out uint stateFlags) ? stateFlags : 0;

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
        ResoniteMod.Debug("buffers 👍");

        // Create Mario Slot
#if DEBUG
        _marioRendererSlot = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyRenderSlotLocal)
                ? MarioSlot.World.AddLocalSlot($"{MarioSlot.Name} Renderer - {MarioSlot.LocalUser.UserName}")
                : MarioSlot.World.AddSlot($"{MarioSlot.Name} Renderer - {MarioSlot.LocalUser.UserName}", false);
#else
        _marioRendererSlot = MarioSlot.World.AddLocalSlot($"{MarioSlot.Name} Renderer - {MarioSlot.LocalUser.UserName}");
#endif

        _marioMeshRenderer = _marioRendererSlot.AttachComponent<MeshRenderer>();
        _marioMeshProvider = _marioRendererSlot.AttachComponent<LocalMeshProvider>();

        _marioMaterial = _marioRendererSlot.AttachComponent<PBS_DualSidedMetallic>();
        _marioMaterialClipped = _marioRendererSlot.AttachComponent<PBS_VertexColorMetallic>();
        _marioMaterialMetal = _marioRendererSlot.AttachComponent<XiexeToonMaterial>();
        _marioMaterialVanish = _marioRendererSlot.AttachComponent<PBS_Metallic>();
        ResoniteMod.Debug("materials 👍");

        // I generated this texture inside Interop.cs (look for 'mario.png')
        // then uploaded it and saved it to my inventory. I think it's better this way, because it gets cached
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

        ResoniteMod.Debug("textures 👍");

        _marioMeshRenderer.Materials.Add();
        _marioMeshRenderer.Materials.Add(_marioMaterial);

        _marioMeshRenderer.Mesh.Target = _marioMeshProvider;
        _marioMesh = new MeshX();

        ResoniteMod.Debug("meshx 👍");

        _marioRendererSlot.LocalScale = new float3(-1, 1, 1) / Interop.ScaleFactor;
        _marioRendererSlot.LocalPosition = float3.Zero;

        _marioMesh.AddVertices(_lerpPositionBuffer.Length);
        TriangleSubmesh marioTris = _marioMesh.AddSubmesh<TriangleSubmesh>();
        for (int i = 0; i < Interop.SM64GeoMaxTriangles; i++)
        {
            marioTris.AddTriangle(i * 3, i * 3 + 1, i * 3 + 2);
        }

        ResoniteMod.Debug("tris 👍");

        _marioMeshProvider.Mesh = _marioMesh;
        _marioMeshProvider.LocalManualUpdate = true;
        _marioMeshProvider.HighPriorityIntegration.Value = true;

        _enabled = true;
        ResoniteMod.Debug("done 👍 " + _marioMeshProvider.Mesh.VertexCount);
    }

    private void CreateNonModdedRenderer()
    {
        Uri uri = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMarioUrl);
        if (uri == null)
        {
            uri = new Uri("resdb:///d85c309f7aa0c909f6b1518c4a74dacc383760c516425bec6617e8ebe8dd50da.brson");
        }

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
                tempSlot.GetComponent<InventoryItem>()?.Unpack();
                ResoniteMod.Debug("Setup Non-Modded Renderer");
            });
        }

        ResoniteMod.Debug("non-modded finish");
    }

    // Game Tick
    internal void ContextFixedUpdateSynced()
    {
        if (!_enabled || _isNuked || _disposed) return;

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
            MarioSpace.TryWriteValue(ActionFlagsVarName, CurrentActionFlags);
            MarioSpace.TryWriteValue(StateFlagsVarName, CurrentStateFlags);

            // Check for deaths, so we delete mario
            float floorHeight = Interop.FindFloor(MarioSlot.GlobalPosition, out SM64SurfaceCollisionData data);
            bool isDeathPlane = data.type == (short)SM64SurfaceType.DeathPlane;
            if (!_isDying && CurrentState.IsDead || !_isDying && MathX.Distance(floorHeight, MarioSlot.GlobalPosition.Y) < 50 && isDeathPlane) /* Find a better value for the distance check */
            {
                _isDying = true;
                bool isQuickSand = (SyncedActionFlags & (uint)ActionFlag.QuicksandDeath) == (uint)ActionFlag.QuicksandDeath;
                MarioSlot.RunInSeconds(isQuickSand ? 1f : isDeathPlane ? 0.5f : 3f, () => Interop.PlaySoundGlobal(Sounds.Menu_BowserLaugh));
                MarioSlot.RunInSeconds(isQuickSand ? 3f : isDeathPlane ? 2f : 15f, () => SetMarioAsNuked(true));
            }

            List<SM64Interactable> interactables = Context.Interactables.Values.GetTempList();
            foreach (SM64Interactable interactable in interactables)
            {
                HandleInteractable(interactable);
            }
        }
        else
        {
            // This seems to be kinda broken, maybe revisit syncing the WHOLE state instead
            // if (currentStateFlags != syncedFlags) SetState(syncedAction);
            // if (currentStateAction != syncedAction) SetAction(syncedAction);

            // Trigger the cap if the synced values have cap (if we already have the cap it will ignore)
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

            if (Utils.HasCapType(SyncedStateFlags, MarioCapType.NormalCap))
            {
                WearCap(MarioCapType.NormalCap);
            }

            // Trigger teleport for remotes
            // if (Utils.IsTeleporting(SyncedStateFlags) && Time.time > _startedTeleporting + 5 * SM64Teleporter.TeleportDuration)
            // {
            //     _startedTeleporting = Time.time;
            // }
        }

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

        // Just for now until Collider Shenanigans is implemented
        List<SM64Mario> marios = Context.AllMarios.Values.GetTempList();
        SM64Mario attackingMario = marios.FirstOrDefault(mario => mario != this && mario.CurrentState.IsAttacking && MathX.Distance(mario.MarioSlot.GlobalPosition, MarioSlot.GlobalPosition) <= 0.1f * MarioScale);
        if (attackingMario != null)
        {
            TakeDamage(attackingMario.MarioSlot.GlobalPosition, 1);
        }

        List<Collider> waterBoxes = Context.WaterBoxes.GetTempList();
        bool setWaterLevel = false;
        float newWaterLevel = -100f;

        foreach (Collider waterBox in waterBoxes)
        {
            if (waterBox.IsRemoved || waterBox.IsDisposed) continue;

            if (_marioCollider.GlobalBoundingBox.Center.IsBetween(waterBox.GlobalBoundingBox.min, waterBox.GlobalBoundingBox.max))
            {
                newWaterLevel = waterBox.GlobalBoundingBox.max.Y;
                setWaterLevel = true;
                break;
            }
        }

        if (!setWaterLevel)
        {
            newWaterLevel = Context.ContextVariableSpace.TryReadValue(WaterVarName, out float fallbackLevel) ? fallbackLevel : -100f;
        }

        if (!MathX.Approximately(_waterLevel, newWaterLevel))
        {
            _waterLevel = newWaterLevel;
            Interop.SetWaterLevel(MarioId, _waterLevel);
        }

        if (Utils.HasCapType(SyncedStateFlags, MarioCapType.MetalCap))
        {
            if (CurrentMaterial != _marioMaterialMetal)
            {
                CurrentMaterial = _marioMaterialMetal;
            }
        }
        else if (Utils.HasCapType(SyncedStateFlags, MarioCapType.VanishCap))
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
        else
        {
            if (_marioMaterialClipped.AlbedoColor.Value != Utils.ColorXWhite)
            {
                _marioMaterialClipped.AlbedoColor.Value = Utils.ColorXWhite;
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
        }

        for (int i = 0; i < _colorBuffer.Length; ++i)
        {
            _colorBufferColors[i] = new color(_colorBuffer[i].x, _colorBuffer[i].y, _colorBuffer[i].z);
        }

        if (!_marioMeshProvider.Enabled)
        {
            ResoniteMod.Error("!!!!!!!!!! Shit fucked :(");
            _marioMeshProvider.Enabled = true;
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
        if (!_enabled || _isNuked || _disposed) return;

        // lerp from previous state to current (this means when you make an input it's delayed by one frame, but it means we can have nice interpolation)
        float t = (float)((MarioSlot.Time.WorldTime - Context.LastTick) / (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyGameTickMs) / 1000f));

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
            MarioSlot.WriteDynamicVariable(HealthPointsVarName, CurrentState.HealthPoints);
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

    public void SetPosition(float3 pos) => Interop.MarioSetPosition(MarioId, pos);

    public void SetRotation(floatQ rot) => Interop.MarioSetRotation(MarioId, rot);

    public void SetFaceAngle(floatQ rot) => Interop.MarioSetFaceAngle(MarioId, rot);

    public void SetHealthPoints(float healthPoints) => Interop.MarioSetHealthPoints(MarioId, healthPoints);

    public void TakeDamage(float3 worldPosition, uint damage) => Interop.MarioTakeDamage(MarioId, worldPosition, damage);

    public void WearCap(MarioCapType capType, float duration = 15f, bool playMusic = true)
    {
        if (playMusic)
        {
            playMusic = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayCapMusic);
        }

        switch (capType)
        {
            case MarioCapType.VanishCap:
            case MarioCapType.MetalCap:
            case MarioCapType.WingCap:
                // Prevent Vanish and Wing from being active at the same time - This prevents a crash
                if (capType == MarioCapType.VanishCap && Utils.HasCapType(SyncedStateFlags, MarioCapType.WingCap) || capType == MarioCapType.WingCap && Utils.HasCapType(SyncedStateFlags, MarioCapType.VanishCap))
                {
                    break;
                }

                if (Utils.HasCapType(SyncedStateFlags, capType))
                {
                    if (IsLocal) Interop.MarioCapExtend(MarioId, duration);
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
                if (Utils.HasCapType(SyncedStateFlags, capType)) break;
                
                SetState(StateFlag.CapOnHead | StateFlag.NormalCap);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(capType), capType, null);
        }
    }

    public void SetMarioAsNuked(bool delete = false)
    {
        _isNuked = true;
        bool deleteMario = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyDeleteAfterDeath) || delete;

        ResoniteMod.Debug($"One of our Marios died, so {(deleteMario ? "delete the mario" : "stop its engine updates")}.");

        if (deleteMario) Dispose();
    }

    public void SetAction(ActionFlag actionFlag) => Interop.MarioSetAction(MarioId, actionFlag);

    public void SetAction(uint actionFlags) => Interop.MarioSetAction(MarioId, actionFlags);

    public void SetState(StateFlag stateFlag) => Interop.MarioSetState(MarioId, stateFlag);

    public void SetState(uint stateFlags) => Interop.MarioSetState(MarioId, stateFlags);

    public void SetVelocity(float3 frooxVelocity) => Interop.MarioSetVelocity(MarioId, frooxVelocity);

    public void SetForwardVelocity(float frooxVelocity) => Interop.MarioSetForwardVelocity(MarioId, frooxVelocity);

    private void Hold()
    {
        if (CurrentState.IsDead) return;

        if ((CurrentActionFlags & (uint)ActionFlag.Sleeping) == (uint)ActionFlag.Sleeping)
        {
            SetAction(ActionFlag.WakingUp);
        }

        SetAction(ActionFlag.Grabbed);
    }

    private void Throw()
    {
        if (CurrentState.IsDead) return;
        float3 throwVelocityFlat = CurrentState.ScaledPosition - PreviousState.ScaledPosition;
        if (throwVelocityFlat.Magnitude > 0.01f)
        {
            if (IsLocal)
            {
                SetFaceAngle(floatQ.LookRotation(throwVelocityFlat));
            }
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

    public void Heal(byte healthPoints)
    {
        if (CurrentState.IsDead || !IsLocal) return;
        
        Interop.MarioHeal(MarioId, healthPoints);
    }

    public void HandleInteractable(SM64Interactable interactable)
    {
        if (!_enabled || !interactable.Collider.Slot.IsActive || !Utils.Overlaps(interactable.Collider.GlobalBoundingBox, _marioCollider.GlobalBoundingBox)) return;

        int typeId = interactable.TypeId;
        bool hasValue = interactable.HasValue;

        bool disable = true;
        switch (interactable.Type)
        {
            case SM64InteractableType.GoldCoin:
                Interop.PlaySoundGlobal(Sounds.SOUND_GENERAL_COIN);
                Heal(1);
                break;
            case SM64InteractableType.BlueCoin:
                Interop.PlaySoundGlobal(Sounds.SOUND_GENERAL_COIN);
                Heal(5);
                break;
            case SM64InteractableType.RedCoin:
                Sounds redCoinSound = typeId switch
                {
                    0 => Sounds.Menu_CollectRedCoin0,
                    1 => Sounds.Menu_CollectRedCoin1,
                    2 => Sounds.Menu_CollectRedCoin2,
                    3 => Sounds.Menu_CollectRedCoin3,
                    4 => Sounds.Menu_CollectRedCoin4,
                    5 => Sounds.Menu_CollectRedCoin5,
                    6 => Sounds.Menu_CollectRedCoin6,
                    7 => Sounds.Menu_CollectRedCoin7,
                    _ => Sounds.SOUND_GENERAL_RED_COIN
                };
                Interop.PlaySoundGlobal(redCoinSound);
                Heal(2);
                break;
            case SM64InteractableType.VanishCap:
                WearCap(MarioCapType.VanishCap);
                break;
            case SM64InteractableType.MetalCap:
                WearCap(MarioCapType.MetalCap);
                break;
            case SM64InteractableType.WingCap:
                WearCap(MarioCapType.WingCap);
                break;
            case SM64InteractableType.NormalCap:
                WearCap(MarioCapType.NormalCap);
                break;
            case SM64InteractableType.Star:
                Interop.PlaySoundGlobal(Sounds.Menu_StarSound);
                Heal(8);
                SetForwardVelocity(0f);
                SetAction(ActionFlag.Freefall);
                break;
            case SM64InteractableType.Damage:
                bool isMarioCollider = interactable.Collider.Slot.IsChildOf(MarioSlot);
                if (!isMarioCollider)
                {
                    uint damage = typeId switch
                    {
                        -1 or >= 10 => 1,
                        _           => (uint)typeId
                    };

                    TakeDamage(interactable.Collider.Slot.GlobalPosition, damage);
                }

                disable = false;
                break;
            case SM64InteractableType.None:
                disable = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        interactable.Collider.Slot.ActiveSelf = !disable;
    }

    private enum Button
    {
        Jump,
        Kick,
        Stomp
    }

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
            if (IsLocal)
            {
                if (MarioSlot is { IsRemoved: false })
                {
                    MarioSlot?.Destroy();
                }
            }

            if (_marioRendererSlot is { IsRemoved: false })
            {
                _marioRendererSlot?.Destroy();
            }
        }

        if (Interop.IsGlobalInit)
        {
            SM64Context.RemoveMario(this);
        }

        _enabled = false;
    }
}