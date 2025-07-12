using System;
using System.Collections.Generic;
using FrooxEngine;
using ResoniteMario64.libsm64;
using ResoniteModLoader;

namespace ResoniteMario64.Components.Context;

public partial class SM64Context : IDisposable
{
    // private readonly List<SM64ColliderDynamic> _surfaceObjects = new();
    public static SM64Context Instance;

    public readonly Dictionary<Slot, SM64Mario> Marios = new Dictionary<Slot, SM64Mario>();

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

        Dictionary<Slot, SM64Mario> marios = Marios.GetTempDictionary();
        foreach (SM64Mario mario in marios.Values)
        {
            mario.ContextUpdateSynced();
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

        Dictionary<Slot, SM64Mario> marios = Marios.GetTempDictionary();
        foreach (SM64Mario mario in marios.Values)
        {
            mario.ContextFixedUpdateSynced();
        }
    }

    public static bool TryAddMario(Slot root, bool isButton = false) => AddMario(root, isButton) != null;

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
    
    public static void RemoveMario(SM64Mario mario)
    {
        if (Instance == null) return;

        Interop.MarioDelete(mario.MarioId);

        Instance.Marios.Remove(mario.MarioSlot);

        if (Instance.Marios.Count == 0)
        {
            Interop.StopMusic();
        }
    }

    private static bool EnsureInstanceExists(World wld)
    {
        if (Instance != null && wld != Instance.World)
        {
            bool destroy = wld.Focus == World.WorldFocus.Focused;
            ResoniteMod.Error("Tried to create instance while one already exists." + (destroy ? " It will be replaced by a new one." : ""));
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
    
    public void Dispose()
    {
        Dictionary<Slot, SM64Mario> marios = Marios.GetTempDictionary();
        foreach (SM64Mario mario in marios.Values)
        {
            mario.Dispose();
        }

        Interop.GlobalTerminate();

        LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
        loco?.SupressSources?.RemoveAll(InputBlock);

        _marioAudioSlot?.Destroy();

        Instance = null;
    }
}