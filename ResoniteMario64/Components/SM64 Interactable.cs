using System;
using FrooxEngine;
using ResoniteMario64.Components.Context;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64.Components;

public sealed class SM64Interactable : IDisposable
{
    public readonly SM64InteractableType Type;

    public readonly int TypeId;
    public bool HasValue => TypeId != -1;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    private bool _disposed;

    public SM64Interactable(Collider col, SM64Context instance)
    {
        World = col.World;
        Context = instance;
        Collider = col;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.TryParseTagParts(tagParts, out _, out _, out Type, out TypeId);

        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Utils.CheckDebug()) ResoniteMod.Warn($"[InteractMeshCollider] {mc.Slot.Name} Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}, so we won't be able to use this as a collider for Mario :(");
            Dispose();
        }
    }
    
    ~SM64Interactable()
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
            Context.UnregisterInteractable(Collider);

            World = null;
            Context = null;
            Collider = null;
        }
        
        _disposed = true;
    }
}