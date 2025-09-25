using System;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;
using Elements.Assets;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Mario64.Components.Context;

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
                _marioAudioStream = CommonAvatarBuilder.GetStreamOrAdd<OpusStream<StereoSample>>(
                    World.LocalUser,
                    $"{AudioTag} - {World.LocalUser.UserID}",
                    out bool created);

                if (created)
                {
                    _marioAudioStream.Group = "SM64";
                }


                bool useLocalAudio = Config.LocalAudio.Value;
                float defaultVolume = (float)Config.AudioVolume.DefaultValue;


                if (useLocalAudio)
                {
                    Slot localSlot = World.LocalUser.Root.Slot.FindLocalChildOrAdd(AudioSlotName);
                    localSlot.Tag = AudioTag;

                    AudioOutput localAudio = localSlot.GetComponentOrAttach<AudioOutput>(out bool localAttached);
                    if (localAttached || localAudio.Source.Target == null)
                    {
                        localAudio.Source.Target = _marioAudioStream;
                        localAudio.Volume.Value = Config.AudioVolume.Value;
                        localAudio.SpatialBlend.Value = 0;
                        localAudio.Spatialize.Value = false;
                        localAudio.DopplerLevel.Value = 0;
                        localAudio.IgnoreAudioEffects.Value = true;
                        localAudio.AudioTypeGroup.Value = AudioTypeGroup.Multimedia;
                    }

                    _audioSlot = localSlot;
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
                else
                {
                    Logger.Warn("GlobalAudioSlot not found or ContextSlot is null");
                }

                if (!useLocalAudio)
                {
                    _audioSlot = globalSlot;
                    _marioAudioOutput = globalAudio;
                }

                World.RunInUpdates(World.LocalUser.AllocationID + 1, () =>
                {
                    float volume = useLocalAudio ? 0f : Config.AudioVolume.Value;

                    ValueUserOverride<float> overrideForUser = globalAudio?.Volume.OverrideForUser(World.LocalUser, volume);
                    if (overrideForUser != null)
                    {
                        overrideForUser.Default.Value = defaultVolume;
                    }
                });

                if (_audioSlot != null)
                {
                    _audioSlot.OnPrepareDestroy -= HandleAudioDestroy;
                    _audioSlot.OnPrepareDestroy += HandleAudioDestroy;
                }

                Config.LocalAudio.SettingChanged -= HandleLocalAudioChange;
                Config.LocalAudio.SettingChanged += HandleLocalAudioChange;

                Config.DisableAudio.SettingChanged -= HandleDisableChange;
                Config.DisableAudio.SettingChanged += HandleDisableChange;

                Config.AudioVolume.SettingChanged -= HandleVolumeChange;
                Config.AudioVolume.SettingChanged += HandleVolumeChange;
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"ERROR during SetAudioSource: {ex}");
        }
    }

    private void HandleAudioDestroy(Slot slot)
    {
        if (Interop.IsGlobalInit)
        {
            if (Config.LocalAudio.Value)
            {
                slot.StartTask(async () =>
                {
                    while (World?.LocalUser?.Root?.GetRegisteredComponent<AvatarManager>() == null)
                    {
                        await Task.Delay(10);
                    }
                    SetAudioSource();
                });
            }
            else
            {
                slot.RunInUpdates(slot.LocalUser.AllocationID + 3, SetAudioSource);
            }
        }
    }

    private void HandleVolumeChange(object value, EventArgs args)
    {
        if (_audioSlot == null || _marioAudioOutput == null)
        {
            return;
        }

        float volume = Config.AudioVolume.Value;

        if (_audioSlot.IsLocalElement)
        {
            _marioAudioOutput.Volume.Value = volume;
        }
        else
        {
            _marioAudioOutput.Volume.OverrideForUser(World.LocalUser, volume);
        }
    }

    private void HandleDisableChange(object value, EventArgs args)
    {
        if (_audioSlot == null)
        {
            return;
        }

        if (_audioSlot.GetAllocatingUser() == World.LocalUser)
        {
            _audioSlot.Destroy();
        }
    }

    private void HandleLocalAudioChange(object value, EventArgs args)
    {
        if (_audioSlot == null)
        {
            return;
        }
        if (_audioSlot.IsLocalElement)
        {
            _audioSlot.Destroy();
        }
        SetAudioSource();
    }
    
    private void ProcessAudio()
    {
        if (_marioAudioStream == null || Config.DisableAudio.Value)
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