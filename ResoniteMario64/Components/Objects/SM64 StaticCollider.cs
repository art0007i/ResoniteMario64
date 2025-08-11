using System;
using FrooxEngine;
using ResoniteMario64.Components.Context;
using ResoniteMario64.Components.Interfaces;
using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64.Components.Objects;

public sealed class SM64StaticCollider : ISM64Object
{
    public readonly SM64SurfaceType SurfaceType;
    public readonly SM64TerrainType TerrainType;
    public readonly int Force;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    public bool IsDisposed { get; private set; }

    public SM64StaticCollider(Collider col, SM64Context instance)
    {
        World = col.World;
        Context = instance;
        Collider = col;
        
        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.TryParseTagParts(tagParts, out SurfaceType, out TerrainType, out _, out Force);
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
            Context?.UnregisterStaticCollider(Collider);

            World = null;
            Context = null;
            Collider = null;
        }

        IsDisposed = true;
    }
}