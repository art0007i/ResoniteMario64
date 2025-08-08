using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.Components;
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

    public static void TransformAndGetSurfaces(List<SM64Surface> outSurfaces, MeshX mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force, Func<float3, float3> transformFunc)
    {
        for (int subMeshIndex = 0; subMeshIndex < mesh.SubmeshCount; subMeshIndex++)
        {
            Submesh submesh = mesh.GetSubmesh(subMeshIndex);
            float3[] vertices = mesh.Vertices.Select(v => transformFunc(v.Position)).ToArray();
            Interop.CreateAndAppendSurfaces(outSurfaces, submesh.RawIndicies, vertices, surfaceType, terrainType, force);
        }
    }

    public static bool IsGoodStaticCollider(Collider col)
    {
        if (!IsActive(col)) return false;

        bool hasStaticTag = HasTag(col, "SM64 StaticCollider") || HasTag(col, "SM64 Collider");
        bool hasDynamicTag = HasTag(col, "SM64 DynamicCollider");

        return CollidesWithCharacters(col) || hasStaticTag && !hasDynamicTag;
    }

    public static bool IsGoodDynamicCollider(Collider col)
    {
        if (!IsActive(col)) return false;

        bool hasDynamicTag = HasTag(col, "SM64 DynamicCollider");
        bool hasStaticTag = HasTag(col, "SM64 StaticCollider") || HasTag(col, "SM64 Collider");

        return col.Type.Value != ColliderType.Trigger && hasDynamicTag && !hasStaticTag;
    }

    public static bool IsGoodWaterBox(Collider col) => IsActive(col) && HasTag(col, "SM64 WaterBox");

    public static bool IsGoodInteractable(Collider col) => col.Enabled && HasTag(col, "SM64 Interactable");

    private static bool IsActive(Collider col) => col.Enabled && col.Slot.IsActive;
    private static bool HasTag(Collider col, string tag) => col.Slot.Tag?.Contains(tag) == true;

    private static bool CollidesWithCharacters(Collider col) => ((ICollider)col).CollidesWithCharacters;

    internal static SM64Surface[] GetAllStaticSurfaces(World wld)
    {
        List<SM64Surface> surfaces = new List<SM64Surface>();
        List<(MeshCollider collider, SM64SurfaceType, SM64TerrainType, int)> meshColliders = new List<(MeshCollider, SM64SurfaceType, SM64TerrainType, int)>();

        foreach (Collider obj in wld.RootSlot.GetComponentsInChildren<Collider>())
        {
            if (!IsGoodStaticCollider(obj)) continue;

            string[] tagParts = obj.Slot.Tag?.Split(',');
            Utils.TryParseTagParts(tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType, out _, out int force);

            if (obj is MeshCollider meshCollider)
            {
                meshColliders.Add((meshCollider, surfaceType, terrainType, force));
            }
            else
            {
                GetTransformedSurfaces(obj, surfaces, surfaceType, terrainType, force);
            }
        }

        // Print all MeshColliders that are Null or Non-Readable
        if (Utils.CheckDebug())
        {
            meshColliders.Where(InvalidCollider).Do(invalid => Logger.Warn($"- [{invalid.collider.GetType()}] {invalid.collider.Slot.Name} ({invalid.collider.ReferenceID}) Mesh is {(invalid.collider.Mesh.Target == null ? "null" : "non-readable")}"));
        }

        // Remove all MeshColliders that are Null or Non-Readable
        meshColliders.RemoveAll(InvalidCollider);

        // Sort the meshColliders list by the length of their triangles array in ascending order
        meshColliders.Sort((a, b) => a.collider.Mesh.Asset.Data.TotalTriangleCount.CompareTo(b.collider.Mesh.Asset.Data.TotalTriangleCount));

        // Add the mesh colliders until we reach the max mesh collider polygon limit
        int maxTris = ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMaxMeshColliderTris);
        int totalMeshColliderTris = 0;

        foreach ((MeshCollider meshCollider, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force) in meshColliders)
        {
            int meshTrisCount = meshCollider.Mesh.Asset.Data.TotalTriangleCount;
            int newTotalMeshColliderTris = totalMeshColliderTris + meshTrisCount;
            if (newTotalMeshColliderTris > maxTris)
            {
                if (Utils.CheckDebug()) Logger.Warn($"[{meshCollider.GetType()}] {meshCollider.Slot.Name} ({meshCollider.ReferenceID}) Mesh has too many triangles.");
                continue;
            }

            GetTransformedSurfaces(meshCollider, surfaces, surfaceType, terrainType, force);
            totalMeshColliderTris = newTotalMeshColliderTris;
        }

        return surfaces.ToArray();

        bool InvalidCollider((MeshCollider collider, SM64SurfaceType, SM64TerrainType, int) col) => col.collider.Mesh.Target == null || !col.collider.Mesh.IsAssetAvailable;
    }

    // Function used for static colliders. Returns correct global positions, rotations and scales.
    public static List<SM64Surface> GetTransformedSurfaces(Collider collider, List<SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, force, x => collider.Slot.LocalPointToGlobal(x + collider.Offset));

        return surfaces;
    }

    // Function used for dynamic colliders. Returns correct scales. (rotation and position is set dynamically)
    internal static List<SM64Surface> GetScaledSurfaces(Collider collider, List<SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, force, x => collider.Slot.GlobalScale * (x + collider.Offset));

        return surfaces;
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
            else if (trimmed.StartsWith("Force_", StringComparison.OrdinalIgnoreCase))
            {
                string idxString = trimmed.Substring("Force_".Length);
                if (int.TryParse(idxString, out int parsedIdx))
                {
                    interactableId = parsedIdx;
                }
            }
        }
    }
    
    public static bool CheckDebug() => ResoniteMod.IsDebugEnabled();

    public static Dictionary<TKey, TValue> GetTempDictionary<TKey, TValue>(this Dictionary<TKey, TValue> source) => new Dictionary<TKey, TValue>(source);

    public static List<T> GetTempList<T>(this List<T> source) => new List<T>(source);

    public static List<T> GetTempList<T>(this IEnumerable<T> source) => new List<T>(source);
    
    public static List<TValue> GetFilteredSortedList<TKey, TValue, TSortKey>(this Dictionary<TKey, TValue> source, Func<TValue, bool> filter = null, Func<TValue, TSortKey> sortKeySelector = null, bool ascending = true)
    {
        IEnumerable<TValue> query = source.Values;

        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (sortKeySelector != null)
        {
            query = ascending
                    ? query.OrderBy(sortKeySelector)
                    : query.OrderByDescending(sortKeySelector);
        }

        return query.ToList();
    }

    public static floatQ LookAt(this Slot target, float3 targetPoint)
    {
        return floatQ.LookRotation(target.Parent.GlobalPointToLocal(in targetPoint) - target.LocalPosition, float3.Up);
    }

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

    public static User GetAllocatingUser(this Slot slot) => slot.World.GetUserByAllocationID(slot.ReferenceID.User);
}