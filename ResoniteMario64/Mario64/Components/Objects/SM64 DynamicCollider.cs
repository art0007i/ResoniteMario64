using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Mario64.libsm64.SM64Constants;

namespace ResoniteMario64.Mario64.Components.Objects;

public sealed class SM64DynamicCollider : ISM64Object, ISM64Collider
{
    public SM64SurfaceType SurfaceType { get; }
    public SM64TerrainType TerrainType { get; }
    public int Force { get; }

    public readonly uint ObjectId;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    public float3 Position { get; private set; }
    public floatQ Rotation { get; private set; }
    public float3 InitScale { get; }

    public bool IsDisposed { get; private set; }

    public SM64DynamicCollider(Collider col, SM64Context instance)
    {
        World = col.World;
        Context = instance;
        Collider = col;

        Position = col.Slot.GlobalPosition;
        Rotation = col.Slot.GlobalRotation;
        InitScale = col.Slot.GlobalScale;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.TryParseTagParts(tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType, out _, out int force);
        
        SurfaceType = surfaceType;
        TerrainType = terrainType;
        Force = force;

        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Utils.CheckDebug()) Logger.Warn($"{mc.Slot.Name} Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}, so we won't be able to use this as a collider for Mario :(");
            Dispose();
            return;
        }

        List<SM64Surface> surfaces = new List<SM64Surface>();
        Utils.GetScaledSurfaces(col, surfaces, SurfaceType, TerrainType, Force);
        ObjectId = Interop.SurfaceObjectCreate(col.Slot.GlobalPosition, col.Slot.GlobalRotation, surfaces.ToArray());
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
            Interop.SurfaceObjectMove(ObjectId, Position, Rotation);
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

        if (Interop.IsGlobalInit)
        {
            Interop.SurfaceObjectDelete(ObjectId);
        }

        IsDisposed = true;
    }
}