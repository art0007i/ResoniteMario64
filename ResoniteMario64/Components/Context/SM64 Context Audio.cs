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
    private Slot AudioSlot;
    private OpusStream<StereoSample> _marioAudioStream;

    private CircularBufferWriteState<StereoSample> _writeState;

    private void SetAudioSource()
    {
        _marioAudioStream = CommonAvatarBuilder.GetStreamOrAdd<OpusStream<StereoSample>>(World.LocalUser, $"{AudioTag} - {World.LocalUser.UserID}", out bool created);
        if (created)
        {
            _marioAudioStream.Group = "SM64";
        }

        Slot userSlot = World.LocalUser.Root.Slot;
        userSlot.RunSynchronously(() =>
        {
            bool useLocalAudio = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyLocalAudio);
            float defaultVolume = ResoniteMario64.KeyAudioVolume.TryComputeDefaultTyped(out float defaultValue) ? defaultValue : 0f;
            if (useLocalAudio)
            {
                Slot localSlot = userSlot.FindLocalChildOrAdd(AudioSlotName);
                localSlot.Tag = AudioTag;

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
                }

                AudioSlot = localSlot;
                _marioAudioOutput = localAudio;
            }

            Slot globalSlot = ContextSlot?.FindChildOrAdd(AudioSlotName, false);
            AudioOutput globalAudio = null;
            if (globalSlot != null)
            {
                globalSlot.Tag = AudioTag;

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
                }
            }

            userSlot.RunInUpdates(userSlot.LocalUser.AllocationID + 1, () =>
            {
                float volume = useLocalAudio ? 0f : ResoniteMario64.Config.GetValue(ResoniteMario64.KeyAudioVolume);

                ValueUserOverride<float> overrideForUser = globalAudio?.Volume.OverrideForUser(userSlot.LocalUser, volume);
                if (overrideForUser != null)
                {
                    overrideForUser.Default.Value = defaultVolume;
                }
            });

            if (!useLocalAudio)
            {
                AudioSlot = globalSlot;
                _marioAudioOutput = globalAudio;
            }

            if (AudioSlot != null)
            {
                AudioSlot.OnPrepareDestroy -= HandleAudioDestroy;
                AudioSlot.OnPrepareDestroy += HandleAudioDestroy;
            }

            ResoniteMario64.KeyLocalAudio.OnChanged -= HandleLocalAudioChange;
            ResoniteMario64.KeyLocalAudio.OnChanged += HandleLocalAudioChange;

            ResoniteMario64.KeyDisableAudio.OnChanged -= HandleDisableChange;
            ResoniteMario64.KeyDisableAudio.OnChanged += HandleDisableChange;

            ResoniteMario64.KeyAudioVolume.OnChanged -= HandleVolumeChange;
            ResoniteMario64.KeyAudioVolume.OnChanged += HandleVolumeChange;
        });
    }

    private void HandleAudioDestroy(Slot slot)
    {
        if (Interop.IsGlobalInit)
        {
            slot.RunInUpdates(slot.LocalUser.AllocationID * 3, SetAudioSource);
        }
    }

    private void HandleVolumeChange(object value)
    {
        if (AudioSlot == null || _marioAudioOutput == null) return;

        if (AudioSlot.IsLocalElement)
        {
            _marioAudioOutput.Volume.Value = (float)value;
        }
        else
        {
            _marioAudioOutput.Volume.OverrideForUser(World.LocalUser, (float)value);
        }
    }

    private void HandleDisableChange(object value)
    {
        if (AudioSlot == null) return;

        if (AudioSlot.GetAllocatingUser() == World.LocalUser)
        {
            AudioSlot.Destroy();
        }
    }

    private void HandleLocalAudioChange(object value)
    {
        if (AudioSlot == null) return;

        if (AudioSlot.IsLocalElement)
        {
            AudioSlot.Destroy();
        }

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

        _marioAudioStream.Write(_convertedBuffer.AsSpan(0, written), ref _writeState);
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