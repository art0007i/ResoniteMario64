using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ResoniteMario64;


[Category("SM64")]
[DefaultUpdateOrder(999999)]
public class SM64Mario : Component {

    internal readonly UserRef controllingUser;

    internal MeshX marioMesh;
    internal LocalMeshProvider marioMeshProvider;
    internal PBS_VertexColorMetallic marioMaterial;

    // Main
    //[SerializeField] private CVRSpawnable spawnable;
    //private readonly Sync<bool> advancedOptions;

    // Material & Textures
    //private readonly AssetRef<Material> material;
    //private readonly Sync<bool> replaceTextures;
    //private readonly SyncFieldList<string> propertiesToReplaceWithTexture;

    // Animators
    //private readonly SyncRefList<Animator> animators;

    // Camera override
    private readonly Sync<bool> overrideCameraPosition;
    private readonly SyncRef<Slot> cameraPositionTransform;

    // Camera Mod Override
    //private SyncRef<Slot> cameraModTransform;
    //private SyncRefList<MeshRenderer> cameraModTransformRenderersToHide;

    protected override void OnAttach()
    {
        base.OnAttach();

        controllingUser.Target = LocalUser;

        //advancedOptions.Value = false;
        //replaceTextures.Value = true;
        //propertiesToReplaceWithTexture.Add("_MainTex");
        overrideCameraPosition.Value = false;
    }

    // Material Properties
    //private const float VanishOpacity = 0.5f;
    //private colorX _colorNormal;
    //private colorX _colorVanish;
    //private colorX _colorInvisible;
    //private readonly int _colorProperty = Shader.PropertyToID("_Color");
    /*private readonly List<int> _metallicProperties = new() {
        Shader.PropertyToID("_Metallic"),
        Shader.PropertyToID("_MochieMetallicMultiplier"),
        Shader.PropertyToID("_Glossiness"),
        Shader.PropertyToID("_MochieRoughnessMultiplier"),
    };*/

    // Components
    //private CVRPickupObject _pickup;
    //private CVRPlayerEntity _owner;
    //private Transform _localPlayerTransform;
    //private RemoteHeadPoint _ownerViewPoint;

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

    // Renderer
    private Slot _marioRendererObject;
    //private MeshRenderer _marioMeshRenderer;
    //private MeshX _marioMesh;

    // Internal (NonSerialized)
    public uint MarioId;
    private bool _enabled;
    private bool _initialized;
    private bool _wasPickedUp;
    private bool _initializedByRemote;
    private bool _isDying;
    private bool _isNuked;

    // Teleporters (NonSerialized)
    private float _startedTeleporting = float.MinValue;
    private Transform _teleportTarget;

    // Bypasses (NonSerialized)
    private bool _wasBypassed;
    private bool _isOverMaxCount;
    private bool _isOverMaxDistance;

    // Spawnable Inputs
    /*
    private int _inputHorizontalIndex;
    private CVRSpawnableValue _inputHorizontal;
    private int _inputVerticalIndex;
    private CVRSpawnableValue _inputVertical;
    private int _inputJumpIndex;
    private CVRSpawnableValue _inputJump;
    private int _inputKickIndex;
    private CVRSpawnableValue _inputKick;
    private int _inputStompIndex;
    private CVRSpawnableValue _inputStomp;
    */

    // Spawnable State Synced Params
    private enum SyncedParameterNames {
        Health,
        Flags,
        Action,
        HasCameraMod,
    }

    readonly Sync<uint> syncedFlags;
    readonly Sync<uint> syncedAction;

    private readonly Dictionary<SyncedParameterNames, int> _syncParameters = new();

    // Animators
    private enum LocalParameterNames {
        HealthPoints,
        HasMod,
        HasMetalCap,
        HasWingCap,
        HasVanishCap,
        IsMine,
        IsBypassed,
        IsTeleporting,
    }

    /*
    private static readonly Dictionary<LocalParameterNames, int> LocalParameters = new() {
        { LocalParameterNames.HealthPoints, Animator.StringToHash(nameof(LocalParameterNames.HealthPoints)) },
        { LocalParameterNames.HasMod, Animator.StringToHash(nameof(LocalParameterNames.HasMod)) },
        { LocalParameterNames.HasMetalCap, Animator.StringToHash(nameof(LocalParameterNames.HasMetalCap)) },
        { LocalParameterNames.HasWingCap, Animator.StringToHash(nameof(LocalParameterNames.HasWingCap)) },
        { LocalParameterNames.HasVanishCap, Animator.StringToHash(nameof(LocalParameterNames.HasVanishCap)) },
        { LocalParameterNames.IsMine, Animator.StringToHash(nameof(LocalParameterNames.IsMine)) },
        { LocalParameterNames.IsBypassed, Animator.StringToHash(nameof(LocalParameterNames.IsBypassed)) },
        { LocalParameterNames.IsTeleporting, Animator.StringToHash(nameof(LocalParameterNames.IsTeleporting)) },
    };
    */
    // Threading
    //private Interop.SM64MarioInputs _currentInputs;
    private readonly object _lock = new();

    // Melon prefs
    private static float _skipFarMarioDistance => ResoniteMario64.config.GetValue(ResoniteMario64.KEY_MARIO_CULL_DISTANCE);
    /*
    private void LoadInput(out CVRSpawnableValue parameter, out int index, string inputName) {
        try {
            index = spawnable.syncValues.FindIndex(value => value.name == inputName);
            parameter = spawnable.syncValues[index];
        }
        catch (ArgumentException) {
            var err = $"{nameof(CVRSM64Mario)} requires a ${nameof(CVRSpawnable)} with a synced value named ${inputName}!";
            ResoniteMario64.Error(err);
            spawnable.Delete();
            throw new Exception(err);
        }
    }
    */

    protected override void OnStart() {

        if (!ResoniteMario64.FilesLoaded) {
            ResoniteMario64.Error($"The mod files were not properly loaded! Check the errors at the startup!");
            Destroy();
            return;
        }

        #if DEBUG
        ResoniteMario64.Msg($"Initializing a SM64Mario Spawnable...");
        #endif

        /*
        // Check for Spawnable component
        if (spawnable != null) {
            #if DEBUG
            ResoniteMario64.Msg($"SM64Mario Spawnable was set! We don't need to look for it!");
            #endif
        }
        else {
            spawnable = GetComponent<CVRSpawnable>();
            if (spawnable == null) {
                var err = $"{nameof(CVRSM64Mario)} requires a ${nameof(CVRSpawnable)} on the same GameObject!";
                ResoniteMario64.Error(err);
                Destroy(this);
                return;
            }

            #if DEBUG
            ResoniteMario64.Msg($"SM64Mario Spawnable was missing, but we look at the game object and found one!");
            #endif
        }
        */
        /*
        if (!spawnable.IsMine()) {
            _owner = MetaPort.Instance.PlayerManager.NetworkPlayers.Find(entity => entity.Uuid == spawnable.ownerId);
            _ownerViewPoint = _owner.PuppetMaster._viewPoint;
            if (_ownerViewPoint == null || _ownerViewPoint == null) {
                var err = $"{nameof(CVRSM64Mario)} failed to start because couldn't find the viewpoint of the owner of it!";
                ResoniteMario64.Error(err);
                spawnable.Delete();
                return;
            }
        }
        */

        /*
        // Load the spawnable inputs
        LoadInput(out _inputHorizontal, out _inputHorizontalIndex, "Horizontal");
        LoadInput(out _inputVertical, out _inputVerticalIndex, "Vertical");
        LoadInput(out _inputJump, out _inputJumpIndex, "Jump");
        LoadInput(out _inputKick, out _inputKickIndex, "Kick");
        LoadInput(out _inputStomp, out _inputStompIndex, "Stomp");
        */

        
        // Load the spawnable synced params
        /*foreach (SyncedParameterNames syncedParam in Enum.GetValues(typeof(SyncedParameterNames))) {
            LoadInput(out var syncedValue, out var syncedValueIndex, syncedParam.ToString());
            _syncParameters.Add(syncedParam, syncedValueIndex);
        }*/
        

        /*
        // Check the advanced settings
        if (advancedOptions) {

            // Check the animators
            var toNuke = new HashSet<Animator>();
            foreach (var animator in animators) {
                if (animator == null || animator.runtimeAnimatorController == null) {
                    toNuke.Add(animator);
                }
                else {
                    animator.SetBool(LocalParameters[LocalParameterNames.HasMod], true);
                    if (spawnable.IsMine()) {
                        animator.SetBool(LocalParameters[LocalParameterNames.IsMine], true);
                    }
                }
            }
            foreach (var animatorToNuke in toNuke) animators.Remove(animatorToNuke);
            if (toNuke.Count > 0) {
                var animatorsToNukeStr = toNuke.Select(animToNuke => animToNuke.gameObject.name);
                ResoniteMario64.Warn($"Removing animators: {string.Join(", ", animatorsToNukeStr)} because they were null or had no controllers slotted.");
            }
        }
        */

        /*
        // Pickup
        _pickup = GetComponent<CVRPickupObject>();
        if (_pickup != null && !IsMine()) {
            _pickup.enabled = false;
        }
        */

        // Player setup transform
        //_localPlayerTransform = PlayerSetup.Instance.transform;

        // Ensure the list of renderers is not null and has no null values
        //cameraModTransformRenderersToHide ??= new List<Renderer>();
        //cameraModTransformRenderersToHide.RemoveAll(r => r == null);

        SM64Context.UpdateMarioCount();
        SM64Context.UpdatePlayerMariosState();

        #if DEBUG
        ResoniteMario64.Msg($"A SM64Mario Spawnable was initialized! Is ours: {controllingUser.Target == LocalUser}");
        #endif

        _initialized = true;

        OnEnabled();
    }

    protected override void OnEnabled() {
        if (!SM64Context.RegisterMario(this)) return;
        SM64Context.UpdateMarioCount();

        var initPos = Slot.GlobalPosition;
        MarioId = Interop.MarioCreate(new float3(-initPos.x, initPos.y, initPos.z) * Interop.SCALE_FACTOR);

        _marioRendererObject = World.AddSlot("MARIO");
        //_marioRendererObject.hideFlags |= HideFlags.HideInHierarchy;

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


        lock (_lock) {
            _states = new Interop.SM64MarioState[2] {
                new Interop.SM64MarioState(),
                new Interop.SM64MarioState()
            };
        }

        // If not material is set, let's set our fallback one
        /*if (material == null) {
            #if DEBUG
            ResoniteMario64.Msg($"CVRSM64Mario didn't have a material, assigning the default material...");
            #endif
            //material = CVRSuperMario64.GetMarioMaterial();
        }
        else {
            #if DEBUG
            ResoniteMario64.Msg($"CVRSM64Mario had a material! Using the existing one...");
            #endif
        }*/

        // Create a new instance of the material, so marios don't interfere with each other
        /*
        material = new Material(material);
        _colorNormal = new Color(material.color.r, material.color.g, material.color.b, material.color.a);
        _colorVanish = new Color(material.color.r, material.color.g, material.color.b, VanishOpacity);
        _colorInvisible = new Color(material.color.r, material.color.g, material.color.b, 0f);
        */

        //_marioMeshRenderer.material = material;

        // Replace the material's texture with mario's textures
        /*if (replaceTextures) {
            foreach (var propertyToReplaceWithTexture in propertiesToReplaceWithTexture) {
                try {
                    _marioMeshRenderer.sharedMaterial.SetTexture(propertyToReplaceWithTexture, Interop.marioTexture);
                }
                catch (Exception e) {
                    ResoniteMario64.Error($"Attempting to replace the texture in the shader property name {propertyToReplaceWithTexture}...");
                    ResoniteMario64.Error(e);
                }
            }
        }
        */

        _marioRendererObject.LocalScale = new float3(-1, 1, 1) / Interop.SCALE_FACTOR;
        _marioRendererObject.LocalPosition = float3.Zero;

        _lerpPositionBuffer = new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _lerpNormalBuffer = new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _positionBuffers = new float3[][] { new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES], new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
        _normalBuffers = new float3[][] { new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES], new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES] };
        _colorBuffer = new float3[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _colorBufferColors = new color[3 * Interop.SM64_GEO_MAX_TRIANGLES];
        _uvBuffer = new float2[3 * Interop.SM64_GEO_MAX_TRIANGLES];

        
        if(marioMesh != null)
        {
            marioMesh.AddVertices(_lerpPositionBuffer.Length);
            var marioTris = marioMesh.AddSubmesh<TriangleSubmesh>();
            for (int i = 0; i < Interop.SM64_GEO_MAX_TRIANGLES; i++)
            {
                marioTris.AddTriangle(i * 3, (i * 3) + 1, (i * 3) + 2);
            }

        }
        marioMeshProvider.LocalManualUpdate = true;
        marioMeshProvider.HighPriorityIntegration.Value = true;

        _enabled = true;
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        OnDisabled();
    }

    protected override void OnDisabled() {

        if (_marioRendererObject != null) {
            _marioRendererObject.Destroy();
            _marioRendererObject = null;
        }

        if (Interop.isGlobalInit) {
            SM64Context.UnregisterMario(this);
            Interop.MarioDelete(MarioId);
            SM64Context.UpdateMarioCount();
        }

        _enabled = false;
    }

    public void ContextFixedUpdateSynced(List<SM64Mario> marios) {

        if (!_enabled || !_initialized || _isNuked) return;

        // Janky remote sync check
        /*
        if (!IsMine() && !_initializedByRemote) {
            if (_syncParameters[SyncedParameterNames.Health].Item2.currentValue != 0) _initializedByRemote = true;
            else return;
        }
        */
        UpdateIsOverMaxDistance();

        if (_wasBypassed) return;

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
            /*
            if (_pickup != null && _pickup.IsGrabbedByMe() != _wasPickedUp) {
                if (_wasPickedUp) Throw();
                else Hold();
                _wasPickedUp = _pickup.IsGrabbedByMe();
            }
            */

            // Send the current flags and action to remotes
            //spawnable.SetValue(_syncParameters[SyncedParameterNames.Flags].Item1, Convert.ToSingle(currentStateFlags));
            //spawnable.SetValue(_syncParameters[SyncedParameterNames.Action].Item1, Convert.ToSingle(currentStateAction));

            // Check Interactables (trigger mario caps)
            SM64Interactable.HandleInteractables(this, currentStateFlags);

            // Check Teleporters
            //CVRSM64Teleporter.HandleTeleporters(this, currentStateFlags, ref _startedTeleporting, ref _teleportTarget);

            // Check for deaths, so we delete the prop
            if (!_isDying && IsDead()) {
                _isDying = true;
                RunInSeconds(15f, SetMarioAsNuked);
            }
        }
        else {

            // Grab the current flags and action from the owner
            //var syncedFlags = syncedFlags.Value//Convert.ToUInt32(_syncParameters[SyncedParameterNames.Flags].Item2.currentValue);
            //var syncedAction = Convert.ToUInt32(_syncParameters[SyncedParameterNames.Action].Item2.currentValue);

            // This seems to be kinda broken, maybe revisit syncing the WHOLE state instead
            // if (currentStateFlags != syncedFlags) SetState(syncedAction);
            // if (currentStateAction != syncedAction) SetAction(syncedAction);

            // Trigger the cap if the synced values have cap (if we already have the cape it will ignore)
            if (Utils.HasCapType(syncedFlags, Utils.MarioCapType.VanishCap)) {
                WearCap(currentStateFlags, Utils.MarioCapType.VanishCap, false);
            }
            if (Utils.HasCapType(syncedFlags, Utils.MarioCapType.MetalCap)) {
                WearCap(currentStateFlags, Utils.MarioCapType.MetalCap, false);
            }
            if (Utils.HasCapType(syncedFlags, Utils.MarioCapType.WingCap)) {
                WearCap(currentStateFlags, Utils.MarioCapType.WingCap, false);
            }
            
            // Trigger teleport for remotes
            /*if (Utils.IsTeleporting(syncedFlags) && Time.WorldTime > _startedTeleporting + 5 * CVRSM64Teleporter.TeleportDuration) {
                _startedTeleporting = Time.time;
            }*/
        }

        // Update Caps material and animator's parameters
        var hasVanishCap = Utils.HasCapType(currentStateFlags, Utils.MarioCapType.VanishCap);
        var hasWingCap = Utils.HasCapType(currentStateFlags, Utils.MarioCapType.WingCap);
        var hasMetalCap = Utils.HasCapType(currentStateFlags, Utils.MarioCapType.MetalCap);
        var isTeleporting = Utils.IsTeleporting(currentStateFlags);
        /*
        // Handle teleporting fading in
        if (Time.WorldTime > _startedTeleporting && Time.WorldTime <= _startedTeleporting + CVRSM64Teleporter.TeleportDuration) {
            material.color = Color.Lerp(_colorNormal, _colorInvisible, MathX.Clamp(Time.time-_startedTeleporting, 0f, 1f));
        }
        // Handle teleport fading out
        else if (Time.time > _startedTeleporting + CVRSM64Teleporter.TeleportDuration && Time.time <= _startedTeleporting + 2 * CVRSM64Teleporter.TeleportDuration) {
            material.color = Color.Lerp(_colorInvisible, _colorNormal, MathX.Clamp(Time.time-_startedTeleporting-CVRSM64Teleporter.TeleportDuration, 0f, 1f));
        }
        else {
            material.SetColor(_colorProperty, hasVanishCap ? _colorVanish : _colorNormal);
            foreach (var metallicProperty in _metallicProperties) {
                material.SetFloat(metallicProperty, hasMetalCap ? 1f : 0f);
            }
        }
        */
        /*
        foreach (var animator in animators) {
            animator.SetBool(LocalParameters[LocalParameterNames.HasVanishCap], hasVanishCap);
            animator.SetBool(LocalParameters[LocalParameterNames.HasWingCap], hasWingCap);
            animator.SetBool(LocalParameters[LocalParameterNames.HasMetalCap], hasMetalCap);
            animator.SetBool(LocalParameters[LocalParameterNames.IsTeleporting], isTeleporting);
        }
        */
        // Check if we're taking damage
        lock (marios) {
            var attackingMario = marios.FirstOrDefault(mario =>
                mario != this && mario.GetCurrentState().IsAttacking() &&
                MathX.Distance(mario.Slot.GlobalPosition, this.Slot.GlobalPosition) <= 0.1f);
            if (attackingMario != null) {
                TakeDamage(attackingMario.Slot.GlobalPosition, 1);
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

    public void ContextUpdateSynced() {
        if (!_enabled || !_initialized || _isNuked) return;

        if (!IsMine() && !_initializedByRemote) return;

        if (_wasBypassed) return;

        // lerp from previous state to current (this means when you make an input it's delayed by one frame, but it means we can have nice interpolation)
        var t = (float)((Time.WorldTime - SM64Context._instance.LastTick) / (ResoniteMario64.config.GetValue(ResoniteMario64.KEY_GAME_TICK_MS)/1000f));

        lock (_lock) {
            var j = 1 - _buffIndex;

            for (var i = 0; i < _numTrianglesUsed * 3; ++i) {
                _lerpPositionBuffer[i] = MathX.LerpUnclamped(_positionBuffers[_buffIndex][i], _positionBuffers[j][i], t);
                _lerpNormalBuffer[i] = MathX.LerpUnclamped(_normalBuffers[_buffIndex][i], _normalBuffers[j][i], t);
            }

            // Handle the position and rotation
            if (IsMine() && !IsBeingGrabbedByMe()) {
                Slot.GlobalPosition = MathX.LerpUnclamped(_states[_buffIndex].UnityPosition, _states[j].UnityPosition, t);
                Slot.GlobalRotation = MathX.LerpUnclamped(_states[_buffIndex].UnityRotation, _states[j].UnityRotation, t);
            }
            else {
                SetPosition(Slot.GlobalPosition);
                SetFaceAngle(Slot.GlobalRotation);
            }

            // Handle other synced params
            if (IsMine()) {

                // Handle health sync
                //spawnable.SetValue(_syncParameters[SyncedParameterNames.Health].Item1, _states[j].HealthPoints);

                // Handle controlling mario with camera mod sub-sync sync
                /*if (MarioCameraMod.Instance.IsControllingMario(this)) {
                    if (advancedOptions && cameraModTransform != null) {
                        var camTransform = MarioCameraMod.Instance.GetCameraTransform();
                        cameraModTransform.SetPositionAndRotation(camTransform.position, camTransform.rotation);
                    }
                }*/
            }
            else {
                //SetHealthPoints(_syncParameters[SyncedParameterNames.Health].Item2.currentValue);
            }

            // Handle local healthPoints param
            /*foreach (var animator in animators) {
                animator.SetInteger(LocalParameters[LocalParameterNames.HealthPoints], (int) _states[j].HealthPoints);
            }*/
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

        // If we're overriding the camera position transform use it instead.
        if (overrideCameraPosition && cameraPositionTransform != null) {
            return cameraPositionTransform.Target.Forward;
        }

        // If we're using the CVR Camera Mod
        if (controllingUser.Target == LocalUser) {
            /*if (MarioCameraMod.Instance.IsControllingMario(this)) {
                spawnable.SetValue(_syncParameters[SyncedParameterNames.HasCameraMod].Item1, 1f);
                return MarioCameraMod.Instance.GetCameraTransform().forward;
            }
            spawnable.SetValue(_syncParameters[SyncedParameterNames.HasCameraMod].Item1, 0f);
            */
        }

        // Use our own camera
        if (controllingUser.Target == LocalUser) {
            return LocalUser.Root.ViewRotation * float3.Forward;
        }

        // Use the remote player viewpoint. This value will be overwritten after with the prop face angle sync
        /*if (_ownerViewPoint) {
            return _ownerViewPoint.transform.forward;
        }
        */
        return float3.Zero;
    }

    readonly Sync<float2> joystick;
    readonly Sync<bool> jump;
    readonly Sync<bool> kick;
    readonly Sync<bool> stomp;

    private enum Button {
        Jump,
        Kick,
        Stomp,
    }

    private float2 GetJoystickAxes()
    {
        return joystick.Value;
    }

    private bool GetButtonHeld(Button button)
    {
        switch (button)
        {
            case Button.Jump:
                return jump;
            case Button.Kick:
                return kick;
            case Button.Stomp:
                return stomp;
        }
        return false;
    }


    public void SetIsOverMaxCount(bool isOverTheMaxCount) {
        _isOverMaxCount = isOverTheMaxCount;
        UpdateIsBypassed();
    }

    private void UpdateIsOverMaxDistance() {
        // Check the distance to see if we should ignore the updates
        _isOverMaxDistance =
            !IsMine()
            && MathX.Distance(Slot.GlobalPosition, LocalUserRoot.Slot.GlobalPosition) > _skipFarMarioDistance;
            //&& (!MarioCameraMod.IsControllingAMario(out var mario) || MathX.Distance(Slot.GlobalPosition, mario.transform.position) > _skipFarMarioDistance);
        UpdateIsBypassed();
    }

    private void UpdateIsBypassed() {
        if (!_initialized) return;
        var isBypassed = _isOverMaxDistance || _isOverMaxCount;
        if (isBypassed == _wasBypassed) return;
        _wasBypassed = isBypassed;

        // Handle local bypassed parameter
        /*foreach (var animator in animators) {
            animator.SetBool(LocalParameters[LocalParameterNames.IsBypassed], isBypassed);
        }*/

        // Enable/Disable the mario's mesh renderer
        _marioRendererObject.ActiveSelf = !isBypassed;
        //_marioMeshRenderer.Enabled = !isBypassed;
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
                // Original game is 15 seconds
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_VANISH_CAP, 40f, playMusic);
                break;
            case Utils.MarioCapType.MetalCap:
                // Originally game is 15 seconds
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_METAL_CAP, 40f, playMusic);
                break;
            case Utils.MarioCapType.WingCap:
                // Originally game is 40 seconds
                Interop.MarioCap(MarioId, FlagsFlags.MARIO_WING_CAP, 40f, playMusic);
                break;
        }
    }

    private bool IsBeingGrabbedByMe() {
        return false;
        //return _pickup != null && _pickup.IsGrabbedByMe();
    }

    public bool IsMine() => controllingUser.Target == LocalUser;//spawnable.IsMine();

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
                            $"{(deleteMario ? "delete the mario" : "stopping its engine updates")}.");
            #endif
            if (deleteMario) Destroy();
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

    //public List<Renderer> GetRenderersToHideFromCamera() => cameraModTransformRenderersToHide;

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
