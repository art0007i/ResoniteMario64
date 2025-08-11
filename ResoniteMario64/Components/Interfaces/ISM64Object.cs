using System;
using FrooxEngine;
using ResoniteMario64.Components.Context;

namespace ResoniteMario64.Components.Interfaces;

public interface ISM64Object : IDisposable
{
    World World { get; }
    SM64Context Context { get; }
    Collider Collider { get; }
    bool IsDisposed { get; }
}