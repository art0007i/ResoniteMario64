using System.Diagnostics;
using Elements.Core;
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

    public bool IsDisposed { get; private set; }

    private readonly Stopwatch _teleporterWatch = new Stopwatch();

    public SM64Teleporter(Collider col, SM64Context instance)
    {
        World = col.World;
        Context = instance;
        Collider = col;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.TryParseTagParts(tagParts, out _, out _, out _, out ID, out Group);
    }

    public void Handle(SM64Mario mario)
    {
        if (mario.IsTeleporting || Collider?.Slot is not { IsActive: true }) return;

        if (!mario.IsInCollider(this))
        {
            if (mario.LastTeleportDestination == this)
            {
                mario.LastTeleportDestination = null;
            }

            _teleporterWatch.Reset();
            return;
        }

        if (mario.LastTeleportDestination == this || (mario.CurrentActionFlags & (uint)SM64Constants.ActionFlag.Stationary) == 0)
        {
            _teleporterWatch.Reset();
            return;
        }

        if (!_teleporterWatch.IsRunning)
        {
            _teleporterWatch.Start();
            return;
        }

        if (_teleporterWatch.Elapsed.TotalSeconds < 1f) return;

        _teleporterWatch.Reset();

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