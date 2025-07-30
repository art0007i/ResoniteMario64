using System.Collections.Generic;
using System.Timers;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.libsm64;
using ResoniteModLoader;

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context
{
    private readonly Dictionary<Collider, SM64DynamicCollider> _sm64DynamicColliders = new Dictionary<Collider, SM64DynamicCollider>();
    internal readonly List<Collider> _waterBoxes = new List<Collider>();
    
    
    private static Timer _staticUpdateTimer;
    public void QueueStaticSurfacesUpdate()
    {
        if (_staticUpdateTimer != null) return;

        _staticUpdateTimer = new Timer(1500);
        _staticUpdateTimer.Elapsed += delegate
        {
            _staticUpdateTimer.Stop();
            _staticUpdateTimer.Dispose();
            _staticUpdateTimer = null;
            
            _forceUpdate = true;
        };
        _staticUpdateTimer.AutoReset = false;
        _staticUpdateTimer.Start();
    }

    private void StaticTerrainUpdate()
    {
        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
    }

    public void AddCollider(Collider instance)
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

        if (Utils.IsGoodWaterBox(instance))
        {
            if (!_waterBoxes.Contains(instance))
            {
                _waterBoxes.Add(instance);
                
                instance.Slot.OnPrepareDestroy -= HandleWaterBoxDestroyed;
                instance.Slot.OnPrepareDestroy += HandleWaterBoxDestroyed;
            }
        }
    }

    private void HandleStaticColliderDestroyed(Slot slot)
    {
        _forceUpdate = true;
    }
    
    private void HandleWaterBoxDestroyed(Slot slot)
    {
        slot.ForeachComponentInChildren<Collider>(col =>
        {
            _waterBoxes.Remove(col);
        });
    }

    public bool RegisterDynamicCollider(Collider surfaceObject)
    {
        if (_sm64DynamicColliders.TryGetValue(surfaceObject, out SM64DynamicCollider value))
        {
            if (value.InitScale.Approximately(surfaceObject.Slot.GlobalScale, 0.001f))
            {
                return false;
            }

            value.Dispose();
        }

        SM64DynamicCollider col = new SM64DynamicCollider(surfaceObject);
        _sm64DynamicColliders.Add(surfaceObject, col);

        ResoniteMod.Debug($"Successfully Registered DynamicCollider - {surfaceObject.ReferenceID}");
        return true;
    }

    public void UnregisterDynamicCollider(Collider surfaceObject)
    {
        _sm64DynamicColliders.Remove(surfaceObject);

        ResoniteMod.Debug($"Successfully Unregistered DynamicCollider - {surfaceObject.ReferenceID}");
    }

    [HarmonyPatch(typeof(Collider))]
    public class ColliderPatch
    {
        [HarmonyPatch("OnAwake"), HarmonyPostfix]
        public static void OnAwakePatch(Collider __instance)
        {
            __instance.RunInUpdates(1, () => { SM64Context.Instance?.AddCollider(__instance); });
        }

        [HarmonyPatch("OnChanges"), HarmonyPostfix]
        public static void OnChangesPatch(Collider __instance)
        {
            __instance.RunInUpdates(1, () => { SM64Context.Instance?.AddCollider(__instance); });
        }
    }
}