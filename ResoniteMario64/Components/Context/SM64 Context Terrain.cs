using System.Timers;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.libsm64;
using ResoniteModLoader;

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context
{
    private static Timer _staticUpdateTimer;

    public static void QueueStaticSurfacesUpdate()
    {
        if (Instance == null) return;
        if (_staticUpdateTimer != null) return;

        _staticUpdateTimer = new Timer(1500);
        _staticUpdateTimer.Elapsed += delegate
        {
            _staticUpdateTimer.Stop();
            _staticUpdateTimer.Dispose();
            _staticUpdateTimer = null;
            
            Instance._forceUpdate = true;
        };
        _staticUpdateTimer.AutoReset = false;
        _staticUpdateTimer.Start();
    }

    private void StaticTerrainUpdate()
    {
        if (Instance == null) return;

        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
    }

    public static void AddCollider(Collider instance)
    {
        if (Utils.IsGoodStaticCollider(instance))
        {
            QueueStaticSurfacesUpdate();

            instance.Slot.OnPrepareDestroy -= HandleStaticColliderDestroyed;
            instance.Slot.OnPrepareDestroy += HandleStaticColliderDestroyed;
        }

        if (Utils.IsGoodDynamicCollider(instance))
        {
            RegisterDynamicCollider(instance);
        }
    }

    private static void HandleStaticColliderDestroyed(Slot slot)
    {
        if (Instance == null) return;

        Instance._forceUpdate = true;
    }

    public static bool RegisterDynamicCollider(Collider surfaceObject)
    {
        if (Instance == null) return false;
        
        if (Instance._sm64DynamicColliders.ContainsKey(surfaceObject))
        {
            return false;
        }

        SM64DynamicCollider col = new SM64DynamicCollider(surfaceObject);
        Instance._sm64DynamicColliders.Add(surfaceObject, col);

        ResoniteMod.Debug($"Successfully Registered DynamicCollider - {surfaceObject.ReferenceID}");
        return true;
    }

    public static void UnregisterDynamicCollider(Collider surfaceObject)
    {
        if (Instance == null) return;
        
        Instance._sm64DynamicColliders.Remove(surfaceObject);

        ResoniteMod.Debug($"Successfully Unregistered DynamicCollider - {surfaceObject.ReferenceID}");
    }

    [HarmonyPatch(typeof(Collider))]
    public class ColliderPatch
    {
        [HarmonyPatch("OnAwake"), HarmonyPostfix]
        public static void OnAwakePatch(Collider __instance)
        {
            __instance.RunInUpdates(1, () => { AddCollider(__instance); });
        }

        [HarmonyPatch("OnChanges"), HarmonyPostfix]
        public static void OnChangesPatch(Collider __instance)
        {
            __instance.RunInUpdates(1, () => { AddCollider(__instance); });
        }
    }
}