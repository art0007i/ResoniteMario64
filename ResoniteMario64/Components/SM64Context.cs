using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.libsm64;
using static ResoniteMario64.Consts;

namespace ResoniteMario64.Components;

public class SM64Context : IDisposable
{
    // private readonly List<SM64ColliderDynamic> _surfaceObjects = new();
    public static SM64Context Instance;
    
#region Audio

    private Slot _marioAudioSlot;
    private OpusStream<StereoSample> _marioAudioStream;
    private AudioOutput _marioAudioOutput;
    
    private const int NativeSampleRate = 32000;
    private const int TargetSampleRate = 48000;

    private const int NativeBufferCount = 544 * 2;             // Random ahh numbers
    private const int NativeBufferSize = NativeBufferCount * 2;// Pt.2

    private readonly short[] _audioBuffer = new short[NativeBufferSize];
    private readonly StereoSample[] _convertedBuffer = new StereoSample[(int)(NativeBufferSize * (TargetSampleRate / (float)NativeSampleRate))];

    private CircularBufferWriteState<StereoSample> _writeState;
    
    private readonly Stopwatch _audioStopwatch = Stopwatch.StartNew();
    private const double AudioTickInterval = 1000.0 / 30.0;
    private double _audioAccumulator = 0.0;

#endregion
    
    public readonly Dictionary<Slot, SM64Mario> Marios = new Dictionary<Slot, SM64Mario>();
    
    public Comment InputBlock;

    public float2 Joystick;
    public bool Jump;
    public bool Kick;
    public bool Stomp;

    internal double LastTick;
    
    public World World;

    private SM64Context(World wld)
    {
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

    public static SM64Mario AddMario(Slot root, bool isButton = false)
    {
        SM64Mario mario = null;
        
        if (EnsureInstanceExists(root.World))
        {
            mario = new SM64Mario(root, isButton);
            Instance.Marios.Add(root, mario);
            if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayRandomMusic)) Interop.PlayRandomMusic();
        }

        return mario;
    }

    public static bool TryAddMario(Slot root, bool isButton = false) => AddMario(root, isButton) != null;

    private void HandleInputs()
    {
        LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
        if (loco == null) return;

        InputInterface inp = World.InputInterface;
        if (inp.VR_Active)
        {
            InteractionHandler main = World.LocalUser.GetInteractionHandler(World.LocalUser.Primaryhand);
            InteractionHandler off = main.OtherTool;

            Joystick = off.Inputs.Axis.CurrentValue;
            Jump = main.SharesUserspaceToggleAndMenus ? main.Inputs.Menu.Held : main.Inputs.UserspaceToggle.Held;
            Stomp = main.Inputs.Grab.Held;
            Kick = main.Inputs.Interact.Held;
        }
        else
        {
            bool w = inp.GetKey(Key.W);
            bool s = inp.GetKey(Key.S);
            bool d = inp.GetKey(Key.D);
            bool a = inp.GetKey(Key.A);

            Joystick = GetDesktopJoystick(w, s, d, a);
            Jump = inp.GetKey(Key.Space);
            Stomp = inp.GetKey(Key.Shift);
            Kick = inp.Mouse.LeftButton.Held;
        }
        
        if (InputBlock == null || InputBlock.IsRemoved)
        {
            Comment block = World.LocalUser.Root.Slot.GetComponentOrAttach<Comment>(c => c.Text.Value == $"SM64 {InputBlockTag}");
            block.Text.Value = $"SM64 {InputBlockTag}";
            InputBlock = block;
        }

        if (Marios.Count > 0 && !(inp.GetKey(Key.Control) || inp.VR_Active))
        {
            Comment currentBlock = loco.SupressSources.OfType<Comment>().FirstOrDefault(c => c.Text.Value == $"SM64 {InputBlockTag}");
            if (currentBlock == null)
            {
                loco.SupressSources.Add(InputBlock);
            }
        }
        else
        {
            loco.SupressSources.RemoveAll(InputBlock);
        }
    }

    private float2 GetDesktopJoystick(bool up, bool down, bool left, bool right)
    {
        float vert = up ? 1 : down ? -1 : 0;
        float hori = left ? 1 : right ? -1 : 0;

        float2 input = new float2(hori, vert);

        float length = MathX.Sqrt(input.x * input.x + input.y * input.y);
        return length > 1.0f
                ? new float2(input.x / length, input.y / length)
                : input;
    }

    private static bool ShouldblockInputs(InteractionHandler c, Chirality hand) => Instance?.World == c.World && Instance.Marios.Count > 0 && c.InputInterface.VR_Active && c.Side.Value == hand;

    private void ProcessAudio()
    {
        if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyDisableAudio)) return;
        if (_marioAudioStream == null) return;

        double elapsed = _audioStopwatch.Elapsed.TotalMilliseconds;
        _audioStopwatch.Restart();
        _audioAccumulator += elapsed;

        // Clamp accumulator to avoid runaway overflow
        if (_audioAccumulator > AudioTickInterval * 4)
        {
            _audioAccumulator = AudioTickInterval * 4;
        }

        // Run at 30Hz max
        if (_audioAccumulator >= AudioTickInterval)
        {
            _audioAccumulator -= AudioTickInterval;

            Interop.AudioTick(_audioBuffer, (uint)(NativeBufferCount / _marioAudioStream.FrameSize * _marioAudioStream.FrameSize));

            int written = DownmixAndResampleStereo(
                _audioBuffer,
                NativeSampleRate,
                TargetSampleRate,
                _convertedBuffer
            );

            if (written <= 0) return;
            if (_marioAudioStream.CurrentBufferSize - _marioAudioStream.SamplesAvailableForEncode < written)
            {
                return;
            }

            _marioAudioStream.Write(_convertedBuffer.AsSpan(0, written), ref _writeState);
        }
    }

    private static int DownmixAndResampleMono(short[] input, float inputRate, float outputRate, MonoSample[] output)
    {
        float ratio = inputRate / outputRate;
        float pos = 0.0f;
        int outputIndex = 0;

        while ((int)pos * 2 + 3 < input.Length && outputIndex < output.Length)
        {
            int i = (int)pos * 2;

            float l1 = input[i] / 32768.0f;
            float r1 = input[i + 1] / 32768.0f;
            float l2 = input[i + 2] / 32768.0f;
            float r2 = input[i + 3] / 32768.0f;

            float t = pos - (int)pos;

            float s1 = (l1 + r1) * 0.5f;
            float s2 = (l2 + r2) * 0.5f;

            float sample = s1 * (1 - t) + s2 * t;

            output[outputIndex++] = new MonoSample(sample);
            pos += ratio;
        }

        return outputIndex;
    }
    
    private static int DownmixAndResampleStereo(short[] input, float inputRate, float outputRate, StereoSample[] output)
    {
        float ratio = inputRate / outputRate;
        float pos = 0.0f;
        int outputIndex = 0;

        while ((int)pos * 2 + 3 < input.Length && outputIndex < output.Length)
        {
            int i = (int)pos * 2;

            float l1 = input[i] / 32768.0f;
            float r1 = input[i + 1] / 32768.0f;
            float l2 = input[i + 2] / 32768.0f;
            float r2 = input[i + 3] / 32768.0f;

            float t = pos - (int)pos;

            float left = l1 * (1 - t) + l2 * t;
            float right = r1 * (1 - t) + r2 * t;

            output[outputIndex++] = new StereoSample(left, right);
            pos += ratio;
        }

        return outputIndex;
    }

    private void SetAudioSource()
    {
        _marioAudioStream = CommonAvatarBuilder.GetStreamOrAdd<OpusStream<StereoSample>>(World.LocalUser, $"SM64 {AudioTag}", out bool created);
        if (created)
        {
            _marioAudioStream.Group = "SM64";
        }
        Slot userSlot = World.LocalUser.Root.Slot;
        userSlot.RunSynchronously(() =>
        {
            _marioAudioSlot = World.RootSlot.FindChildOrAdd($"SM64 {AudioTag}", false);
            _marioAudioSlot.Tag = $"SM64 {AudioTag}";
            _marioAudioSlot.OnPrepareDestroy += slot =>
            {
                if (Interop.IsGlobalInit)
                {
                    slot.RunInUpdates(slot.LocalUser.AllocationID * 3, SetAudioSource);
                }
            };
            
            _marioAudioOutput = _marioAudioSlot.GetComponentOrAttach<AudioOutput>(out bool attached);
            if (attached || _marioAudioOutput.Source.Target == null)
            {
                _marioAudioOutput.Source.Target = _marioAudioStream;
                _marioAudioOutput.SpatialBlend.Value = 0;
                _marioAudioOutput.Spatialize.Value = false;
                _marioAudioOutput.DopplerLevel.Value = 0;
                _marioAudioOutput.IgnoreAudioEffects.Value = true;
                _marioAudioOutput.AudioTypeGroup.Value = AudioTypeGroup.Multimedia;
                _marioAudioOutput.Volume.Value = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyAudioVolume);
            }
        });
    }

    public void OnCommonUpdate()
    {
        HandleInputs();

        if (World.InputInterface.GetKeyDown(Key.Semicolon))
        {
            QueueStaticSurfacesUpdate();
        }

        if (World.Time.WorldTime - LastTick >= ResoniteMario64.Config.GetValue(ResoniteMario64.KeyGameTickMs) / 1000f)
        {
            SM64GameTick();
            LastTick = World.Time.WorldTime;
        }

        Dictionary<Slot, SM64Mario> marios = new Dictionary<Slot, SM64Mario>(Marios);
        foreach (SM64Mario o in marios.Values)
        {
            o.ContextUpdateSynced();
        }
    }

    private void SM64GameTick()
    {
        ProcessAudio();

        /*lock (_surfaceObjects) {
            foreach (var o in _surfaceObjects) {
                o.ContextFixedUpdateSynced();
            }
        }
        */

        Dictionary<Slot, SM64Mario> marios = new Dictionary<Slot, SM64Mario>(Marios);
        foreach (SM64Mario o in marios.Values)
        {
            o.ContextFixedUpdateSynced();
        }
    }

    private static bool EnsureInstanceExists(World wld)
    {
        if (Instance != null && wld != Instance.World)
        {
            bool destroy = wld.Focus == World.WorldFocus.Focused;
            ResoniteMario64.Error("Tried to create instance while one already exists." + (destroy ? " It will be replaced by a new one." : ""));
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

        Instance = new SM64Context(wld);
        return true;
    }

    public static void QueueStaticSurfacesUpdate()
    {
        // TODO: implement buffer (so it will execute the update after 1.5s, and you can call it multiple times within that time)
        if (Instance == null) return;
        Instance.StaticTerrainUpdate();
    }

    private void StaticTerrainUpdate()
    {
        if (Instance == null) return;
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
    }

    public static void UnregisterMario(SM64Mario mario)
    {
        if (Instance == null) return;

        Interop.MarioDelete(mario.MarioId);
        
        Instance.Marios.Remove(mario.MarioSlot);

        if (Instance.Marios.Count == 0)
        {
            Interop.StopMusic();
        }
    }

    [HarmonyPatch(typeof(InteractionHandler), "OnInputUpdate")]
    public class JumpInputBlocker
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            FieldInfo lookFor = AccessTools.Field(typeof(InteractionHandler), "_blockUserspaceOpen");
            foreach (CodeInstruction code in codes)
            {
                yield return code;
                if (code.LoadsField(lookFor))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(JumpInputBlocker).GetMethod(nameof(Injection)));
                }
            }
        }

        public static bool Injection(bool b, InteractionHandler c) => ShouldblockInputs(c, c.LocalUser.Primaryhand) || b;
    }

    [HarmonyPatch(typeof(InteractionHandler), nameof(InteractionHandler.BeforeInputUpdate))]
    public class MarioInputBlocker
    {
        public static void Postfix(InteractionHandler __instance)
        {
            if (ShouldblockInputs(__instance, __instance.LocalUser.Primaryhand.GetOther()))
            {
                __instance.Inputs.Axis.RegisterBlocks = true;
            }
            /*if (ShouldblockInputs(__instance, __instance.LocalUser.Primaryhand))
            {
                __instance.Inputs.UserspaceToggle.RegisterBlocks = true;
                __instance.Inputs.Interact.RegisterBlocks = true;
                __instance.Inputs.Grab.RegisterBlocks = true;
            }*/
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
    public void Dispose()
    {
        Dictionary<Slot, SM64Mario> marios = new Dictionary<Slot, SM64Mario>(Marios);
        foreach (SM64Mario o in marios.Values)
        {
            o.Dispose();
        }
        
        Interop.GlobalTerminate();
        
        LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
        loco?.SupressSources?.RemoveAll(InputBlock);
        
        _marioAudioSlot?.Destroy();

        Instance = null;
    }
}