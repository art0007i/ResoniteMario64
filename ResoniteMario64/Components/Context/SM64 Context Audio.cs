using System;
using System.Diagnostics;
using Elements.Assets;
using FrooxEngine;
using ResoniteMario64.libsm64;
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
    private Slot _audioSlot;
    private OpusStream<StereoSample> _marioAudioStream;

    private CircularBufferWriteState<StereoSample> _writeState;

    private void SetAudioSource()
    {
        try
        {
            World.RunSynchronously(() =>
            {
                Logger.Msg($"Starting AudioSource setup at {DateTime.Now:HH:mm:ss}");

                _marioAudioStream = CommonAvatarBuilder.GetStreamOrAdd<OpusStream<StereoSample>>(
                    World.LocalUser,
                    $"{AudioTag} - {World.LocalUser.UserID}",
                    out bool created);

                Logger.Msg($"AudioStream {(created ? "created" : "reused")} for user {World.LocalUser.UserID}");

                if (created)
                {
                    _marioAudioStream.Group = "SM64";
                    Logger.Msg("AudioStream group set to SM64");
                }


                bool useLocalAudio = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyLocalAudio);
                float defaultVolume = ResoniteMario64.KeyAudioVolume.TryComputeDefaultTyped(out float defaultValue) ? defaultValue : 0f;

                Logger.Msg($"useLocalAudio={useLocalAudio}, defaultVolume={defaultVolume}");

                if (useLocalAudio)
                {
                    Logger.Msg("Creating LocalAudioSlot");
                    Slot localSlot = World.LocalUser.Root.Slot.FindLocalChildOrAdd(AudioSlotName);
                    localSlot.Tag = AudioTag;

                    Logger.Msg("Attaching or getting LocalAudioOutput component");
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

                        Logger.Msg("LocalAudioOutput configured");
                    }

                    _audioSlot = localSlot;
                    _marioAudioOutput = localAudio;

                    Logger.Msg("LocalAudioSlot and AudioOutput set");
                }

                Logger.Msg("Creating GlobalAudioSlot");
                Slot globalSlot = ContextSlot?.FindChildOrAdd(AudioSlotName, false);
                AudioOutput globalAudio = null;

                if (globalSlot != null)
                {
                    globalSlot.Tag = AudioTag;

                    Logger.Msg("Attaching or getting GlobalAudioOutput component");
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

                        Logger.Msg("GlobalAudioOutput configured");
                    }

                    Logger.Msg("GlobalAudioSlot ready");
                }
                else
                {
                    Logger.Msg("GlobalAudioSlot not found or ContextSlot is null");
                }

                if (!useLocalAudio)
                {
                    _audioSlot = globalSlot;
                    _marioAudioOutput = globalAudio;
                    Logger.Msg("Using GlobalAudioSlot and AudioOutput");
                }

                World.RunInUpdates(World.LocalUser.AllocationID + 1, () =>
                {
                    float volume = useLocalAudio ? 0f : ResoniteMario64.Config.GetValue(ResoniteMario64.KeyAudioVolume);
                    Logger.Msg($"Overriding volume for user: {World.LocalUser.UserID} to {volume}");

                    ValueUserOverride<float> overrideForUser = globalAudio?.Volume.OverrideForUser(World.LocalUser, volume);
                    if (overrideForUser != null)
                    {
                        overrideForUser.Default.Value = defaultVolume;
                        Logger.Msg($"Override default volume set to {defaultVolume}");
                    }
                    else
                    {
                        Logger.Msg("No override created for volume");
                    }
                });

                Logger.Msg("Subscribing event handlers");

                if (_audioSlot != null)
                {
                    _audioSlot.OnPrepareDestroy -= HandleAudioDestroy;
                    _audioSlot.OnPrepareDestroy += HandleAudioDestroy;
                    Logger.Msg("Subscribed to AudioSlot.OnPrepareDestroy");
                }
                else
                {
                    Logger.Msg("AudioSlot is null, skipping OnPrepareDestroy subscription");
                }

                ResoniteMario64.KeyLocalAudio.OnChanged -= HandleLocalAudioChange;
                ResoniteMario64.KeyLocalAudio.OnChanged += HandleLocalAudioChange;

                ResoniteMario64.KeyDisableAudio.OnChanged -= HandleDisableChange;
                ResoniteMario64.KeyDisableAudio.OnChanged += HandleDisableChange;

                ResoniteMario64.KeyAudioVolume.OnChanged -= HandleVolumeChange;
                ResoniteMario64.KeyAudioVolume.OnChanged += HandleVolumeChange;

                Logger.Msg("Event handlers subscribed");
                Logger.Msg($"AudioSource setup finished at {DateTime.Now:HH:mm:ss.fff}");
            });
        }
        catch (Exception ex)
        {
            Logger.Msg($"ERROR during SetAudioSource: {ex}");
        }
    }

    private void HandleAudioDestroy(Slot slot)
    {
        Logger.Msg("AudioSlot is being destroyed, checking if global init");

        if (Interop.IsGlobalInit)
        {
            Logger.Msg("GlobalInit is true, scheduling SetAudioSource");
            slot.RunInUpdates(slot.LocalUser.AllocationID * 3, SetAudioSource);
        }
        else
        {
            Logger.Msg("GlobalInit is false, skipping SetAudioSource");
        }
    }

    private void HandleVolumeChange(object value)
    {
        if (_audioSlot == null || _marioAudioOutput == null)
        {
            Logger.Msg("AudioSlot or _marioAudioOutput is null, ignoring volume change");
            return;
        }

        float volume = (float)value;
        Logger.Msg($"Volume change requested: {volume}");

        if (_audioSlot.IsLocalElement)
        {
            _marioAudioOutput.Volume.Value = volume;
            Logger.Msg("Volume set directly on local AudioOutput");
        }
        else
        {
            _marioAudioOutput.Volume.OverrideForUser(World.LocalUser, volume);
            Logger.Msg($"Volume overridden for user {World.LocalUser.UserID}");
        }
    }

    private void HandleDisableChange(object value)
    {
        if (_audioSlot == null)
        {
            Logger.Msg("AudioSlot is null, ignoring disable change");
            return;
        }

        if (_audioSlot.GetAllocatingUser() == World.LocalUser)
        {
            Logger.Msg("Disabling audio, destroying AudioSlot");
            _audioSlot.Destroy();
        }
        else
        {
            Logger.Msg("Allocating user is not local user, no action taken");
        }
    }

    private void HandleLocalAudioChange(object value)
    {
        if (_audioSlot == null)
        {
            Logger.Msg("AudioSlot is null, cannot change local audio");
            return;
        }

        if (_audioSlot.IsLocalElement)
        {
            Logger.Msg("Local audio slot detected, destroying it");
            _audioSlot.Destroy();
        }

        Logger.Msg("Reinitializing audio source after local audio change");
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
            Logger.Debug($"[Audio] Skipping write. Needed {estimatedPutCount}, available {available}");
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