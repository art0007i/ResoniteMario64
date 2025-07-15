using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64;

public static class Utils
{
    public static void TransformAndGetSurfaces(List<SM64Surface> outSurfaces, MeshX mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<float3, float3> transformFunc)
    {
        for (int subMeshIndex = 0; subMeshIndex < mesh.SubmeshCount; subMeshIndex++)
        {
            Submesh submesh = mesh.GetSubmesh(subMeshIndex);
            float3[] vertices = mesh.Vertices.Select(v => transformFunc(v.Position)).ToArray();
            Interop.CreateAndAppendSurfaces(outSurfaces, submesh.RawIndicies, vertices, surfaceType, terrainType);
        }
    }

    public static bool IsGoodStaticCollider(Collider col)
    {
        return col.Enabled && col.Slot.IsActive && CollidesWithCharacters(col) || (col.Slot.Tag?.Contains("SM64 StaticCollider") is true || col.Slot.Tag?.Contains("SM64 Collider") is true) && col.Slot.Tag?.Contains("SM64 DynamicCollider") is not true;
    }

    public static bool IsGoodDynamicCollider(Collider col)
    {
        return col.Enabled && col.Slot.IsActive && col.Type.Value != ColliderType.Trigger && col.Slot.Tag?.Contains("SM64 DynamicCollider") is true && col.Slot.Tag?.Contains("SM64 StaticCollider") is not true;
    }

    private static bool CollidesWithCharacters(Collider col) => ((ICollider)col).CollidesWithCharacters;

    internal static SM64Surface[] GetAllStaticSurfaces(World wld)
    {
        List<SM64Surface> surfaces = new List<SM64Surface>();
        List<(MeshCollider collider, SM64SurfaceType, SM64TerrainType)> meshColliders = new List<(MeshCollider, SM64SurfaceType, SM64TerrainType)>();

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

        // Print all MeshColliders that are Null or Non-Readable
        if (Utils.CheckDebug())
        {
            meshColliders.Where(InvalidCollider).Do(invalid =>
            {
                ResoniteMod.Warn($"[MeshCollider] {invalid.collider.Slot.Name} Mesh is {(invalid.collider.Mesh.Target == null ? "null" : "non-readable")}, so we won't be able to use this as a collider for Mario :(");
            });
        }

        // Remove all MeshColliders that are Null or Non-Readable
        meshColliders.RemoveAll(InvalidCollider);

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

        bool InvalidCollider((MeshCollider collider, SM64SurfaceType, SM64TerrainType) col)
            => col.collider.Mesh.Target == null || !col.collider.Mesh.IsAssetAvailable;
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
                string enumName = trimmed["SurfaceType_".Length..];
                if (Enum.TryParse(enumName, true, out SM64SurfaceType parsedSurface))
                {
                    surfaceType = parsedSurface;
                }
            }
            else if (trimmed.StartsWith("TerrainType_", StringComparison.OrdinalIgnoreCase))
            {
                string enumName = trimmed["TerrainType_".Length..];
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

    public static User GetAllocatingUser(this Slot slot)
    {
        slot.ReferenceID.ExtractIDs(out _, out byte userByte);
        return slot.World.GetUserByAllocationID(userByte);
    }
}