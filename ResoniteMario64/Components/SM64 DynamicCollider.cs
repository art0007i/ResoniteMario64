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

    private readonly uint _surfaceObjectId;

    public World World { get; }
    public SM64Context Context { get; }

    public Collider Collider { get; }
    public float3 InitScale { get; }

    private float3 LastPosition { get; set; }
    private floatQ LastRotation { get; set; }

    private float3 GlobalPosition
    {
        get
        {
            try
            {
                return Collider?.Slot?.GlobalPosition ?? float3.Zero;
            }
            catch
            {
                return float3.Zero;
            }
        }
    }
    private floatQ GlobalRotation
    {
        get
        {
            try
            {
                return Collider?.Slot?.GlobalRotation ?? floatQ.Identity;
            }
            catch
            {
                return floatQ.Identity;
            }
        }
    }

    private bool _enabled;
    private bool _disposed;

    public SM64DynamicCollider(Collider col)
    {
        World = col.World;
        Context = SM64Context.Instance;
        
        Collider = col;
        LastPosition = col.Slot.GlobalPosition;
        LastRotation = col.Slot.GlobalRotation;
        InitScale = col.Slot.GlobalScale;
        
        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.ParseTagParts(tagParts, out SurfaceType, out TerrainType);

        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Utils.CheckDebug()) ResoniteMod.Warn($"[DynamicMeshCollider] {mc.Slot.Name} Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}, so we won't be able to use this as a collider for Mario :(");
            Dispose();
            return;
        }
        
        col.Slot.OnPrepareDestroy += _ => { Dispose(); };
        
        List<SM64Surface> surfaces = Utils.GetScaledSurfaces(col, new List<SM64Surface>(), SurfaceType, TerrainType);
        _surfaceObjectId = Interop.SurfaceObjectCreate(col.Slot.GlobalPosition, col.Slot.GlobalRotation, surfaces.ToArray());

        _enabled = true;
    }

    private bool UpdateCurrentPositionData()
    {
        if (!_enabled || _disposed || Collider?.Slot == null) return false;

        if (GlobalPosition == LastPosition && GlobalRotation == LastRotation) return false;

        LastPosition = GlobalPosition;
        LastRotation = GlobalRotation;

        return true;
    }

    internal void ContextFixedUpdateSynced()
    {
        if (UpdateCurrentPositionData())
        {
            Interop.SurfaceObjectMove(_surfaceObjectId, GlobalPosition, GlobalRotation);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (Interop.IsGlobalInit)
        {
            Context.UnregisterDynamicCollider(Collider);
            Interop.SurfaceObjectDelete(_surfaceObjectId);
        }

        _enabled = false;
    }
}
