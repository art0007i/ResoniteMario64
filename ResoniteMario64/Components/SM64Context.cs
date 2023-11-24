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
public class SM64Context : Component {

    internal static SM64Context _instance = null;

    private readonly List<SM64Mario> _marios = new();
    private readonly List<SM64ColliderDynamic> _surfaceObjects = new();
    private readonly List<SM64LevelModifier> _levelModifierObjects = new();

    // Audio
    private AudioOutput _audioSource;
    private const int BufferSize = 544 * 2 * 2;
    private int _bufferPosition = BufferSize;
    private readonly short[] _audioBuffer = new short[BufferSize];
    private readonly float[] _processedAudioBuffer = new float[BufferSize];


    // Melon prefs
    private int _maxMariosAnimatedPerPerson;

    protected override void OnAttach() {
        base.OnAttach();

        Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
        ResoniteMario64.KEY_MAX_MESH_COLLIDER_TRIS.OnChanged += (newValue) => {
            Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
        };

        // Setup melon pref updates
        ResoniteMario64.KEY_MAX_MARIOS_PER_PERSON.OnChanged += newValue => {
            _maxMariosAnimatedPerPerson = (int)newValue;
            UpdatePlayerMariosState();
            ResoniteMario64.Msg($"Changed the Max Marios animated per player to {newValue}.");
        };

        QueueStaticSurfacesUpdate();
    }

    private void SetAudioSource() {

        _audioSource = Slot.AttachComponent<AudioOutput>();
        _audioSource.SpatialBlend.Value = 0f;

        // TODO: maybe delete these configs...
        _audioSource.Volume.Value = ResoniteMario64.config.GetValue(ResoniteMario64.KEY_AUDIO_VOLUME);
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

    protected override void OnCommonUpdate()
    {
        base.OnCommonUpdate();
        if (Time.WorldTime - LastTick >= ResoniteMario64.config.GetValue(ResoniteMario64.KEY_GAME_TICK_MS) / 1000f)
        {
            SM64GameTick();
            LastTick = Time.WorldTime;
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

        lock (_surfaceObjects) {
            foreach (var o in _surfaceObjects) {
                o.ContextFixedUpdateSynced();
            }
        }

        lock (_marios) {
            //CVRSM64LevelModifier.ContextTick(_marios);

            foreach (var o in _marios) {
                o.ContextFixedUpdateSynced(_marios);
            }
        }
    }

    protected override void OnDestroy() {
        Interop.GlobalTerminate();
        _instance = null;
    }

    private static bool EnsureInstanceExists(World wld) {
        if(_instance != null && wld != _instance.World)
        {
            var destroy = wld.Focus == World.WorldFocus.Focused;
            ResoniteMario64.Error("Tried to create instance while one already exists." + (destroy ? " It will be replaced by a new one." : ""));
            if (destroy) _instance.Destroy();
            else return false;
        }
        if (_instance != null) return false;

        var contextGo = wld.AddSlot("SM64_CONTEXT");
        //contextGo.hideFlags |= HideFlags.HideInHierarchy;
        _instance = contextGo.AttachComponent<SM64Context>();
        return true;
    }

    public static void QueueStaticSurfacesUpdate() {
        if (_instance == null) return;
        // If there was a queued update, cancel it first
        //_instance.CancelInvoke(nameof(StaticTerrainUpdate));
        //_instance.Invoke(nameof(StaticTerrainUpdate), 1.5f);
        _instance.StaticTerrainUpdate();
    }

    private void StaticTerrainUpdate() {
        if (_instance == null) return;
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces());
    }

    public static void UpdateMarioCount() {
        /*if (_instance == null || MarioInputModule.Instance == null) return;
        lock (_instance._marios) {
            var ourMarios = _instance._marios.FindAll(m => m.IsMine());
            MarioInputModule.Instance.controllingMarios = ourMarios.Count;
            MarioCameraMod.Instance.UpdateOurMarios(ourMarios);
        }*/
    }

    public static void UpdatePlayerMariosState() {
        if (_instance == null) return;

        lock (_instance._marios) {
            foreach (var playerMarios in _instance._marios.GroupBy(m => m.controllingUser.Target)) {
                if (playerMarios.First().IsMine()) continue;
                var maxMarios = _instance._maxMariosAnimatedPerPerson;
                foreach (var playerMario in playerMarios) {
                    playerMario.SetIsOverMaxCount(maxMarios-- <= 0);
                }
            }
        }
    }

    public static bool RegisterMario(SM64Mario mario) {
        if (!EnsureInstanceExists(mario.World)) return false;

        // Note: Don't use mario.IsMine() or mario.IsDead() here, they're still not initialized!
        lock (_instance._marios) {
            if (_instance._marios.Contains(mario)) return true;

            _instance._marios.Add(mario);

            //if (Config.MePlayRandomMusicOnMarioJoin.Value) Interop.PlayRandomMusic();

            //CVRSM64LevelModifier.MarkForUpdate();
        }
        return true;
    }

    public static void UnregisterMario(SM64Mario mario) {
        if (_instance == null) return;

        lock (_instance._marios) {
            if (!_instance._marios.Contains(mario)) return;

            _instance._marios.Remove(mario);
            UpdatePlayerMariosState();

            if (_instance._marios.Count == 0) {
                Interop.StopMusic();
            }
        }
    }

    public static bool RegisterSurfaceObject(SM64ColliderDynamic surfaceObject) {
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
    }
}
