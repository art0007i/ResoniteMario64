using System.Runtime.CompilerServices;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.Components.Objects;
using ResoniteMario64.Mario64.libsm64;

namespace ResoniteMario64.Mario64.Components.Context;

public sealed partial class SM64Context
{
    internal readonly Dictionary<Collider, SM64StaticCollider> StaticColliders = new Dictionary<Collider, SM64StaticCollider>();
    internal readonly Dictionary<Collider, SM64DynamicCollider> DynamicColliders = new Dictionary<Collider, SM64DynamicCollider>();
    internal readonly Dictionary<Collider, SM64Interactable> Interactables = new Dictionary<Collider, SM64Interactable>();
    internal readonly Dictionary<Collider, SM64Teleporter> Teleporters = new Dictionary<Collider, SM64Teleporter>();
    internal readonly Dictionary<Collider, SM64WaterBox> WaterBoxes = new Dictionary<Collider, SM64WaterBox>();
    internal readonly Dictionary<Collider, SM64FakeObject> FakeObjects = new Dictionary<Collider, SM64FakeObject>();

    public void HandleCollider(Collider collider, bool log = true)
    {
        if (collider == null) return;

        if (collider.World != World) return;
        if (collider.IsDestroyed)
        {
            HandleColliderDestroyed(collider);
            return;
        }

        ColliderOp added = TryAddCollider(collider);
        if (added != null)
        {
            collider.Destroyed -= HandleColliderDestroyed;
            collider.Destroyed += HandleColliderDestroyed;
        }

        if (log) LogCollider(collider, added);
    }

    private void HandleColliderDestroyed(IDestroyable instance)
    {
        if (instance is not Collider collider) return;

        ColliderOp removed = TryRemoveCollider(collider);
        if (removed != null)
        {
            collider.Destroyed -= HandleColliderDestroyed;
        }

        LogCollider(collider, removed);
    }

    private ColliderOp TryAddCollider(Collider collider)
    {
        return Utils.GetColliderCategory(collider) switch
        {
            ColliderCategory.Static => RegisterStaticCollider(collider),
            ColliderCategory.Dynamic => RegisterDynamicCollider(collider),
            ColliderCategory.Interactable => RegisterInteractable(collider),
            ColliderCategory.WaterBox => RegisterWaterBox(collider),
            ColliderCategory.Teleporter => RegisterTeleporter(collider),
            ColliderCategory.FakeObject => RegisterFakeObject(collider),
            _ => null
        };
    }

    private ColliderOp TryRemoveCollider(Collider collider)
    {
        if (StaticColliders.Remove(collider, out SM64StaticCollider staticCollider))
        {
            staticCollider.Dispose();
            return new ColliderOp(ColliderCategory.Static, ColliderOpResult.Removed);
        }

        if (DynamicColliders.Remove(collider, out SM64DynamicCollider dynamicCollider))
        {
            dynamicCollider.Dispose();
            return new ColliderOp(ColliderCategory.Dynamic, ColliderOpResult.Removed);
        }

        if (Interactables.Remove(collider, out SM64Interactable interactable))
        {
            interactable.Dispose();
            return new ColliderOp(ColliderCategory.Interactable, ColliderOpResult.Removed);
        }

        if (WaterBoxes.Remove(collider, out SM64WaterBox waterBox))
        {
            waterBox.Dispose();
            return new ColliderOp(ColliderCategory.WaterBox, ColliderOpResult.Removed);
        }

        if (Teleporters.Remove(collider, out SM64Teleporter teleporter))
        {
            teleporter.Dispose();
            return new ColliderOp(ColliderCategory.Teleporter, ColliderOpResult.Removed);
        }

        if (FakeObjects.Remove(collider, out SM64FakeObject fakeObject))
        {
            fakeObject.Dispose();
            return new ColliderOp(ColliderCategory.FakeObject, ColliderOpResult.Removed);
        }

        return null;
    }

    private static System.Timers.Timer _staticUpdateTimer;

    private void QueueStaticCollidersUpdate()
    {
        if (_staticUpdateTimer != null) return;

        _staticUpdateTimer = new System.Timers.Timer(1500);
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
    private ColliderOp RegisterStaticCollider(Collider collider)
    {
        QueueStaticCollidersUpdate();

        if (StaticColliders.TryGetValue(collider, out SM64StaticCollider staticCollider))
        {
            if (collider.Slot.Tag == staticCollider.OriginalTag)
            {
                return new ColliderOp(ColliderCategory.Static, ColliderOpResult.AlreadyExists);
            }

            staticCollider.Dispose();
            DynamicColliders.Remove(collider);
        }

        SM64StaticCollider col = new SM64StaticCollider(collider, this);
        StaticColliders.Add(collider, col);
        return new ColliderOp(ColliderCategory.Static, ColliderOpResult.Added);
    }

    internal void UnregisterStaticCollider(Collider collider)
    {
        QueueStaticCollidersUpdate();

        StaticColliders.Remove(collider);
    }

    // Dynamic Colliders
    private ColliderOp RegisterDynamicCollider(Collider collider)
    {
        if (DynamicColliders.TryGetValue(collider, out SM64DynamicCollider dynamicCollider))
        {
            bool scaleChanged = !dynamicCollider.InitScale.Approximately(collider.Slot.GlobalScale, 0.001f);
            bool tagChanged = collider.Slot.Tag != dynamicCollider.OriginalTag;
            bool shapeChanged = collider.ShapeChanged;

            bool changed = scaleChanged || tagChanged || shapeChanged;

            if (changed && dynamicCollider.IsPlayer)
            {
                double timeSinceChange = (collider.World.Time.AbsoluteWorldTime - dynamicCollider.LastChangedTime).TotalMilliseconds;

                changed = timeSinceChange >= 100.0;
            }

            if (!changed)
            {
                return new ColliderOp(ColliderCategory.Dynamic, ColliderOpResult.AlreadyExists);
            }

            dynamicCollider.Dispose();
            DynamicColliders.Remove(collider);
        }
        
        SM64DynamicCollider col = new SM64DynamicCollider(collider, this);
        DynamicColliders.Add(collider, col);

        return new ColliderOp(ColliderCategory.Dynamic, ColliderOpResult.Added);
    }

    internal void UnregisterDynamicCollider(Collider collider)
    {
        DynamicColliders.Remove(collider);
    }

    // Interactables
    private ColliderOp RegisterInteractable(Collider collider)
    {
        if (Interactables.TryGetValue(collider, out SM64Interactable interactable))
        {
            if (collider.Slot.Tag == interactable.OriginalTag)
            {
                return new ColliderOp(ColliderCategory.Interactable, ColliderOpResult.AlreadyExists);
            }

            interactable.Dispose();
            Interactables.Remove(collider);
        }

        SM64Interactable col = new SM64Interactable(collider, this);
        Interactables.Add(collider, col);
        return new ColliderOp(ColliderCategory.Interactable, ColliderOpResult.Added);
    }

    internal void UnregisterInteractable(Collider collider)
    {
        Interactables.Remove(collider);
    }

    // WaterBoxes
    private ColliderOp RegisterWaterBox(Collider collider)
    {
        if (WaterBoxes.ContainsKey(collider))
        {
            return new ColliderOp(ColliderCategory.WaterBox, ColliderOpResult.AlreadyExists);
        }

        SM64WaterBox col = new SM64WaterBox(collider, this);
        WaterBoxes.Add(collider, col);
        return new ColliderOp(ColliderCategory.WaterBox, ColliderOpResult.Added);
    }

    internal void UnregisterWaterBox(Collider collider)
    {
        WaterBoxes.Remove(collider);
    }

    // Teleporters
    private ColliderOp RegisterTeleporter(Collider collider)
    {
        if (Teleporters.TryGetValue(collider, out SM64Teleporter teleporter))
        {
            if (collider.Slot.Tag == teleporter.OriginalTag)
            {
                return new ColliderOp(ColliderCategory.Teleporter, ColliderOpResult.AlreadyExists);
            }

            teleporter.Dispose();
            Teleporters.Remove(collider);
        }

        SM64Teleporter col = new SM64Teleporter(collider, this);
        Teleporters.Add(collider, col);
        return new ColliderOp(ColliderCategory.Teleporter, ColliderOpResult.Added);
    }

    internal void UnregisterTeleporter(Collider collider)
    {
        Teleporters.Remove(collider);
    }
    
    // FakeObjects
    private ColliderOp RegisterFakeObject(Collider collider)
    {
        if (FakeObjects.TryGetValue(collider, out SM64FakeObject fakeObject))
        {
            bool scaleChanged = !fakeObject.InitScale.Approximately(collider.Slot.GlobalScale, 0.001f);
            bool tagChanged = collider.Slot.Tag != fakeObject.OriginalTag;
            bool shapeChanged = collider.ShapeChanged;

            bool changed = scaleChanged || tagChanged || shapeChanged;
            if (!changed)
            {
                return new ColliderOp(ColliderCategory.FakeObject, ColliderOpResult.AlreadyExists);
            }

            fakeObject.Dispose();
            FakeObjects.Remove(collider);
        }

        SM64FakeObject col = new SM64FakeObject(collider, this);
        FakeObjects.Add(collider, col);
        return new ColliderOp(ColliderCategory.FakeObject, ColliderOpResult.Added);
    }

    internal void UnregisterFakeObject(Collider collider)
    {
        FakeObjects.Remove(collider);
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

            SM64Context.Instance?.HandleCollider(__instance);
        }
    }

    private static void LogCollider(object obj, ColliderOp op, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        if (op == null) return;
        if (obj is not Collider collider) return;
        if ((collider.Slot.Tag?.Contains("NOLOG") ?? false) || collider.Slot.GetComponent<UserRoot>() != null) return;
        if (!Config.LogColliderChanges.Value) return;

        string name = op.Category.ToString();
        string state = op.Result.ToString();

        if (collider.IsRemoved) state = "Destroyed";

        string tag = collider.Slot?.Tag;
        string[] tagParts = tag?.Split(',');

        Utils.ParseTagParts(tagParts, out SurfaceType surfaceType, out TerrainType terrainType, out InteractableType interactableType, out int idx, out int ext);

        string message = $"{name} {state}: Name: {collider.Slot?.Name}, ID: {collider.ReferenceID}, Surface: {surfaceType}, Terrain: {terrainType}, Interactable: {interactableType}, ID/Force: {idx}, Ext: {ext}";

        if (op.Result == ColliderOpResult.Removed || collider.IsRemoved)
            Logger.Error(message, caller, line);
        else if (op.Result == ColliderOpResult.Added)
            Logger.Msg(message, caller, line);
        else
            Logger.Warn(message, caller, line);
    }

    public Dictionary<ColliderCategory, List<ISM64Object>> GetAllColliders(bool log)
    {
        Dictionary<ColliderCategory, List<ISM64Object>> colliders = new Dictionary<ColliderCategory, List<ISM64Object>>();

        Add(ColliderCategory.Static, StaticColliders.Values);
        Add(ColliderCategory.Dynamic, DynamicColliders.Values);
        Add(ColliderCategory.Interactable, Interactables.Values);
        Add(ColliderCategory.WaterBox, WaterBoxes.Values);
        Add(ColliderCategory.Teleporter, Teleporters.Values);
        Add(ColliderCategory.FakeObject, FakeObjects.Values);

        return colliders;

        void Add(ColliderCategory category, IEnumerable<ISM64Object> source)
        {
            List<ISM64Object> objects = source.GetTempList();
            colliders[category] = objects;

            if (!log) return;

            for (int i = 0; i < objects.Count; i++)
            {
                ISM64Object obj = objects[i];

                ColliderOpResult result = obj.Collider.IsDestroyed
                    ? ColliderOpResult.Removed
                    : ColliderOpResult.AlreadyExists;

                LogCollider(obj.Collider, new ColliderOp(category, result));
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