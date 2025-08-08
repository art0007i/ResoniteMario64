using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Timers;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.libsm64;
using ResoniteModLoader;

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context
{
    internal readonly List<Collider> StaticColliders = new List<Collider>();
    internal readonly Dictionary<Collider, SM64DynamicCollider> DynamicColliders = new Dictionary<Collider, SM64DynamicCollider>();
    internal readonly Dictionary<Collider, SM64Interactable> Interactables = new Dictionary<Collider, SM64Interactable>();
    internal readonly List<Collider> WaterBoxes = new List<Collider>();

    public void HandleCollider(Collider collider, bool log = true)
    {
        if (collider == null) return;
        if (collider.IsDestroyed)
        {
            HandleColliderDestroyed(collider);
            return;
        }

        int? added = TryAddCollider(collider);
        if (added != null)
        {
            collider.Destroyed -= HandleColliderDestroyed;
            collider.Destroyed += HandleColliderDestroyed;
        }

        if (log) LogCollider(collider, added, collider.IsDestroyed);
    }

    private void HandleColliderDestroyed(IDestroyable instance)
    {
        if (instance is not Collider collider) return;

        int? removed = TryRemoveCollider(collider);
        if (removed != null)
        {
            collider.Destroyed -= HandleColliderDestroyed;
        }

        LogCollider(collider, removed, true);
    }

    private int? TryAddCollider(Collider collider)
    {
        if (Utils.IsGoodStaticCollider(collider))
        {
            return RegisterStaticCollider(collider);
        }

        if (Utils.IsGoodDynamicCollider(collider))
        {
            return RegisterDynamicCollider(collider);
        }

        if (Utils.IsGoodInteractable(collider))
        {
            return RegisterInteractable(collider);
        }

        if (Utils.IsGoodWaterBox(collider))
        {
            return RegisterWaterBox(collider);
        }

        return null;
    }

    private int? TryRemoveCollider(Collider collider)
    {
        if (StaticColliders.Contains(collider))
        {
            UnregisterStaticCollider(collider);
            return 1;
        }

        if (DynamicColliders.TryGetValue(collider, out SM64DynamicCollider dynamicCollider))
        {
            dynamicCollider.Dispose();
            return 2;
        }

        if (Interactables.TryGetValue(collider, out SM64Interactable interactable))
        {
            interactable.Dispose();
            return 3;
        }

        if (WaterBoxes.Contains(collider))
        {
            UnregisterWaterBox(collider);
            return 4;
        }

        return null;
    }

    private static Timer _staticUpdateTimer;

    public void QueueStaticCollidersUpdate()
    {
        if (_staticUpdateTimer != null) return;

        _staticUpdateTimer = new Timer(1500);
        _staticUpdateTimer.Elapsed += delegate
        {
            _staticUpdateTimer.Stop();
            _staticUpdateTimer.Dispose();
            _staticUpdateTimer = null;

            _staticColliderUpdate = true;
        };
        _staticUpdateTimer.AutoReset = false;
        _staticUpdateTimer.Start();
    }

    // Static Colliders
    private int RegisterStaticCollider(Collider collider)
    {
        QueueStaticCollidersUpdate();

        if (StaticColliders.Contains(collider))
        {
            return 10;
        }

        StaticColliders.Add(collider);
        return 1;
    }

    private void UnregisterStaticCollider(Collider collider)
    {
        QueueStaticCollidersUpdate();

        StaticColliders.Remove(collider);
    }

    // Dynamic Colliders
    private int RegisterDynamicCollider(Collider collider)
    {
        if (DynamicColliders.TryGetValue(collider, out SM64DynamicCollider dynamicCollider))
        {
            if (dynamicCollider.InitScale.Approximately(collider.Slot.GlobalScale, 0.001f))
            {
                return 20;
            }

            dynamicCollider.Dispose();
        }

        SM64DynamicCollider col = new SM64DynamicCollider(collider, this);
        DynamicColliders.Add(collider, col);
        return 2;
    }

    internal void UnregisterDynamicCollider(Collider collider)
    {
        DynamicColliders.Remove(collider);
    }

    // Interactables
    private int RegisterInteractable(Collider collider)
    {
        if (Interactables.ContainsKey(collider))
        {
            return 30;
        }

        SM64Interactable col = new SM64Interactable(collider, this);
        Interactables.Add(collider, col);
        return 3;
    }

    internal void UnregisterInteractable(Collider collider)
    {
        Interactables.Remove(collider);
    }

    // WaterBoxes
    private int RegisterWaterBox(Collider collider)
    {
        if (WaterBoxes.Contains(collider))
        {
            return 40;
        }

        WaterBoxes.Add(collider);
        return 4;
    }

    private void UnregisterWaterBox(Collider collider)
    {
        WaterBoxes.Remove(collider);
    }

    // Patches
    [HarmonyPatch(typeof(Collider))]
    public class ColliderPatch
    {
        [HarmonyPatch("OnAwake"), HarmonyPostfix]
        public static void OnAwakePatch(Collider __instance)
        {
            if (SM64Context.Instance == null) return;

            __instance.RunInUpdates(1, () => SM64Context.Instance?.HandleCollider(__instance));
        }

        [HarmonyPatch("OnChanges"), HarmonyPostfix]
        public static void OnChangesPatch(Collider __instance)
        {
            if (SM64Context.Instance == null) return;

            __instance.RunInUpdates(1, () => SM64Context.Instance?.HandleCollider(__instance));
        }
    }

    private static void LogCollider(object obj, int? added, bool destroyed, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
#if DEBUG
        if (!ResoniteMario64.Config.GetValue(ResoniteMario64.KeyLogColliderChanges)) return;
        if (obj is not Collider collider) return;
        if (added == null) return;

        bool isNewlyAdded = added is 1 or 2 or 3 or 4;
        string name = added switch
        {
            1 or 10 => "Static Collider",
            2 or 20 => "Dynamic Collider",
            3 or 30 => "Interactable",
            4 or 40 => "WaterBox",
            _       => "Collider"
        };

        string tag = collider.Slot?.Tag;
        string[] tagParts = tag?.Split(',');

        Utils.TryParseTagParts(
            tagParts,
            out SM64Constants.SM64SurfaceType surfaceType,
            out SM64Constants.SM64TerrainType terrainType,
            out SM64Constants.SM64InteractableType interactableType,
            out int interactableId
        );

        string state = destroyed
                ? "Destroyed"
                : isNewlyAdded
                        ? "Added"
                        : "Already Added";

        string message = $"{name} {state}: Name: {collider.Slot?.Name}, ID: {collider.ReferenceID}, Surface: {surfaceType}, Terrain: {terrainType}, Interactable: {interactableType}, ID/Force: {interactableId}";

        if (destroyed)
            Logger.Error(message, caller, line);
        else if (isNewlyAdded)
            Logger.Msg(message, caller, line);
        else
            Logger.Warn(message, caller, line);
#endif
    }

    public void GetAllColliders(bool log, out Dictionary<int, List<Collider>> colliders)
    {
        colliders = new Dictionary<int, List<Collider>>
        {
            [10] = StaticColliders.GetTempList(),
            [20] = DynamicColliders.Keys.GetTempList(),
            [30] = Interactables.Keys.GetTempList(),
            [40] = WaterBoxes.GetTempList()
        };

        if (!log) return;

        foreach (KeyValuePair<int, List<Collider>> kvp in colliders)
        {
            foreach (Collider collider in kvp.Value)
            {
                LogCollider(collider, kvp.Key, collider.IsDestroyed);
            }
        }
    }

    public void ReloadAllColliders(bool log = true)
    {
        World.RootSlot.ForeachComponentInChildren<Collider>(c =>
        {
            TryRemoveCollider(c);
            HandleCollider(c, log);
        });
    }
}