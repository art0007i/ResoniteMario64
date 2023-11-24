using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Contexts;

namespace ResoniteMario64;

[Category("SM64")]
public class SM64Context {

    internal static SM64Context _instance = null;

    public World World;

    private readonly List<SM64Mario> _marios = new();
    //private readonly List<SM64ColliderDynamic> _surfaceObjects = new();

    // Audio
    private AudioOutput _audioSource;
    private const int BufferSize = 544 * 2 * 2;
    private int _bufferPosition = BufferSize;
    private readonly short[] _audioBuffer = new short[BufferSize];
    private readonly float[] _processedAudioBuffer = new float[BufferSize];

    protected SM64Context(World wld) {
        World = wld;

        Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
        ResoniteMario64.KEY_MAX_MESH_COLLIDER_TRIS.OnChanged += (newValue) => {
            Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
        };

        QueueStaticSurfacesUpdate();
    }

    public SM64Mario AddMario(Slot root)
    {
        var mario = new SM64Mario(root);
        _instance._marios.Add(mario);
        if (ResoniteMario64.config.GetValue(ResoniteMario64.KEY_PLAY_RANDOM_MUSIC)) Interop.PlayRandomMusic();

        return mario;
    }

    private void SetAudioSource() {

        // TODO: what the fuck is audio anyway

        //_audioSource = Slot.AttachComponent<AudioOutput>();
        //_audioSource.SpatialBlend.Value = 0f;

        //_audioSource.Volume.Value = ResoniteMario64.config.GetValue(ResoniteMario64.KEY_AUDIO_VOLUME);
        //Config.MeAudioVolume.OnEntryValueChanged.Subscribe((_, newValue) => _audioSource.volume = newValue);

        //_audioSource.pitch = Config.MeAudioPitch.Value;
        //Config.MeAudioPitch.OnEntryValueChanged.Subscribe((_, newValue) => _audioSource.pitch = newValue);

        //_audioSource.loop = true;
        //_audioSource.Play();
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
        // TODO: implement queue maybe?
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
