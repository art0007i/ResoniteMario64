using ResoniteMario64.libsm64;

namespace ResoniteMario64.Components.Context;

public partial class SM64Context
{
    public static void QueueStaticSurfacesUpdate()
    {
        // TODO: implement buffer (so it will execute the update after 1.5s, and you can call it multiple times within that time)
        if (Instance == null) return;

        Instance.StaticTerrainUpdate();
    }

    private void StaticTerrainUpdate()
    {
        if (Instance == null) return;

        Interop.StaticSurfacesLoad(Utils.GetAllStaticSurfaces(World));
    }

    /*public static bool RegisterSurfaceObject(SM64ColliderDynamic surfaceObject)
    {
        if (!EnsureInstanceExists(surfaceObject.World)) return false;

        lock (_instance._surfaceObjects)
        {
            if (!_instance._surfaceObjects.Contains(surfaceObject))
            {
                _instance._surfaceObjects.Add(surfaceObject);
            }
        }

        return true;
    }

    public static void UnregisterSurfaceObject(SM64ColliderDynamic surfaceObject)
    {
        if (_instance == null) return;

        lock (_instance._surfaceObjects)
        {
            if (_instance._surfaceObjects.Contains(surfaceObject))
            {
                _instance._surfaceObjects.Remove(surfaceObject);
            }
        }
    }*/
}