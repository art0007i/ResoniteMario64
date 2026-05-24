using System.Diagnostics;
using System.Runtime.CompilerServices;
using FrooxEngine;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.libsm64;

namespace ResoniteMario64.Mario64.Components.Objects;

public sealed class SM64Teleporter : ISM64Object
{
    public readonly int ID;
    public readonly int Group;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }
    public string OriginalTag { get; }

    public bool IsDisposed { get; private set; }

    private readonly ConditionalWeakTable<SM64Mario, Stopwatch> _marioWatches = new ConditionalWeakTable<SM64Mario, Stopwatch>();

    public SM64Teleporter(Collider col, SM64Context instance)
    {
        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Config.DebugEnabled.Value) Logger.Warn($"[Teleporter{mc.GetType()}] {mc.Slot.Name} ({mc.ReferenceID}) Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}");
            Dispose();
            return;
        }

        World = col.World;
        Context = instance;
        Collider = col;
        OriginalTag = col.Slot.Tag;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.ParseTagParts(tagParts, out _, out _, out _, out ID, out Group);
    }

    public void Handle(SM64Mario mario)
    {
        if (mario.IsTeleporting || Collider?.Slot is not { IsActive: true }) return;

        Stopwatch teleporterWatch = _marioWatches.GetOrCreateValue(mario);
        if (teleporterWatch == null) return;

        if (!mario.IsInCollider(this))
        {
            if (mario.LastTeleportDestination == this)
            {
                Logger.Warn("Resetting teleport destination");
                mario.LastTeleportDestination = null;
            }

            teleporterWatch.Reset();
            return;
        }

        if (mario.LastTeleportDestination == this || !mario.CurrentActionFlags.HasFlag(SM64Constants.ActionFlag.Stationary))
        {
            teleporterWatch.Reset();
            return;
        }

        if (!teleporterWatch.IsRunning)
        {
            teleporterWatch.Start();
            return;
        }

        if (teleporterWatch.Elapsed.TotalSeconds < 1f) return;

        teleporterWatch.Reset();

        List<SM64Teleporter> group = Context.Teleporters.Values.Where(x => x.Group == Group).GetTempList();
        group.Sort((a, b) => a.ID.CompareTo(b.ID));

        int index = group.FindIndex(x => x.ID == ID);
        if (index < 0) return;

        int nextIndex = (index + 1) % group.Count;
        SM64Teleporter next = group[nextIndex];
        if (next == null) return;

        mario.TeleportTo(next.Collider.Slot.GlobalPosition);

        mario.LastTeleportDestination = next;
    }

    ~SM64Teleporter()
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
            Context.UnregisterTeleporter(Collider);

            World = null;
            Context = null;
            Collider = null;
        }

        IsDisposed = true;
    }
}