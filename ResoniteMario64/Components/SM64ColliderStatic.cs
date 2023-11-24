
using HarmonyLib;
using FrooxEngine;
using System;

namespace ResoniteMario64;

// This component can be placed on the same slot as any collider to change surface properties (lava n shit)
[Category("SM64")]
public class SM64ColliderStatic : Component {

    private readonly Sync<SM64TerrainType> terrainType;
    private readonly Sync<SM64SurfaceType> surfaceType;

    protected override void OnAttach()
    {
        base.OnAttach();
        terrainType.Value = SM64TerrainType.Grass;
        surfaceType.Value = SM64SurfaceType.Default;
    }

    public SM64TerrainType TerrainType => terrainType;
    public SM64SurfaceType SurfaceType => surfaceType;

    // TODO: on changes update colliders maybe?

}
