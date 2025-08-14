using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64.Components.Interfaces;

public interface ISM64Collider
{
    SM64SurfaceType SurfaceType { get; }
    SM64TerrainType TerrainType { get; }
    int Force { get; }
}