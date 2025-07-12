using System;
using System.Diagnostics;
using Elements.Assets;
using FrooxEngine;
using ResoniteMario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Components.Context;

public partial class SM64Context
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
    private Slot _marioAudioSlot;
    private OpusStream<StereoSample> _marioAudioStream;

    private CircularBufferWriteState<StereoSample> _writeState;

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
}