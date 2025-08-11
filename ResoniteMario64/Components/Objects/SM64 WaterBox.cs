using System;
using FrooxEngine;
using ResoniteMario64.Components.Context;
using ResoniteMario64.Components.Interfaces;

namespace ResoniteMario64.Components.Objects;

public sealed class SM64WaterBox : ISM64Object
{
    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }
    
    public bool IsDisposed { get; private set; }
    
    public SM64WaterBox(Collider col, SM64Context instance)
    {
        World = col.World;
        Context = instance;
        Collider = col;
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
            Context?.UnregisterWaterBox(Collider);

            World = null;
            Context = null;
            Collider = null;
        }

        IsDisposed = true;
    }
}
