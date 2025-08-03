using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;
using ResoniteMario64.Components.Context;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64.Components;

public sealed class SM64DynamicCollider : IDisposable
{
    public readonly SM64SurfaceType SurfaceType;
    public readonly SM64TerrainType TerrainType;

    public readonly uint ObjectId;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    private float3 LastPosition { get; set; }
    private floatQ LastRotation { get; set; }
    public float3 InitScale { get; }

    private bool _disposed;

    public SM64DynamicCollider(Collider col, SM64Context instance)
    {
        World = col.World;
        Context = instance;
        Collider = col;
        
        LastPosition = col.Slot.GlobalPosition;
        LastRotation = col.Slot.GlobalRotation;
        InitScale = col.Slot.GlobalScale;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.TryParseTagParts(tagParts, out SurfaceType, out TerrainType, out _, out _);

        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Utils.CheckDebug()) ResoniteMod.Warn($"[DynamicMeshCollider] {mc.Slot.Name} Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}, so we won't be able to use this as a collider for Mario :(");
            Dispose();
            return;
        }
        
        List<SM64Surface> surfaces = Utils.GetScaledSurfaces(col, new List<SM64Surface>(), SurfaceType, TerrainType);
        ObjectId = Interop.SurfaceObjectCreate(col.Slot.GlobalPosition, col.Slot.GlobalRotation, surfaces.ToArray());
    }

    private bool UpdateCurrentPositionData()
    {
        if (_disposed || Collider?.Slot == null) return false;

        float3 currentPosition = Collider.Slot.GlobalPosition;
        floatQ currentRotation = Collider.Slot.GlobalRotation;
        
        if (currentPosition == LastPosition && currentRotation == LastRotation) return false;

        LastPosition = currentPosition;
        LastRotation = currentRotation;

        return true;
    }

    internal void ContextFixedUpdateSynced()
    {
        if (UpdateCurrentPositionData())
        {
            Interop.SurfaceObjectMove(ObjectId, LastPosition, LastRotation);
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
        if (_disposed) return;

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
        
        _disposed = true;
    }
}