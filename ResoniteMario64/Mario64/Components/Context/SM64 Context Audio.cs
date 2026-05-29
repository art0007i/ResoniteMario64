using System.Diagnostics;
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

    private readonly Stopwatch _audioStopwatch = new Stopwatch();
    private readonly StereoSample[] _convertedBuffer = new StereoSample[(int)(NativeBufferSize * (TargetSampleRate / (float)NativeSampleRate))];
    private double _audioAccumulator;
    private AudioOutput _marioAudioOutput;
    private Slot _audioSlot;
    private OpusStream<StereoSample> _marioAudioStream;
    private Thread _audioThread;
    private volatile bool _audioThreadRunning;

    private CircularBufferWriteState<StereoSample> _writeState;

    private void SetAudioSource()
    {
        try
        {
            World.RunSynchronously(() =>
            {
                _marioAudioStream = CommonAvatarBuilder.GetStreamOrAdd<OpusStream<StereoSample>>(World.LocalUser, $"{AudioTag} - {World.LocalUser.UserID}", out bool created);

                if (created)
                {
                    _marioAudioStream.Group = "SM64";
                }


                bool useLocalAudio = Config.LocalAudio.Value;
                float defaultVolume = (float)Config.AudioVolume.DefaultValue;


                Slot localSlot = null;
                AudioOutput localAudio = null;
                if (useLocalAudio)
                {
                    localSlot = World.LocalUser.Root.Slot.FindLocalChildOrAdd(AudioSlotName);
                    localSlot.Tag = AudioTag;

                    localAudio = localSlot.GetComponentOrAttach<AudioOutput>(out bool localAttached);
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
                        overrideForUser.CreateOverrideOnWrite.Value = true;
                    }
                });

                DynamicField<float> floatField = globalSlot?.GetComponentOrAttach<DynamicField<float>>();
                if (floatField != null)
                {
                    floatField.VariableName.Value = "VolumeLevel";
                    floatField.TargetField.Target = globalAudio?.Volume;
                }

                ValueEqualityDriver<float> valEqual = globalSlot?.GetComponentOrAttach<ValueEqualityDriver<float>>();
                if (valEqual != null)
                {
                    valEqual.TargetValue.Target = globalAudio.Volume;
                    valEqual.Target.Target = globalAudio.EnabledField;
                    valEqual.Invert.Value = true;
                }

                ValueEqualityDriver<float> localValEqual = localSlot?.GetComponentOrAttach<ValueEqualityDriver<float>>();
                if (localValEqual != null)
                {
                    localValEqual.TargetValue.Target = globalAudio?.Volume;
                    localValEqual.Target.Target = localAudio.EnabledField;
                }

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

    private void StartAudioThread()
    {
        if (_audioThread != null)
        {
            return;
        }

        _audioThreadRunning = true;
        _audioThread = new Thread(AudioThreadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "SM64 Audio"
        };
        _audioThread.Start();
    }

    private void StopAudioThread()
    {
        _audioThreadRunning = false;

        Thread audioThread = _audioThread;
        if (audioThread != null && audioThread.IsAlive && audioThread != Thread.CurrentThread)
        {
            audioThread.Join();
        }

        _audioThread = null;
    }

    private void AudioThreadLoop()
    {
        while (_audioThreadRunning)
        {
            Stopwatch tickStopwatch = Stopwatch.StartNew();
            ProcessAudio();

            long targetTicks = (long)(Config.GameTickMs.Value * Stopwatch.Frequency / 1000.0);
            if (targetTicks < 1)
            {
                targetTicks = 1;
            }

            while (_audioThreadRunning && tickStopwatch.ElapsedTicks < targetTicks)
            {
                long remainingTicks = targetTicks - tickStopwatch.ElapsedTicks;
                if (remainingTicks > Stopwatch.Frequency / 1000)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(64);
                }
            }
        }
    }

    private void HandleAudioDestroy(Slot slot)
    {
        if (SM64Interop.IsGlobalInit)
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

        _audioSlot.RunSynchronously(() =>
        {
            if (_audioSlot.IsLocalElement)
            {
                _marioAudioOutput.Volume.Value = volume;
            }
            else
            {
                _marioAudioOutput.Volume.OverrideForUser(World.LocalUser, volume);
            }
        }, true);
    }

    private void HandleDisableChange(object value, EventArgs args)
    {
        if (_audioSlot == null)
        {
            return;
        }

        if (_audioSlot.GetAllocatingUser() == World.LocalUser)
        {
            _audioSlot.SafeDestroy();
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
            _audioSlot.SafeDestroy();
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

        float bufferFullness = (float)_marioAudioStream.UnreadSamples / _marioAudioStream.CurrentBufferSize;
        if (bufferFullness > 0.75f)
        {
            _audioAccumulator = Math.Min(_audioAccumulator, AudioTickInterval);
        }

        if (_audioAccumulator > AudioTickInterval * 4)
        {
            _audioAccumulator = AudioTickInterval * 4;
        }

        if (_audioAccumulator < AudioTickInterval) return;
        _audioAccumulator -= AudioTickInterval;

        var numSamples = SM64Interop.AudioTick(_audioBuffer, (uint)_marioAudioStream.CurrentBufferSize, (uint)_marioAudioStream.UnreadSamples);
        if (numSamples <= 0) return;

        int written = DownmixAndResampleStereo(_audioBuffer, NativeSampleRate, TargetSampleRate, _convertedBuffer);
        if (written <= 0) return;
        if (written > _marioAudioStream.CurrentBufferSize - _marioAudioStream.UnreadSamples) return;

        Span<StereoSample> writeSpan = _convertedBuffer.AsSpan(0, written);
        _marioAudioStream.Write(writeSpan, ref _writeState);
    }

    private static int DownmixAndResampleStereo(short[] input, float inputRate, float outputRate, StereoSample[] output)
    {
        if (input == null || output == null)
            throw new ArgumentNullException();

        if (input.Length < 2)
            return 0;

        float ratio = inputRate / outputRate;
        float pos = 0f;
        int outIndex = 0;
        int inputFrames = input.Length / 2;
        const float invShortMax = 1f / 32768f;

        while (outIndex < output.Length)
        {
            int frameIndex = (int)pos;

            if (frameIndex + 1 >= inputFrames)
                break;

            int i = frameIndex * 2;
            float t = pos - frameIndex;
            float oneMinusT = 1f - t;

            float l = (input[i] * oneMinusT + input[i + 2] * t) * invShortMax;
            float r = (input[i + 1] * oneMinusT + input[i + 3] * t) * invShortMax;

            output[outIndex++] = new StereoSample(l, r);
            pos += ratio;
        }

        return outIndex;
    }
}
