using Elements.Core;
using FrooxEngine;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;

namespace ResoniteMario64.Mario64.Components.Objects;

public sealed class SM64WaterBox : ISM64Object
{
    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    public bool IsDisposed { get; private set; }

    public SM64WaterBox(Collider col, SM64Context instance)
    {
        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Config.DebugEnabled.Value) Logger.Warn($"[WaterBox{mc.GetType()}] {mc.Slot.Name} ({mc.ReferenceID}) Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}");
            Dispose();
            return;
        }

        World = col.World;
        Context = instance;
        Collider = col;
    }

    public float Handle(float3 marioPos)
    {
        Collider collider = Collider;
        if (collider == null || collider.IsRemoved || collider.IsDisposed) return float.NaN;

        if (collider is BoxCollider box)
        {
            float3 localMarioPos = collider.Slot.GlobalPointToLocal(marioPos);
            BoundingBox localWaterBox = box.LocalBoundingBox;

            if (localWaterBox.Contains(localMarioPos))
            {
                return collider.GlobalBoundingBox.max.y;
            }
        }
        else if (collider.GlobalBoundingBox.Contains(marioPos))
        {
            return collider.GlobalBoundingBox.max.y;
        }

        return float.NaN;
    }

    ~SM64WaterBox()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (IsDisposed) return;

        if (disposing)
        {
            Context?.Terrain.UnregisterWaterBox(Collider);

            World = null;
            Context = null;
            Collider = null;
        }

        IsDisposed = true;
    }
}
