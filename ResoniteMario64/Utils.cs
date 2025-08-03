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
    public static readonly colorX VanishCapColor = new colorX(1, 1, 1, 0.5f);
    public static readonly colorX ColorXWhite = colorX.White;

    public static readonly float2 Float2Zero = float2.Zero;
    public static readonly float2 Float2One = float2.One;
    public static readonly float2 Float2NegOne = -float2.One;
    public static readonly float2 Float2Up = new float2(0, 1);
    public static readonly float2 Float2Down = new float2(0, -1);
    public static readonly float2 Float2Left = new float2(1);
    public static readonly float2 Float2Right = new float2(-1);

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

    public static bool IsGoodWaterBox(Collider col)
    {
        return col.Enabled && col.Slot.IsActive && col.Slot.Tag?.Contains("SM64 WaterBox") is true;
    }

    public static bool IsGoodInteractable(Collider col)
    {
        return col.Enabled && col.Slot.IsActive && col.Slot.Tag?.Contains("SM64 Interactable") is true;
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
            Utils.TryParseTagParts(tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType, out _, out _);

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
            meshColliders.Where(InvalidCollider).Do(invalid => { ResoniteMod.Warn($"[MeshCollider] {invalid.collider.Slot.Name} Mesh is {(invalid.collider.Mesh.Target == null ? "null" : "non-readable")}, so we won't be able to use this as a collider for Mario :("); });
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

        bool InvalidCollider((MeshCollider collider, SM64SurfaceType, SM64TerrainType) col) => col.collider.Mesh.Target == null || !col.collider.Mesh.IsAssetAvailable;
    }

    public static void TryParseTagParts(string[] tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType, out SM64InteractableType interactableType, out int interactableId)
    {
        surfaceType = SM64SurfaceType.Default;
        terrainType = SM64TerrainType.Grass;
        interactableType = SM64InteractableType.None;
        interactableId = -1;

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
            else if (trimmed.StartsWith("InteractableType_", StringComparison.OrdinalIgnoreCase))
            {
                string enumSuffix = trimmed.Substring("InteractableType_".Length);

                int splitIndex = enumSuffix.Length;
                while (splitIndex > 0 && char.IsDigit(enumSuffix[splitIndex - 1]))
                {
                    splitIndex--;
                }

                string enumName = enumSuffix.Substring(0, splitIndex);
                string indexString = enumSuffix.Substring(splitIndex);

                if (Enum.TryParse(enumName, true, out SM64InteractableType parsedInteractable))
                {
                    interactableType = parsedInteractable;

                    if (int.TryParse(indexString, out int parsedIndex))
                    {
                        interactableId = parsedIndex;
                    }
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

    public static bool Overlaps(BoundingBox a, BoundingBox b)
    {
        return (a.min.X <= b.max.X && a.max.X >= b.min.X) &&
               (a.min.Y <= b.max.Y && a.max.Y >= b.min.Y) &&
               (a.min.Z <= b.max.Z && a.max.Z >= b.min.Z);
    }

    public static bool CheckDebug() => ResoniteMod.IsDebugEnabled();

    public static Dictionary<TKey, TValue> GetTempDictionary<TKey, TValue>(this Dictionary<TKey, TValue> source) => new Dictionary<TKey, TValue>(source);

    public static List<T> GetTempList<T>(this List<T> source) => new List<T>(source);

    public static List<T> GetTempList<T>(this IEnumerable<T> source) => new List<T>(source);

    public static bool HasCapType(uint flags, MarioCapType capType)
    {
        return capType switch
        {
            MarioCapType.VanishCap => (flags & (uint)StateFlag.VanishCap) != 0,
            MarioCapType.MetalCap  => (flags & (uint)StateFlag.MetalCap) != 0,
            MarioCapType.WingCap   => (flags & (uint)StateFlag.WingCap) != 0,
            MarioCapType.NormalCap => (flags & (uint)StateFlag.NormalCap) != 0,
            _                      => throw new ArgumentOutOfRangeException(nameof(capType), capType, null)
        };
    }

    public static User GetAllocatingUser(this Slot slot)
    {
        slot.ReferenceID.ExtractIDs(out _, out byte userByte);
        return slot.World.GetUserByAllocationID(userByte);
    }
}