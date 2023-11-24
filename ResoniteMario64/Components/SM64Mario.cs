using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ResoniteMario64;


[Category("SM64")]
[DefaultUpdateOrder(999999)]
public class SM64Mario {

    internal Slot MarioObject;
    internal Grabbable MarioGrabbable;

    // Renderer
    private Slot _marioRendererObject;
    internal MeshX marioMesh;
    internal LocalMeshProvider marioMeshProvider;
    internal PBS_VertexColorMetallic marioMaterial;

    public SM64Mario(Slot slot)
    {
        MarioObject = slot;
        MarioGrabbable = slot.GetComponentOrAttach<Grabbable>();
        var coll = slot.AttachComponent<CapsuleCollider>();
        coll.Offset.Value = new float3(0, 0.075f, 0);
        coll.Radius.Value = 0.05f;
        coll.Height.Value = 0.15f;
        MarioObject.OnPrepareDestroy += (s) =>
        {
            DestroyMario();
        };
        var initPos = MarioObject.GlobalPosition;
        MarioId = Interop.MarioCreate(new float3(-initPos.x, initPos.y, initPos.z) * Interop.SCALE_FACTOR);

        CreateMarioRenderer();

        var vars = MarioObject.AddSlot("MarioInputs");
        joystick = vars.AttachComponent<DynamicReferenceVariable<IValue<float2>>>();
        joystick.VariableName.Value = "joystick";
        jump = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        jump.VariableName.Value = "jump";
        kick = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        kick.VariableName.Value = "kick";
        stomp = vars.AttachComponent<DynamicReferenceVariable<IValue<bool>>>();
        stomp.VariableName.Value = "stomp";
    }

    // Inputs
    readonly DynamicReferenceVariable<IValue<float2>> joystick;
    readonly DynamicReferenceVariable<IValue<bool>> jump;
    readonly DynamicReferenceVariable<IValue<bool>> kick;
    readonly DynamicReferenceVariable<IValue<bool>> stomp;


    // Mario State
    private float3[][] _positionBuffers;
    private float3[][] _normalBuffers;
    private float3[] _lerpPositionBuffer;
    private float3[] _lerpNormalBuffer;
    private float3[] _colorBuffer;
    private color[] _colorBufferColors;
    private float2[] _uvBuffer;
    private int _buffIndex;
    private Interop.SM64MarioState[] _states;
    private ushort _numTrianglesUsed;
    private ushort _previousNumTrianglesUsed;


    // Internal (NonSerialized)
    public uint MarioId;
    private bool _enabled;
    private bool _wasPickedUp;
    private bool _initializedByRemote;
    private bool _isDying;
    private bool _isNuked;

    // Threading
    private readonly object _lock = new();

    protected void CreateMarioRenderer() {
        // Initialize Buffers
        lock (_lock)
        {
            _states = new Interop.SM64MarioState[2] {
                new Interop.SM64MarioState(),
                new Interop.SM64MarioState()
            };
        }

        _lerpPositionBuffer = new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _lerpNormalBuffer = new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _positionBuffers = new float3[][] { new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES], new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
        _normalBuffers = new float3[][] { new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES], new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
        _colorBuffer = new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _colorBufferColors = new color[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _uvBuffer = new float2[3 * Interop.SM64_GEO_MAX_TRIANGLES];


        // Create Mario Slot
        _marioRendererObject = MarioObject.World.AddSlot("MarioRenderer");

        var _marioMeshRenderer = _marioRendererObject.AttachComponent<MeshRenderer>();
        marioMeshProvider = _marioRendererObject.AttachComponent<LocalMeshProvider>();
        marioMaterial = _marioRendererObject.AttachComponent<PBS_VertexColorMetallic>();

        var marioTexture = _marioRendererObject.AttachComponent<StaticTexture2D>();

        // I generated this texture inside Interop.cs (look for 'mario.png')
        // then uploaded it and saved it to my inventory. I think it's better this way, because it gets cached
        marioTexture.URL.Value = new Uri("resdb:///f05ee58da859926aa5652bb92a07ad0d5ce5fb33979fd7ead9bc5ed78eb5b7d7.webp");
        marioMaterial.AlbedoTexture.Target = marioTexture;

        _marioMeshRenderer.Materials.Add(marioMaterial);
        _marioMeshRenderer.Mesh.Target = marioMeshProvider;
        marioMesh = new MeshX();

        _marioRendererObject.LocalScale = new float3(-1, 1, 1) / Interop.SCALE_FACTOR;
        _marioRendererObject.LocalPosition = float3.Zero;

        marioMesh.AddVertices(_lerpPositionBuffer.Length);
        var marioTris = marioMesh.AddSubmesh<TriangleSubmesh>();
        for (int i = 0; i < Interop.SM64_GEO_MAX_TRIANGLES; i++)
        {
            marioTris.AddTriangle(i * 3, (i * 3) + 1, (i * 3) + 2);
        }

        marioMeshProvider.LocalManualUpdate = true;
        marioMeshProvider.HighPriorityIntegration.Value = true;

        _enabled = true;
    }

    public void DestroyMario() {

        if (_marioRendererObject != null) {
            _marioRendererObject.Destroy();
        }

        if (Interop.isGlobalInit) {
            SM64Context.UnregisterMario(this);
        }

        _enabled = false;
    }

    // Game Tick
    public void ContextFixedUpdateSynced(List<SM64Mario> marios) {

        if (!_enabled  || _isNuked) return;

        var inputs = new Interop.SM64MarioInputs();
        var look = GetCameraLookDirection();
        look = look.SetY(0).Normalized;

        var joystick = GetJoystickAxes();

        inputs.camLookX = -look.x;
        inputs.camLookZ = look.z;
        inputs.stickX = joystick.x;
        inputs.stickY = -joystick.y;
        inputs.buttonA = GetButtonHeld(Button.Jump) ? (byte)1 : (byte)0;
        inputs.buttonB = GetButtonHeld(Button.Kick) ? (byte)1 : (byte)0;
        inputs.buttonZ = GetButtonHeld(Button.Stomp) ? (byte)1 : (byte)0;

        lock (_lock) {
            _states[_buffIndex] = Interop.MarioTick(MarioId, inputs, _positionBuffers[_buffIndex], _normalBuffers[_buffIndex], _colorBuffer, _uvBuffer, out _numTrianglesUsed);

            // If the tris count changes, reset the buffers
            if (_previousNumTrianglesUsed != _numTrianglesUsed) {
                for (var i = _numTrianglesUsed * 3; i < _positionBuffers[_buffIndex].Length; i++) {
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
        }

        var currentStateFlags = GetCurrentState().flags;
        var currentStateAction = GetCurrentState().action;

        if (IsMine()) {

            if(MarioGrabbable != null)
            {
                var root = MarioGrabbable.Grabber?.Slot.ActiveUserRoot;
                var pickup = root != null && root == MarioObject.LocalUserRoot;
                if(_wasPickedUp != pickup)
                {
                    if (_wasPickedUp) Throw();
                    else Hold();
                }

                _wasPickedUp = pickup;
            }

            // Check for deaths, so we delete the prop
            if (!_isDying && IsDead()) {
                _isDying = true;
                MarioObject.RunInSeconds(15f, SetMarioAsNuked);
            }
        }

        for (var i = 0; i < _colorBuffer.Length; ++i) {
            _colorBufferColors[i] = new color(_colorBuffer[i].x, _colorBuffer[i].y, _colorBuffer[i].z, 1f);
        }

        if(marioMesh != null)
        {
            for (int i = 0; i < marioMesh.VertexCount; i++)
            {
                marioMesh.SetColor(i, _colorBufferColors[i]);
                marioMesh.SetUV(i, 0, _uvBuffer[i]);
            }
        }
    }

    // Engine Tick
    public void ContextUpdateSynced() {
        if (!_enabled || _isNuked) return;

        if (!IsMine() && !_initializedByRemote) return;

        // lerp from previous state to current (this means when you make an input it's delayed by one frame, but it means we can have nice interpolation)
        var t = (float)((MarioObject.Time.WorldTime - SM64Context._instance.LastTick) / (ResoniteMario64.config.GetValue(ResoniteMario64.KEY_GAME_TICK_MS)/1000f));

        lock (_lock) {
            var j = 1 - _buffIndex;

            for (var i = 0; i < _numTrianglesUsed * 3; ++i) {
                _lerpPositionBuffer[i] = MathX.LerpUnclamped(_positionBuffers[_buffIndex][i], _positionBuffers[j][i], t);
                _lerpNormalBuffer[i] = MathX.LerpUnclamped(_normalBuffers[_buffIndex][i], _normalBuffers[j][i], t);
            }

            // Handle the position and rotation
            if (IsMine() && !IsBeingGrabbedByMe()) {
                MarioObject.GlobalPosition = MathX.LerpUnclamped(_states[_buffIndex].UnityPosition, _states[j].UnityPosition, t);
                MarioObject.GlobalRotation = MathX.LerpUnclamped(_states[_buffIndex].UnityRotation, _states[j].UnityRotation, t);
            }
            else {
                SetPosition(MarioObject.GlobalPosition);
                SetFaceAngle(MarioObject.GlobalRotation);
            }
        }

        if(marioMesh != null)
        {
            for (int i = 0; i < marioMesh.VertexCount; i++)
            {
                marioMesh.SetVertex(i, _lerpPositionBuffer[i]);
                marioMesh.SetNormal(i, _lerpNormalBuffer[i]);
            }
            marioMeshProvider.Mesh = marioMesh;
            marioMeshProvider.Update();
        }
    }


    private float3 GetCameraLookDirection() {
        return MarioObject.LocalUser.Root.ViewRotation * float3.Forward;
    }

    private enum Button {
        Jump,
        Kick,
        Stomp,
    }

    private float2 GetJoystickAxes()
    {
        if (SM64Context._instance == null) return float2.Zero;

        return joystick?.Reference.Target?.Value ?? SM64Context._instance.joystick;
    }

    private bool GetButtonHeld(Button button)
    {
        if (SM64Context._instance == null) return false;

        switch (button)
        {
            case Button.Jump:
                return jump?.Reference.Target?.Value ?? SM64Context._instance.jump;
            case Button.Kick:
                return kick?.Reference.Target?.Value ?? SM64Context._instance.kick;
            case Button.Stomp:
                return stomp?.Reference.Target?.Value ?? SM64Context._instance.stomp;
        }
        return false;
    }

    private Interop.SM64MarioState GetCurrentState() {
        lock (_lock) {
            return _states[1 - _buffIndex];
        }
    }

    private Interop.SM64MarioState GetPreviousState() {
        lock (_lock) {
            return _states[_buffIndex];
        }
    }

    public void SetPosition(float3 pos) {
        if (!_enabled) return;
        Interop.MarioSetPosition(MarioId, pos);
    }

    public void SetRotation(floatQ rot) {
        if (!_enabled) return;
        Interop.MarioSetRotation(MarioId, rot);
    }

    public void SetFaceAngle(floatQ rot) {
        if (!_enabled) return;
        Interop.MarioSetFaceAngle(MarioId, rot);
    }

    public void SetHealthPoints(float healthPoints) {
        if (!_enabled) return;
        Interop.MarioSetHealthPoints(MarioId, healthPoints);
    }

    // TODO: allow mario to collide with things using a normal active collider.
    public void TakeDamage(float3 worldPosition, uint damage) {
        if (!_enabled) return;
        Interop.MarioTakeDamage(MarioId, worldPosition, damage);
    }


    internal void WearCap(uint flags, Utils.MarioCapType capType, bool playMusic) {
        if (!_enabled) return;

        if (Utils.HasCapType(flags, capType)) return;
        switch (capType) {
            case Utils.MarioCapType.VanishCap:
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_VANISH_CAP, 15f, playMusic);
                break;
            case Utils.MarioCapType.MetalCap:
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_METAL_CAP, 15f, playMusic);
                break;
            case Utils.MarioCapType.WingCap:
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_WING_CAP, 40f, playMusic);
                break;
        }
    }

    private bool IsBeingGrabbedByMe() {
        var root = MarioGrabbable.Grabber?.Slot.ActiveUserRoot;
        return root != null && root == MarioObject.LocalUserRoot;
    }

    // TODO: sync
    public bool IsMine() => true;

    private bool IsDead() {
        lock (_lock) {
            return GetCurrentState().health < 1 * Interop.SM64_HEALTH_PER_HEALTH_POINT;
        }
    }

    private void SetMarioAsNuked() {
        lock (_lock) {
            _isNuked = true;
            var deleteMario = ResoniteMario64.config.GetValue(ResoniteMario64.KEY_DELETE_AFTER_DEATH);
            #if DEBUG
            ResoniteMario64.Msg($"One of our Marios died, it has been 15 seconds and we're going to " +
                            $"{(deleteMario ? "delete the mario" : "stop its engine updates")}.");
            #endif
            if (deleteMario) DestroyMario();
        }
    }

    private void SetAction(ActionFlags actionFlags) {
        Interop.MarioSetAction(MarioId, actionFlags);
    }

    private void SetAction(uint actionFlags) {
        Interop.MarioSetAction(MarioId, actionFlags);
    }

    private void SetState(uint flags) {
        Interop.MarioSetState(MarioId, flags);
    }

    private void SetVelocity(float3 unityVelocity) {
        Interop.MarioSetVelocity(MarioId, unityVelocity);
    }

    private void SetForwardVelocity(float unityVelocity) {
        Interop.MarioSetForwardVelocity(MarioId, unityVelocity);
    }

    private void Hold() {
        if (IsDead()) return;
        SetAction(ActionFlags.ACT_GRABBED);
    }

    public void TeleportStart() {
        if (IsDead()) return;
        SetAction(ActionFlags.ACT_TELEPORT_FADE_OUT);
    }

    public void TeleportEnd() {
        if (IsDead()) return;
        SetAction(ActionFlags.ACT_TELEPORT_FADE_IN);
    }

    private void Throw() {
        if (IsDead()) return;
        var currentState = GetCurrentState();
        var throwVelocityFlat = currentState.UnityPosition - GetPreviousState().UnityPosition;
        SetFaceAngle(floatQ.LookRotation(throwVelocityFlat));
        var hasWingCap = Utils.HasCapType(currentState.flags, Utils.MarioCapType.WingCap);
        SetAction(hasWingCap ? ActionFlags.ACT_FLYING : ActionFlags.ACT_THROWN_FORWARD);
        SetVelocity(throwVelocityFlat);
        SetForwardVelocity(throwVelocityFlat.Magnitude);
    }

    public void Heal(byte healthPoints) {
        if (!_enabled) return;

        if (IsDead()) {
            // Revive (not working)
            Interop.MarioSetHealthPoints(MarioId, healthPoints + 1);
            SetAction(ActionFlags.ACT_FLAG_IDLE);
        }
        else {
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

    public bool IsFirstPerson() {
        lock (_lock) {
            return GetCurrentState().IsFlyingOrSwimming();
        }
    }

    public bool IsSwimming() {
        lock (_lock) {
            return GetCurrentState().IsSwimming();
        }
    }

    public bool IsFlying() {
        lock (_lock) {
            return GetCurrentState().IsFlying();
        }
    }
}
