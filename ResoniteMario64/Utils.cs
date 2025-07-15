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

    public static bool HasCapType(uint flags, MarioCapType capType)
    {
        return capType switch
        {
            MarioCapType.VanishCap => (flags & (uint)StateFlag.VanishCap) != 0,
            MarioCapType.MetalCap  => (flags & (uint)StateFlag.MetalCap) != 0,
            MarioCapType.WingCap   => (flags & (uint)StateFlag.WingCap) != 0,
            _                      => capType == MarioCapType.NormalCap
        };
    }

    public static void TransformAndGetSurfaces(List<SM64Surface> outSurfaces, MeshX mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<float3, float3> transformFunc)
    {
        for (int subMeshIndex = 0; subMeshIndex < mesh.SubmeshCount; subMeshIndex++)
        {
            Submesh submesh = mesh.GetSubmesh(subMeshIndex);
            float3[] vertices = mesh.Vertices.Select(v => transformFunc(v.Position)).ToArray();
            Interop.CreateAndAppendSurfaces(outSurfaces, submesh.RawIndicies, vertices, surfaceType, terrainType);
        }
    }

    public static bool IsGoodStaticCollider(Collider col) =>
            col.Enabled
            && col.Slot.IsActive
            && CollidesWithCharacters(col) || col.Slot.Tag?.Contains("SM64 StaticCollider") is true
            && col.Slot.Tag?.Contains("SM64 DynamicCollider") is not true;

    public static bool IsGoodDynamicCollider(Collider col) =>
            col.Enabled 
            && col.Slot.IsActive
            && col.Type.Value != ColliderType.Trigger
            && col.Slot.Tag?.Contains("SM64 DynamicCollider") is true
            && col.Slot.Tag?.Contains("SM64 StaticCollider") is not true;

    public static bool CollidesWithCharacters(Collider col) => ((ICollider)col).CollidesWithCharacters;

    internal static SM64Surface[] GetAllStaticSurfaces(World wld)
    {
        List<SM64Surface> surfaces = new List<SM64Surface>();
        List<(MeshCollider collider, SM64SurfaceType surfaceType, SM64TerrainType terrainType)> meshColliders = new List<(MeshCollider, SM64SurfaceType, SM64TerrainType)>();

        foreach (Collider obj in wld.RootSlot.GetComponentsInChildren<Collider>())
        {
            if (!IsGoodStaticCollider(obj)) continue;

            string[] tagParts = obj.Slot.Tag?.Split(',');
            Utils.ParseTagParts(tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType);

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

        foreach (var invalid in nonReadableMeshColliders)
        {
            if (Utils.CheckDebug())
                ResoniteMod.Warn($"[MeshCollider] {invalid.collider.Slot.Name} Mesh is " +
                                 $"{(invalid.collider.Mesh.Target == null ? "null" : "non-readable")}, " +
                                 "so we won't be able to use this as a collider for Mario :(");
        }

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
                if (Utils.CheckDebug()) ResoniteMod.Warn("[MeshCollider] Collider has too many triangles. " + meshCollider);
                continue;
            }

            GetTransformedSurfaces(meshCollider, surfaces, surfaceType, terrainType);
            totalMeshColliderTris = newTotalMeshColliderTris;
        }

        return surfaces.ToArray();
    }
    
    public static void ParseTagParts(string[] tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType)
    {
        surfaceType = SM64SurfaceType.Default;
        terrainType = SM64TerrainType.Grass;
        
        if (tagParts == null) return;
        
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

    public static bool CheckDebug() => ResoniteMod.IsDebugEnabled();

    public static User GetAllocatingUser(this Slot slot)
    {
        slot.ReferenceID.ExtractIDs(out _, out byte userByte);
        return slot.World.GetUserByAllocationID(userByte);
    }
}