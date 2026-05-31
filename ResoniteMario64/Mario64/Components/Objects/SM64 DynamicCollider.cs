using Elements.Core;
using FrooxEngine;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.libsm64;

namespace ResoniteMario64.Mario64.Components.Objects;

public sealed class SM64DynamicCollider : ISM64Object, ISM64Collider
{
    public SurfaceType SurfaceType { get; }
    public TerrainType TerrainType { get; }
    public int Force { get; }
    public string OriginalTag { get; }

    public readonly uint ObjectId;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }
    public bool IsPlayer { get; internal set; }

    public float3 Position { get; private set; }
    public floatQ Rotation { get; private set; }
    public float3 InitScale { get; }

    public DateTime LastChangedTime { get; private set; }

    public bool IsDisposed { get; private set; }

    public SM64DynamicCollider(Collider col, SM64Context instance)
    {
        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Config.DebugEnabled.Value) Logger.Warn($"{mc.Slot.Name} Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}, so we won't be able to use this as a collider for Mario :(");
            Dispose();
            return;
        }

        World = col.World;
        Context = instance;
        Collider = col;
        OriginalTag = col.Slot.Tag;

        Position = col.Slot.GlobalPosition;
        Rotation = col.Slot.GlobalRotation;
        InitScale = col.Slot.GlobalScale;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.ParseTagParts(tagParts, out SurfaceType surfaceType, out TerrainType terrainType, out _, out int force, out _);

        SurfaceType = surfaceType;
        TerrainType = terrainType;
        Force = force;

        IsPlayer = col.Type.Value == ColliderType.CharacterController && col.Slot.GetComponent<UserRoot>() != null;

        List<SM64Surface> surfaces = new List<SM64Surface>();
        Utils.GetScaledSurfaces(surfaces, col, SurfaceType, TerrainType, SurfaceFlag.Dynamic, Force);
        ObjectId = SM64Interop.SurfaceObjectCreate(col.Slot.GlobalPosition, col.Slot.GlobalRotation, surfaces.ToArray());

        LastChangedTime = col.World.Time.AbsoluteWorldTime;
    }

    private bool UpdateCurrentPositionData()
    {
        if (IsDisposed || Collider?.Slot == null) return false;

        float3 currentPosition = Collider.Slot.GlobalPosition;
        floatQ currentRotation = Collider.Slot.GlobalRotation;

        if (currentPosition == Position && currentRotation == Rotation) return false;

        Position = currentPosition;
        Rotation = currentRotation;

        return true;
    }

    internal void ContextFixedUpdateSynced()
    {
        if (UpdateCurrentPositionData())
        {
            SM64Interop.SurfaceObjectMove(ObjectId, Position, Rotation);
        }
    }

    ~SM64DynamicCollider()
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
            Context?.UnregisterDynamicCollider(Collider);

            Context = null;
            Collider = null;
            World = null;
        }

        if (SM64Interop.IsGlobalInit)
        {
            SM64Interop.SurfaceObjectDelete(ObjectId);
        }

        IsDisposed = true;
    }
}