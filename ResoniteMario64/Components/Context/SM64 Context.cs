using System;
using System.Collections.Generic;
using System.Linq;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.Constants;
#if IsNet9
using Renderite.Shared;
#endif

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context : IDisposable
{
    public static SM64Context Instance { get; private set; }

    private readonly Dictionary<Collider, SM64DynamicCollider> _sm64DynamicColliders = new Dictionary<Collider, SM64DynamicCollider>();
    public readonly Dictionary<Slot, SM64Mario> Marios = new Dictionary<Slot, SM64Mario>();

    public bool AnyControlledMarios => Marios.Values.Any(x => x.IsLocal);

    public World World { get; }

    internal double LastTick;

    private bool _forceUpdate;

    private bool _disposed;

    private SM64Context(World wld)
    {
        World = wld;

        wld.WorldDestroyed += _ => { Dispose(); };

        Interop.GlobalInit(ResoniteMario64.SuperMario64UsZ64RomBytes);

        SetAudioSource();

        // Update context's colliders
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(wld));

        ResoniteMario64.KeyMaxMeshColliderTris.OnChanged += _ => { Instance._forceUpdate = true; };

        wld.RunInUpdates(3, Init);
    }

    private void Init()
    {
        if (_disposed) return;

        World.RootSlot.ForeachComponentInChildren<Collider>(c =>
        {
            if (Utils.IsGoodDynamicCollider(c))
            {
                AddCollider(c);
            }
        });
    }

    public void OnCommonUpdate()
    {
        if (_disposed) return;

        if (World.InputInterface.GetKeyDown(Key.Semicolon))
        {
            _forceUpdate = true;
        }

        if (_forceUpdate)
        {
            StaticTerrainUpdate();
            _forceUpdate = false;
        }

        HandleInputs();

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
        if (_disposed) return;

        ProcessAudio();

        Dictionary<Collider, SM64DynamicCollider> dynamicColliders = _sm64DynamicColliders.GetTempDictionary();
        foreach (SM64DynamicCollider dynamicCol in dynamicColliders.Values)
        {
            dynamicCol?.ContextFixedUpdateSynced();
        }

        Dictionary<Slot, SM64Mario> marios = Marios.GetTempDictionary();
        foreach (SM64Mario mario in marios.Values)
        {
            mario?.ContextFixedUpdateSynced();
        }
    }

    public static bool TryAddMario(Slot root) => AddMario(root) != null;

    public static SM64Mario AddMario(Slot root)
    {
        ResoniteMod.Msg($"Trying to add mario for SlotID: {root.ReferenceID}");

        SM64Mario mario = null;

        bool success = EnsureInstanceExists(root.World);
        if (!success) return null;

        int maxLocalMarios = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMaxMariosPerPerson);
        if (Instance.Marios.Values.Count(x => x.IsLocal) >= maxLocalMarios && root.GetAllocatingUser() == root.LocalUser)
        {
            ResoniteMod.Error($"You cannot have more than {maxLocalMarios} Mario's");
            return null;
        }

        ResoniteMod.Msg("instance?  " + success);
        if (!Instance.Marios.ContainsKey(root))
        {
            ResoniteMod.Msg("Non-duplicate mario.");
            mario = new SM64Mario(root);
            Instance.Marios.Add(root, mario);
            if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayRandomMusic)) Interop.PlayRandomMusic();
        }

        root.Parent.RunInUpdates(3, () =>
        {
            root.Parent.Children.Where(x => x.Tag == MarioTag && !Instance.Marios.ContainsKey(x)).Do(root2 =>
            {
                ResoniteMod.Msg("Non-duplicate mario.");
                var mario2 = new SM64Mario(root2);
                Instance.Marios.Add(root2, mario2);
            });
        });

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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            ResoniteMod.Debug("Disposing SM64Context");

            Dictionary<Slot, SM64Mario> marios = Marios.GetTempDictionary();
            foreach (SM64Mario mario in marios.Values)
            {
                mario?.Dispose();
            }
            Marios.Clear();

            Dictionary<Collider, SM64DynamicCollider> dynamicColliders = _sm64DynamicColliders.GetTempDictionary();
            foreach (SM64DynamicCollider col in dynamicColliders.Values)
            {
                col?.Dispose();
            }
            _sm64DynamicColliders.Clear();

            LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
            loco?.SupressSources?.RemoveAll(InputBlock);

            if (_marioAudioSlot is { IsRemoved: false } && _marioAudioSlot.GetAllocatingUser() == _marioAudioSlot.LocalUser)
            {
                _marioAudioSlot?.Destroy();
            }

            ResoniteMod.Debug("Finished disposing SM64Context");
        }

        Interop.GlobalTerminate();
        Instance = null;
    }
}