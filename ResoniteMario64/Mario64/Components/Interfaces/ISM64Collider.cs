using ResoniteMario64.Mario64.libsm64;

namespace ResoniteMario64.Mario64.Components.Interfaces;

public interface ISM64Collider
{
    SurfaceType SurfaceType { get; }
    TerrainType TerrainType { get; }
    int Force { get; }
}