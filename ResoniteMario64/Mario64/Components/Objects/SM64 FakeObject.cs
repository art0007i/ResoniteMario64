using Elements.Core;
using FrooxEngine;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.libsm64;

namespace ResoniteMario64.Mario64.Components.Objects;

public sealed class SM64FakeObject : ISM64Object
{
    public string OriginalTag { get; }

    public readonly int ObjectId;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    public float3 Position { get; private set; }
    public float3 InitScale { get; private set; }

    public bool IsDisposed { get; private set; }

    public SM64FakeObject(Collider col, SM64Context instance)
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

        Position = col.Slot.GlobalPosition;
        InitScale = col.Slot.GlobalScale;

        float worldHeight = Collider.Slot.GlobalScale.y * Collider.LocalBoundingBox.Size.y;
        float marioHeight = worldHeight.ToMarioFloat();
        float halfHeight = marioHeight / 2f;

        // Preset 1 == Pole
        // TODO: Implement stuff other than Poles in libsm64, this is currently just for poles... Also Parse the Tags for Preset etc...
        ObjectId = SM64Interop.CreateFakeObject(Position, 1);

        SM64Interop.SetFakeObjectHitbox(ObjectId, 32f, halfHeight, halfHeight + 100);
    }

    private bool UpdateCurrentPositionData()
    {
        if (IsDisposed || Collider?.Slot == null) return false;

        float3 currentPosition = Collider.Slot.GlobalPosition;

        if (currentPosition == Position) return false;

        Position = currentPosition;

        return true;
    }

    internal void ContextFixedUpdate()
    {
        SM64Interop.TickFakeObject(ObjectId);
    }

    internal void ContextFixedUpdateSynced()
    {
        if (UpdateCurrentPositionData())
        {
            SM64Interop.SetFakeObjectPosition(ObjectId, Position);
        }
    }

    ~SM64FakeObject()
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
            Context?.Terrain.UnregisterFakeObject(Collider);

            Context = null;
            Collider = null;
            World = null;
        }

        if (SM64Interop.IsGlobalInit)
        {
            SM64Interop.DeleteFakeObject(ObjectId);
        }

        IsDisposed = true;
    }
}
