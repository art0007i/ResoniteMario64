using System;
using FrooxEngine;
using ResoniteMario64.Components.Context;
using ResoniteMario64.Components.Interfaces;
using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64.Components.Objects;

public sealed class SM64Interactable : ISM64Object
{
    public readonly SM64InteractableType Type;

    public readonly int TypeId;
    public bool HasValue => TypeId != -1;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }

    public bool IsDisposed { get; private set; }

    public SM64Interactable(Collider col, SM64Context instance)
    {
        World = col.World;
        Context = instance;
        Collider = col;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.TryParseTagParts(tagParts, out _, out _, out Type, out TypeId);

        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Utils.CheckDebug()) Logger.Warn($"[Interactable{mc.GetType()}] {mc.Slot.Name} ({mc.ReferenceID}) Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}");
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
        if (IsDisposed) return;

        if (disposing)
        {
            Context.UnregisterInteractable(Collider);

            World = null;
            Context = null;
            Collider = null;
        }

        IsDisposed = true;
    }
}