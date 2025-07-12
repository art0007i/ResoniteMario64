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

    private static bool IsGoodCollider(Collider col) =>
            // Ignore disabled
            col.Enabled
            && col.Slot.IsActive
            // Ignore non character colliders and non Tagged Colliders
            && (col.CharacterCollider.Value || col.Slot.Tag?.Contains("SM64 Collider") is true)
            // Ignore triggers
            && col.Type.Value != ColliderType.Trigger;

    internal static SM64Surface[] GetAllStaticSurfaces(World wld)
    {
        List<SM64Surface> surfaces = new List<SM64Surface>();
        List<(MeshCollider collider, SM64SurfaceType surfaceType, SM64TerrainType terrainType)> meshColliders = new List<(MeshCollider, SM64SurfaceType, SM64TerrainType)>();

        foreach (Collider obj in wld.RootSlot.GetComponentsInChildren<Collider>())
        {
            if (!IsGoodCollider(obj)) continue;

            SM64SurfaceType surfaceType = SM64SurfaceType.Default;
            SM64TerrainType terrainType = SM64TerrainType.Grass;

            string[] tagParts = obj.Slot.Tag?.Split(',');
            if (tagParts != null)
            {
                foreach (string part in tagParts)
                {
                    string trimmed = part.Trim();

                    if (trimmed.StartsWith("SurfaceType_", StringComparison.OrdinalIgnoreCase))
                    {
                        string enumName = trimmed.Substring("SurfaceType_".Length);
                        if (Enum.TryParse(enumName, true, out SM64SurfaceType parsedSurface))
                        {
                            surfaceType = parsedSurface;
                        }
                    }
                    else if (trimmed.StartsWith("TerrainType_", StringComparison.OrdinalIgnoreCase))
                    {
                        string enumName = trimmed.Substring("TerrainType_".Length);
                        if (Enum.TryParse(enumName, true, out SM64TerrainType parsedTerrain))
                        {
                            terrainType = parsedTerrain;
                        }
                    }
                }
            }

            if (obj is MeshCollider meshCollider)
            {
                meshColliders.Add((meshCollider, surfaceType, terrainType));
            }
            else
            {
                GetTransformedSurfaces(obj, surfaces, surfaceType, terrainType);
            }
        }

        // Ignore all meshes colliders with a null shared mesh, or non-readable
        List<(MeshCollider collider, SM64SurfaceType surfaceType, SM64TerrainType terrainType)> nonReadableMeshColliders = meshColliders.Where(mc => mc.collider.Mesh.Target == null || !mc.collider.Mesh.IsAssetAvailable).ToList();
#if DEBUG
        foreach (var invalid in nonReadableMeshColliders)
        {
            ResoniteMod.Warn($"[MeshCollider] {invalid.collider.Slot.Name} Mesh is " +
                             $"{(invalid.collider.Mesh.Target == null ? "null" : "non-readable")}, " +
                             "so we won't be able to use this as a collider for Mario :(");
        }
#endif
        meshColliders.RemoveAll(mc => mc.collider.Mesh.Target == null || !mc.collider.Mesh.IsAssetAvailable);

        // Sort the meshColliders list by the length of their triangles array in ascending order
        meshColliders.Sort((a, b) => a.collider.Mesh.Asset.Data.TotalTriangleCount.CompareTo(b.collider.Mesh.Asset.Data.TotalTriangleCount));

        // Add the mesh colliders until we reach the max mesh collider polygon limit
        int maxTris = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMaxMeshColliderTris);
        int totalMeshColliderTris = 0;
        
        foreach ((MeshCollider meshCollider, SM64SurfaceType surfaceType, SM64TerrainType terrainType) in meshColliders)
        {
            int meshTrisCount = meshCollider.Mesh.Asset.Data.TotalTriangleCount;
            int newTotalMeshColliderTris = totalMeshColliderTris + meshTrisCount;
            if (newTotalMeshColliderTris > maxTris)
            {
#if DEBUG
                ResoniteMod.Debug("[MeshCollider] Collider has too many triangles. " + meshCollider);
#endif
                continue;
            }

#if DEBUG
            ResoniteMod.Debug($"[MeshCollider] Adding mesh collider. (Remaining tris: {maxTris - newTotalMeshColliderTris}) " + meshCollider);   
#endif

            GetTransformedSurfaces(meshCollider, surfaces, surfaceType, terrainType);
            totalMeshColliderTris = newTotalMeshColliderTris;
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