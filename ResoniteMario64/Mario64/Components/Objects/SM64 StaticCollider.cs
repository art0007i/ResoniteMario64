using Elements.Core;
using FrooxEngine;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.libsm64;

namespace ResoniteMario64.Mario64.Components.Objects;

public sealed class SM64StaticCollider : ISM64Object, ISM64Collider
{
    public SurfaceType SurfaceType { get; }
    public TerrainType TerrainType { get; }
    public int Force { get; }
    public string OriginalTag { get; }

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    public bool IsDisposed { get; private set; }
    public int PoleId = -1;

    public SM64StaticCollider(Collider col, SM64Context instance)
    {
        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Config.DebugEnabled.Value) Logger.Warn($"[StaticCollider{mc.GetType()}] {mc.Slot.Name} ({mc.ReferenceID}) Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}");
            Dispose();
            return;
        }

        World = col.World;
        Context = instance;
        Collider = col;
        OriginalTag = col.Slot.Tag;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.ParseTagParts(tagParts, out var surfaceType, out var terrainType, out _, out int force, out int ext);

        SurfaceType = surfaceType;
        TerrainType = terrainType;
        Force = force;

        if (ext == -1) return;

        float radiusFloat = MathF.Max(Collider.LocalBoundingBox.Size.x, Collider.LocalBoundingBox.Size.z) * 0.5f;

        PoleId = SM64Interop.CreateFakeObject(new float3(Collider.Slot.GlobalPosition.x, Collider.Slot.GlobalPosition.y, Collider.Slot.GlobalPosition.z), 1);
        SM64Interop.SetFakeObjectHitbox(PoleId, radiusFloat, Collider.LocalBoundingBox.Size.y.ToMarioFloat(), 0);
    }

    public void UpdateFakeObject()
    {
        if (PoleId == -1) return;
        SM64Interop.TickFakeObject(PoleId);
    }

    ~SM64StaticCollider()
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
            Context?.Terrain.UnregisterStaticCollider(Collider);

            World = null;
            Context = null;
            Collider = null;
        }

        IsDisposed = true;
    }
}
