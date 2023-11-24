using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace ResoniteMario64;

[Category("SM64")]
public class SM64Context {

    internal static SM64Context _instance = null;

    public World World;

    private readonly List<SM64Mario> _marios = new();
    //private readonly List<SM64ColliderDynamic> _surfaceObjects = new();

    // Audio
    private const int BufferSize = 544 * 2 * 2;
    private int _bufferPosition = BufferSize;
    private readonly short[] _audioBuffer = new short[BufferSize];
    private readonly float[] _processedAudioBuffer = new float[BufferSize];

    OpusStream<MonoSample> MarioAudio;
    CircularBufferWriteState<MonoSample> _writeState = default;


    protected SM64Context(World wld) {
        World = wld;

        Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
        /*ResoniteMario64.KEY_MAX_MESH_COLLIDER_TRIS.OnChanged += (newValue) => {
            Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
        };
        */
        QueueStaticSurfacesUpdate();
    }

    public SM64Mario AddMario(Slot root)
    {
        var mario = new SM64Mario(root);
        _instance._marios.Add(mario);
        if (ResoniteMario64.config.GetValue(ResoniteMario64.KEY_PLAY_RANDOM_MUSIC)) Interop.PlayRandomMusic();

        return mario;
    }

    public float2 joystick;
    public bool jump;
    public bool kick;
    public bool stomp;

    public Comment inputBlock;

    private void HandleInputs()
    {
        var root = World.LocalUser.Root;
        if (root == null) return;
        var loco = root.GetRegisteredComponent<LocomotionController>();
        if (loco == null) return;

        var inp = World.InputInterface;

        if (inp.VR_Active)
        {
            var main = World.LocalUser.GetInteractionHandler(World.LocalUser.Primaryhand);
            joystick = main.Inputs.Axis.CurrentValue;
            jump = (main.SharesUserspaceToggleAndMenus ? main.Inputs.Menu.Held : main.Inputs.UserspaceToggle.Held);
            stomp = main.Inputs.Grab.Held;
            kick = main.Inputs.Interact.Held;
        }
        else
        {
            var w = inp.GetKey(Key.W);
            var s = inp.GetKey(Key.S);
            var d = inp.GetKey(Key.D);
            var a = inp.GetKey(Key.A);
            joystick = GetDekstopJoystick(w, s, d, a);

            jump = inp.GetKey(Key.Space);
            stomp = inp.GetKey(Key.Shift);
            kick = inp.Mouse.LeftButton.Held;
        }


        if (inputBlock == null || inputBlock.IsRemoved)
        {
            var block = World.LocalUser.Root.Slot.GetComponentOrAttach<Comment>(c => c.Text.Value == "Mario64InputBlock");
            block.Text.Value = "Mario64InputBlock";
            inputBlock = block;
        }
        if (_marios.Count > 0 && !(inp.GetKey(Key.Control) || inp.VR_Active))
        {
            var currentBlock = loco.SupressSources.OfType<Comment>().FirstOrDefault(c => c.Text.Value == "Mario64InputBlock");
            if (currentBlock == null)
            {
                loco.SupressSources.Add(inputBlock);
            }
        }
        else
        {
            loco.SupressSources.RemoveAll(inputBlock);
        }
    }
    private float2 GetDekstopJoystick(bool up, bool down, bool left, bool right)
    {
        // prioritize Top Right when all inputs are held
        float vert = (up ? 1 : (down ? -1 : 0));
        float hori = (left ? 1 : (right ? -1 : 0));
        // could normalize but I think mario engine does it at some point down the line anyway
        return new float2(hori, vert);
    }

    [HarmonyPatch(typeof(InteractionHandler), "OnInputUpdate")]
    public class JumpInputBlocker
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var lookFor = AccessTools.Field(typeof(InteractionHandler), "_blockUserspaceOpen");
            foreach (var code in codes)
            {
                yield return code;
                if (code.LoadsField(lookFor))
                {
                    yield return new(OpCodes.Ldarg_0);
                    yield return new(OpCodes.Call, typeof(JumpInputBlocker).GetMethod(nameof(Injection)));
                }
            }
        }

        public static bool Injection(bool b, InteractionHandler c)
        {
            if (_instance?.World == c.World 
                && _instance._marios.Count > 0 
                && !c.SharesUserspaceToggleAndMenus
                && c.InputInterface.VR_Active
                && c.Side.Value == c.LocalUser.Primaryhand)
            {
                return true;
            }
            return b;
        }
    }


    private void ProcessAudio()
    {
        if (ResoniteMario64.config.GetValue(ResoniteMario64.KEY_DISABLE_AUDIO)) return;
        Interop.AudioTick(_audioBuffer, BufferSize);
        if (MarioAudio != null)
        {
            MarioAudio.Write(ConvertSamples(_audioBuffer, ResoniteMario64.config.GetValue(ResoniteMario64.KEY_AUDIO_PITCH)), ref _writeState);
        }
    }

    public static Span<MonoSample> ConvertSamples(short[] audioBuffer, float pitchShiftFactor)
    {
        int newLength = (int)(audioBuffer.Length / pitchShiftFactor);
        MonoSample[] shiftedAudio = new MonoSample[newLength];

        for (int i = 0; i < newLength; i++)
        {
            int index = (int)(i * pitchShiftFactor);

            if (index >= 0 && index < audioBuffer.Length)
            {
                // Linear interpolation
                float fraction = index % 1;
                int floorIndex = index;
                int ceilIndex = floorIndex + 1;

                float floorValue = MathX.Min((float)audioBuffer[floorIndex] / short.MaxValue, 1f);
                float ceilValue = MathX.Min((float)audioBuffer[ceilIndex] / short.MaxValue, 1f);

                float interpolatedValue = MathX.Lerp(floorValue, ceilValue, fraction);

                shiftedAudio[i] = interpolatedValue;
            }
        }

        return shiftedAudio.AsSpan();
    }


    private void SetAudioSource() {
        OpusStream<MonoSample> stream = World.LocalUser.GetStreamOrAdd<OpusStream<MonoSample>>("Mario64Audio", (s) =>
        {
            s.Name = "Mario64Audio";
        });
        MarioAudio = stream;
        var userSlot = World.LocalUser.Root.Slot;
        var audio = World.AddSlot("mario audio");
        var output = audio.AttachComponent<AudioOutput>();
        output.Source.Target = stream;
        output.SpatialBlend.Value = 0;
        output.Spatialize.Value = false;
        output.AudioTypeGroup.Value = AudioTypeGroup.Multimedia;
        output.Volume.Value = ResoniteMario64.config.GetValue(ResoniteMario64.KEY_AUDIO_VOLUME);
    }

    private void ProcessMoreSamples() {
        Interop.AudioTick(_audioBuffer, BufferSize);
        for (var i = 0; i < BufferSize; i++) {
            _processedAudioBuffer[i] = MathX.Min((float)_audioBuffer[i] / short.MaxValue, 1f);
        }
        _bufferPosition = 0;
    }

    // TODO: apparently this is just some unity function that allows you to insert audio somehow
    private void OnAudioFilterRead(float[] data, int channels) {

        // Disable audio, it can get annoying
        if (ResoniteMario64.config.GetValue(ResoniteMario64.KEY_DISABLE_AUDIO)) return;

        var samplesRemaining = data.Length;
        while (samplesRemaining > 0) {
            var samplesToCopy = MathX.Min(samplesRemaining, BufferSize - _bufferPosition);
            Array.Copy(_processedAudioBuffer, _bufferPosition, data, data.Length - samplesRemaining, samplesToCopy);
            _bufferPosition += samplesToCopy;
            samplesRemaining -= samplesToCopy;
            if (_bufferPosition >= BufferSize) {
                ProcessMoreSamples();
            }
        }
    }

    internal double LastTick;

    public void OnCommonUpdate()
    {
        HandleInputs();


        if (World.InputInterface.GetKeyDown(Key.Semicolon))
        {
            QueueStaticSurfacesUpdate();
        }

        if (World.Time.WorldTime - LastTick >= ResoniteMario64.config.GetValue(ResoniteMario64.KEY_GAME_TICK_MS) / 1000f)
        {
            SM64GameTick();
            LastTick = World.Time.WorldTime;
        }
        lock (_marios)
        {
            foreach (var o in _marios)
            {
                o.ContextUpdateSynced();
            }
        }
    }

    private void SM64GameTick() {
        ProcessAudio();

        /*lock (_surfaceObjects) {
            foreach (var o in _surfaceObjects) {
                o.ContextFixedUpdateSynced();
            }
        }
        */
        lock (_marios) {
            foreach (var o in _marios) {
                o.ContextFixedUpdateSynced(_marios);
            }
        }
    }

    public void DestroyInstance() {
        lock (_marios)
        {
            foreach (var o in _marios)
            {
                o.DestroyMario();
            }
        }

        Interop.GlobalTerminate();
        _instance = null;
    }

    public static bool EnsureInstanceExists(World wld) {
        if(_instance != null && wld != _instance.World)
        {
            var destroy = wld.Focus == World.WorldFocus.Focused;
            ResoniteMario64.Error("Tried to create instance while one already exists." + (destroy ? " It will be replaced by a new one." : ""));
            if (destroy) _instance.DestroyInstance();
            else return false;
        }
        if (_instance != null) return true;

        _instance = new SM64Context(wld);
        return true;
    }

    public static void QueueStaticSurfacesUpdate() {
        // TODO: implement buffer (so it will execute the update after 1.5s, and you can call it multiple times within that time)
        if (_instance == null) return;
        _instance.StaticTerrainUpdate();
    }

    private void StaticTerrainUpdate() {
        if (_instance == null) return;
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
    }

    public static void UnregisterMario(SM64Mario mario) {
        if (_instance == null) return;

        Interop.MarioDelete(mario.MarioId);

        lock (_instance._marios) {

            _instance._marios.Remove(mario);

            if (_instance._marios.Count == 0) {
                Interop.StopMusic();
            }
        }
    }

    /*public static bool RegisterSurfaceObject(SM64ColliderDynamic surfaceObject) {
        if (!EnsureInstanceExists(surfaceObject.World)) return false;

        lock (_instance._surfaceObjects) {
            if (!_instance._surfaceObjects.Contains(surfaceObject)) {
                _instance._surfaceObjects.Add(surfaceObject);
            }
        }
        return true;
    }

    public static void UnregisterSurfaceObject(SM64ColliderDynamic surfaceObject) {
        if (_instance == null) return;

        lock (_instance._surfaceObjects) {
            if (_instance._surfaceObjects.Contains(surfaceObject)) {
                _instance._surfaceObjects.Remove(surfaceObject);
            }
        }
    }*/
}
