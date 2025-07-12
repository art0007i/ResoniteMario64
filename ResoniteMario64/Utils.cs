using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64;

public static class Utils
{
    public static Dictionary<TKey, TValue> GetTempDictionary<TKey, TValue>(this Dictionary<TKey, TValue> source) => new Dictionary<TKey, TValue>(source);

    public static void TransformAndGetSurfaces(List<SM64Surface> outSurfaces, MeshX mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<float3, float3> transformFunc)
    {
        for (int subMeshIndex = 0; subMeshIndex < mesh.SubmeshCount; subMeshIndex++)
        {
            Submesh submesh = mesh.GetSubmesh(subMeshIndex);
            float3[] vertices = mesh.Vertices.Select(v => transformFunc(v.Position)).ToArray();
            Interop.CreateAndAppendSurfaces(outSurfaces, submesh.RawIndicies, vertices, surfaceType, terrainType);
        }
    }

    public static bool IsGoodCollider(Collider col) =>
            // Ignore disabled
            col.Enabled
            && col.Slot.IsActive
            // Ignore non character colliders and non Tagged Colliders
            && (col.CharacterCollider.Value || col.Slot.Tag == "SM64 Collider")
            // Ignore triggers
            && col.Type.Value != ColliderType.Trigger;

    internal static SM64Surface[] GetAllStaticSurfaces(World wld)
    {
        List<SM64Surface> surfaces = new List<SM64Surface>();
        List<MeshCollider> meshColliders = new List<MeshCollider>();

        foreach (Collider obj in wld.RootSlot.GetComponentsInChildren<Collider>())
        {
            // Ignore bad colliders
            if (!IsGoodCollider(obj)) continue;

            // TODO: Handle dynamic colliders somehow

            // Check if we have surface and terrain data
            SM64SurfaceType surfaceType = SM64SurfaceType.Default;
            SM64TerrainType terrainType = SM64TerrainType.Grass;
            
            // ResoniteMod.Debug($"[GoodCollider] {obj.name}");

            if (obj is MeshCollider meshCollider)
            {
                // Let's do some more processing to the mesh colliders without dedicated components
                meshColliders.Add(meshCollider);
            }
            else
            {
                // Everything else, let's just add (probably a bad idea)
                GetTransformedSurfaces(obj, surfaces, surfaceType, terrainType);
            }
        }

        // Ignore all meshes colliders with a null shared mesh, or non-readable
        List<MeshCollider> nonReadableMeshColliders = meshColliders.Where(meshCollider => meshCollider.Mesh.Target == null || !meshCollider.Mesh.IsAssetAvailable).ToList();
#if DEBUG
        foreach (MeshCollider invalidMeshCollider in nonReadableMeshColliders)
        {
            ResoniteMod.Warn($"[MeshCollider] {invalidMeshCollider.Slot.Name} Mesh is " +
                                 $"{(invalidMeshCollider.Mesh.Target == null ? "null" : "non-readable")}, " +
                                 "so we won't be able to use this as a collider for Mario :(");
        }
#endif
        meshColliders.RemoveAll(meshCollider => meshCollider.Mesh.Target == null || !meshCollider.Mesh.IsAssetAvailable);

        // Sort the meshColliders list by the length of their triangles array in ascending order
        meshColliders.Sort((a, b) => a.Mesh.Asset.Data.TotalTriangleCount.CompareTo(b.Mesh.Asset.Data.TotalTriangleCount));

        // Add the mesh colliders until we reach the max mesh collider polygon limit
        int maxTris = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMaxMeshColliderTris);
        int totalMeshColliderTris = 0;
        foreach (MeshCollider meshCollider in meshColliders)
        {
            int meshTrisCount = meshCollider.Mesh.Asset.Data.TotalTriangleCount;
            int newTotalMeshColliderTris = totalMeshColliderTris + meshTrisCount;
            if (newTotalMeshColliderTris > maxTris)
            {
                ResoniteMod.Debug("[MeshCollider] Collider has too many triangles. " + meshCollider);
                continue;
            }

            ResoniteMod.Debug($"[MeshCollider] Adding mesh collider. (Remaining tris: {maxTris - newTotalMeshColliderTris}) " + meshCollider);

            GetTransformedSurfaces(meshCollider, surfaces, SM64SurfaceType.Default, SM64TerrainType.Grass);
        }

        return surfaces.ToArray();
    }

    // Function used for static colliders. Returns correct global positions, rotations and scales.
    public static List<SM64Surface> GetTransformedSurfaces(Collider collider, List<SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, x => collider.Slot.LocalPointToGlobal(x + collider.Offset));

        return surfaces;
    }

    // Function used for dynamic colliders. Returns correct scales. (rotation and position is set dynamically)
    internal static List<SM64Surface> GetScaledSurfaces(Collider collider, List<SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, x => collider.Slot.GlobalScale * (x + collider.Offset));

        return surfaces;
    }
}

