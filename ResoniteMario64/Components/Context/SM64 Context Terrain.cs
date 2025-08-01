using System.Collections.Generic;
using System.Timers;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.libsm64;

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context
{
    internal readonly Dictionary<Collider, SM64DynamicCollider> DynamicColliders = new Dictionary<Collider, SM64DynamicCollider>();
    internal readonly Dictionary<Collider, SM64Interactable> Interactables = new Dictionary<Collider, SM64Interactable>();
    internal readonly List<Collider> WaterBoxes = new List<Collider>();
    
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
        
        if (Utils.IsGoodInteractable(instance))
        {
            RegisterInteractable(instance);
        }
        
        if (Utils.IsGoodWaterBox(instance))
        {
            if (!WaterBoxes.Contains(instance))
            {
                WaterBoxes.Add(instance);
                
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
            WaterBoxes.Remove(col);
        });
    }

    public void RegisterDynamicCollider(Collider surfaceObject)
    {
        if (DynamicColliders.TryGetValue(surfaceObject, out SM64DynamicCollider value))
        {
            if (value.InitScale.Approximately(surfaceObject.Slot.GlobalScale, 0.001f))
            {
                return;
            }

            value.Dispose();
        }

        SM64DynamicCollider col = new SM64DynamicCollider(surfaceObject, this);
        DynamicColliders.Add(surfaceObject, col);
    }

    public void UnregisterDynamicCollider(Collider surfaceObject)
    {
        DynamicColliders.Remove(surfaceObject);
    }
    
    public void RegisterInteractable(Collider surfaceObject)
    {
        if (Interactables.ContainsKey(surfaceObject))
        {
            return;
        }

        SM64Interactable col = new SM64Interactable(surfaceObject, this);
        Interactables.Add(surfaceObject, col);
    }

    public void UnregisterInteractable(Collider surfaceObject)
    {
        Interactables.Remove(surfaceObject);
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