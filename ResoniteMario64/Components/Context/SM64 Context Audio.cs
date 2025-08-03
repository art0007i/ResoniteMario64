using System;
using System.Diagnostics;
using Elements.Assets;
using FrooxEngine;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context
{
    private const int NativeSampleRate = 32000;
    private const int TargetSampleRate = 48000;

    private const int NativeBufferCount = 544 * 2;              // Random ahh numbers
    private const int NativeBufferSize = NativeBufferCount * 2; // Pt.2
    private const double AudioTickInterval = 1000.0 / 30.0;

    private readonly short[] _audioBuffer = new short[NativeBufferSize];

    private readonly Stopwatch _audioStopwatch = Stopwatch.StartNew();
    private readonly StereoSample[] _convertedBuffer = new StereoSample[(int)(NativeBufferSize * (TargetSampleRate / (float)NativeSampleRate))];
    private double _audioAccumulator;
    private AudioOutput _marioAudioOutput;
    private Slot AudioSlot;
    private OpusStream<StereoSample> _marioAudioStream;

    private CircularBufferWriteState<StereoSample> _writeState;

    private void SetAudioSource()
    {
        const string method = nameof(SetAudioSource);
        try
        {
            World.RunSynchronously(() =>
            {
                ResoniteMod.Msg($"[{method}] Starting AudioSource setup at {DateTime.Now:HH:mm:ss}");
                
                _marioAudioStream = CommonAvatarBuilder.GetStreamOrAdd<OpusStream<StereoSample>>(
                    World.LocalUser,
                    $"{AudioTag} - {World.LocalUser.UserID}",
                    out bool created);

                ResoniteMod.Msg($"[{method}] AudioStream {(created ? "created" : "reused")} for user {World.LocalUser.UserID}");

                if (created)
                {
                    _marioAudioStream.Group = "SM64";
                    ResoniteMod.Msg($"[{method}] AudioStream group set to SM64");
                }
            
            
                bool useLocalAudio = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyLocalAudio);
                float defaultVolume = ResoniteMario64.KeyAudioVolume.TryComputeDefaultTyped(out float defaultValue) ? defaultValue : 0f;

                ResoniteMod.Msg($"[{method}] useLocalAudio={useLocalAudio}, defaultVolume={defaultVolume}");

                if (useLocalAudio)
                {
                    ResoniteMod.Msg($"[{method}] Creating LocalAudioSlot");
                    Slot localSlot = World.LocalUser.Root.Slot.FindLocalChildOrAdd(AudioSlotName);
                    localSlot.Tag = AudioTag;

                    ResoniteMod.Msg($"[{method}] Attaching or getting LocalAudioOutput component");
                    AudioOutput localAudio = localSlot.GetComponentOrAttach<AudioOutput>(out bool localAttached);
                    if (localAttached || localAudio.Source.Target == null)
                    {
                        localAudio.Source.Target = _marioAudioStream;
                        localAudio.Volume.Value = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyAudioVolume);
                        localAudio.SpatialBlend.Value = 0;
                        localAudio.Spatialize.Value = false;
                        localAudio.DopplerLevel.Value = 0;
                        localAudio.IgnoreAudioEffects.Value = true;
                        localAudio.AudioTypeGroup.Value = AudioTypeGroup.Multimedia;

                        ResoniteMod.Msg($"[{method}] LocalAudioOutput configured");
                    }

                    AudioSlot = localSlot;
                    _marioAudioOutput = localAudio;

                    ResoniteMod.Msg($"[{method}] LocalAudioSlot and AudioOutput set");
                }

                ResoniteMod.Msg($"[{method}] Creating GlobalAudioSlot");
                Slot globalSlot = ContextSlot?.FindChildOrAdd(AudioSlotName, false);
                AudioOutput globalAudio = null;

                if (globalSlot != null)
                {
                    globalSlot.Tag = AudioTag;

                    ResoniteMod.Msg($"[{method}] Attaching or getting GlobalAudioOutput component");
                    globalAudio = globalSlot.GetComponentOrAttach<AudioOutput>(out bool globalAttached);
                    if (globalAttached || globalAudio.Source.Target == null)
                    {
                        globalAudio.Source.Target = _marioAudioStream;
                        globalAudio.Volume.Value = defaultVolume;
                        globalAudio.SpatialBlend.Value = 0;
                        globalAudio.Spatialize.Value = false;
                        globalAudio.DopplerLevel.Value = 0;
                        globalAudio.IgnoreAudioEffects.Value = true;
                        globalAudio.AudioTypeGroup.Value = AudioTypeGroup.Multimedia;

                        ResoniteMod.Msg($"[{method}] GlobalAudioOutput configured");
                    }

                    ResoniteMod.Msg($"[{method}] GlobalAudioSlot ready");
                }
                else
                {
                    ResoniteMod.Msg($"[{method}] GlobalAudioSlot not found or ContextSlot is null");
                }

                if (!useLocalAudio)
                {
                    AudioSlot = globalSlot;
                    _marioAudioOutput = globalAudio;
                    ResoniteMod.Msg($"[{method}] Using GlobalAudioSlot and AudioOutput");
                }

                World.RunInUpdates(World.LocalUser.AllocationID + 1, () =>
                {
                    float volume = useLocalAudio ? 0f : ResoniteMario64.Config.GetValue(ResoniteMario64.KeyAudioVolume);
                    ResoniteMod.Msg($"[{method}] Overriding volume for user: {World.LocalUser.UserID} to {volume}");

                    ValueUserOverride<float> overrideForUser = globalAudio?.Volume.OverrideForUser(World.LocalUser, volume);
                    if (overrideForUser != null)
                    {
                        overrideForUser.Default.Value = defaultVolume;
                        ResoniteMod.Msg($"[{method}] Override default volume set to {defaultVolume}");
                    }
                    else
                    {
                        ResoniteMod.Msg($"[{method}] No override created for volume");
                    }
                });

                ResoniteMod.Msg($"[{method}] Subscribing event handlers");

                if (AudioSlot != null)
                {
                    AudioSlot.OnPrepareDestroy -= HandleAudioDestroy;
                    AudioSlot.OnPrepareDestroy += HandleAudioDestroy;
                    ResoniteMod.Msg($"[{method}] Subscribed to AudioSlot.OnPrepareDestroy");
                }
                else
                {
                    ResoniteMod.Msg($"[{method}] AudioSlot is null, skipping OnPrepareDestroy subscription");
                }

                ResoniteMario64.KeyLocalAudio.OnChanged -= HandleLocalAudioChange;
                ResoniteMario64.KeyLocalAudio.OnChanged += HandleLocalAudioChange;

                ResoniteMario64.KeyDisableAudio.OnChanged -= HandleDisableChange;
                ResoniteMario64.KeyDisableAudio.OnChanged += HandleDisableChange;

                ResoniteMario64.KeyAudioVolume.OnChanged -= HandleVolumeChange;
                ResoniteMario64.KeyAudioVolume.OnChanged += HandleVolumeChange;

                ResoniteMod.Msg($"[{method}] Event handlers subscribed");
                ResoniteMod.Msg($"[{method}] AudioSource setup finished at {DateTime.Now:HH:mm:ss.fff}");
            });
        }
        catch (Exception ex)
        {
            ResoniteMod.Msg($"[{method}] ERROR during SetAudioSource: {ex}");
        }
    }

    private void HandleAudioDestroy(Slot slot)
    {
        const string method = nameof(HandleAudioDestroy);
        ResoniteMod.Msg($"[{method}] AudioSlot is being destroyed, checking if global init");

        if (Interop.IsGlobalInit)
        {
            ResoniteMod.Msg($"[{method}] GlobalInit is true, scheduling SetAudioSource");
            slot.RunInUpdates(slot.LocalUser.AllocationID * 3, SetAudioSource);
        }
        else
        {
            ResoniteMod.Msg($"[{method}] GlobalInit is false, skipping SetAudioSource");
        }
    }

    private void HandleVolumeChange(object value)
    {
        const string method = nameof(HandleVolumeChange);

        if (AudioSlot == null || _marioAudioOutput == null)
        {
            ResoniteMod.Msg($"[{method}] AudioSlot or _marioAudioOutput is null, ignoring volume change");
            return;
        }

        float volume = (float)value;
        ResoniteMod.Msg($"[{method}] Volume change requested: {volume}");

        if (AudioSlot.IsLocalElement)
        {
            _marioAudioOutput.Volume.Value = volume;
            ResoniteMod.Msg($"[{method}] Volume set directly on local AudioOutput");
        }
        else
        {
            _marioAudioOutput.Volume.OverrideForUser(World.LocalUser, volume);
            ResoniteMod.Msg($"[{method}] Volume overridden for user {World.LocalUser.UserID}");
        }
    }

    private void HandleDisableChange(object value)
    {
        const string method = nameof(HandleDisableChange);

        if (AudioSlot == null)
        {
            ResoniteMod.Msg($"[{method}] AudioSlot is null, ignoring disable change");
            return;
        }

        if (AudioSlot.GetAllocatingUser() == World.LocalUser)
        {
            ResoniteMod.Msg($"[{method}] Disabling audio, destroying AudioSlot");
            AudioSlot.Destroy();
        }
        else
        {
            ResoniteMod.Msg($"[{method}] Allocating user is not local user, no action taken");
        }
    }

    private void HandleLocalAudioChange(object value)
    {
        const string method = nameof(HandleLocalAudioChange);

        if (AudioSlot == null)
        {
            ResoniteMod.Msg($"[{method}] AudioSlot is null, cannot change local audio");
            return;
        }

        if (AudioSlot.IsLocalElement)
        {
            ResoniteMod.Msg($"[{method}] Local audio slot detected, destroying it");
            AudioSlot.Destroy();
        }

        ResoniteMod.Msg($"[{method}] Reinitializing audio source after local audio change");
        SetAudioSource();
    }

    private void ProcessAudio()
    {
        if (_marioAudioStream == null || ResoniteMario64.Config.GetValue(ResoniteMario64.KeyDisableAudio))
        {
            return;
        }

        double elapsed = _audioStopwatch.Elapsed.TotalMilliseconds;
        _audioStopwatch.Restart();
        _audioAccumulator += elapsed;

        if (_audioAccumulator > AudioTickInterval * 4)
        {
            _audioAccumulator = AudioTickInterval * 4;
        }

        if (_audioAccumulator < AudioTickInterval) return;

        _audioAccumulator -= AudioTickInterval;

        Interop.AudioTick(_audioBuffer, (uint)_marioAudioStream.FrameSize);

        int written = DownmixAndResampleStereo(
            _audioBuffer,
            NativeSampleRate,
            TargetSampleRate,
            _convertedBuffer
        );

        if (written <= 0) return;
        if (written > _marioAudioStream.CurrentBufferSize - _marioAudioStream.SamplesAvailableForEncode) return;
        
        const double rate = (double)TargetSampleRate / NativeSampleRate;
        int available = _marioAudioStream.CurrentBufferSize - _marioAudioStream.SamplesAvailableForEncode;

        int estimatedPutCount = (int)Math.Ceiling(_convertedBuffer.Length / rate);
        if (estimatedPutCount > available)
        {
            ResoniteMod.Debug($"[Audio] Skipping write. Needed {estimatedPutCount}, available {available}");
            return;
        }

        Span<StereoSample> writeSpan = _convertedBuffer.AsSpan(0, written);
        _marioAudioStream.Write(writeSpan, ref _writeState);
    }

    // private static int DownmixAndResampleMono(short[] input, float inputRate, float outputRate, MonoSample[] output)
    // {
    //     float ratio = inputRate / outputRate;
    //     float pos = 0.0f;
    //     int outputIndex = 0;
    // 
    //     while ((int)pos * 2 + 3 < input.Length && outputIndex < output.Length)
    //     {
    //         int i = (int)pos * 2;
    // 
    //         float l1 = input[i] / 32768.0f;
    //         float r1 = input[i + 1] / 32768.0f;
    //         float l2 = input[i + 2] / 32768.0f;
    //         float r2 = input[i + 3] / 32768.0f;
    // 
    //         float t = pos - (int)pos;
    // 
    //         float s1 = (l1 + r1) * 0.5f;
    //         float s2 = (l2 + r2) * 0.5f;
    // 
    //         float sample = s1 * (1 - t) + s2 * t;
    // 
    //         output[outputIndex++] = new MonoSample(sample);
    //         pos += ratio;
    //     }
    // 
    //     return outputIndex;
    // }

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
}