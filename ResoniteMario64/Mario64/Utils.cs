using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Mario64.libsm64.SM64Constants;

namespace ResoniteMario64.Mario64;

public static class Utils
{
    internal static readonly List<Collider> StaticSurfaces = new List<Collider>(); 
    
    public static readonly colorX VanishCapColor = new colorX(1, 1, 1, 0.5f);
    
    public static void TransformAndGetSurfaces(List<SM64Surface> outSurfaces, MeshX mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force, Func<float3, float3> transformFunc)
    {
        for (int subMeshIndex = 0; subMeshIndex < mesh.SubmeshCount; subMeshIndex++)
        {
            Submesh submesh = mesh.GetSubmesh(subMeshIndex);
            float3[] vertices = mesh.Vertices.Select(v => transformFunc(v.Position)).ToArray();
            Interop.CreateAndAppendSurfaces(outSurfaces, submesh.RawIndicies, vertices, surfaceType, terrainType, force);
        }
    }
    
    public enum ColliderCategory
    {
        None,
        Static,
        Dynamic,
        WaterBox,
        Interactable
    }

    private static ColliderCategory GetColliderCategory(Collider col)
    {
        string tag = col.Slot.Tag ?? string.Empty;
        
        bool isStatic = tag.Contains("SM64 StaticCollider") || tag.Contains("SM64 Collider");
        bool isDynamic = tag.Contains("SM64 DynamicCollider");
        bool isWaterBox = tag.Contains("SM64 WaterBox");
        bool isInteractable = tag.Contains("SM64 Interactable");
        bool isValid = col.Enabled && col.Slot.IsActive;

        if ((isStatic || ((ICollider)col).CollidesWithCharacters) && !isDynamic && isValid)
            return ColliderCategory.Static;

        if (isDynamic && isValid)
            return ColliderCategory.Dynamic;

        if (isWaterBox && isValid)
            return ColliderCategory.WaterBox;

        if (isInteractable && col.Enabled)
            return ColliderCategory.Interactable;

        return ColliderCategory.None;
    }

    public static bool IsStaticCollider(Collider col) => GetColliderCategory(col) == ColliderCategory.Static;
    public static bool IsDynamicCollider(Collider col) => GetColliderCategory(col) == ColliderCategory.Dynamic && col.Type.Value != ColliderType.Trigger;
    public static bool IsWaterBox(Collider col) => GetColliderCategory(col) == ColliderCategory.WaterBox;
    public static bool IsInteractable(Collider col) => GetColliderCategory(col) == ColliderCategory.Interactable;
    
    internal static SM64Surface[] GetAllStaticSurfaces(World wld)
    {
        StaticSurfaces.Clear();
        
        List<SM64Surface> surfaces = new List<SM64Surface>();
        List<(MeshCollider collider, SM64SurfaceType, SM64TerrainType, int)> meshColliders = new List<(MeshCollider, SM64SurfaceType, SM64TerrainType, int)>();

        foreach (Collider col in wld.RootSlot.GetComponentsInChildren<Collider>())
        {
            if (!IsStaticCollider(col)) continue;

            string[] tagParts = col.Slot.Tag?.Split(',');
            Utils.TryParseTagParts(tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType, out _, out int force);

            if (col is MeshCollider meshCollider)
            {
                meshColliders.Add((meshCollider, surfaceType, terrainType, force));
            }
            else
            {
                GetTransformedSurfaces(col, surfaces, surfaceType, terrainType, force);
            }
            
            StaticSurfaces.Add(col);
        }

        // Print all MeshColliders that are Null or Non-Readable
        if (Utils.CheckDebug())
        {
            meshColliders.Where(InvalidCollider).Do(invalid =>
            {
                Logger.Warn($"- [{invalid.collider.GetType()}] {invalid.collider.Slot.Name} ({invalid.collider.ReferenceID}) Mesh is {(invalid.collider.Mesh.Target == null ? "null" : "non-readable")}");
                StaticSurfaces.Remove(invalid.collider);
            });
        }

        // Remove all MeshColliders that are Null or Non-Readable
        meshColliders.RemoveAll(InvalidCollider);

        // Sort the meshColliders list it by the length of their triangle's array in ascending order
        meshColliders.Sort((a, b) => a.collider.Mesh.Asset.Data.TotalTriangleCount.CompareTo(b.collider.Mesh.Asset.Data.TotalTriangleCount));

        // Add the mesh colliders until we reach the max mesh collider polygon limit
        int maxTris = Config.MaxMeshColliderTris.Value;
        int totalMeshColliderTris = 0;

        foreach ((MeshCollider meshCollider, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force) in meshColliders)
        {
            int meshTrisCount = meshCollider.Mesh.Asset.Data.TotalTriangleCount;
            int newTotalMeshColliderTris = totalMeshColliderTris + meshTrisCount;
            if (newTotalMeshColliderTris > maxTris)
            {
                if (Utils.CheckDebug()) Logger.Warn($"[{meshCollider.GetType()}] {meshCollider.Slot.Name} ({meshCollider.ReferenceID}) Mesh has too many triangles.");
                StaticSurfaces.Remove(meshCollider);
                continue;
            }

            GetTransformedSurfaces(meshCollider, surfaces, surfaceType, terrainType, force);
            totalMeshColliderTris = newTotalMeshColliderTris;
        }

        SM64Context instance = SM64Context.Instance;
        List<Collider> toRemove = instance?.StaticColliders.Keys.Except(StaticSurfaces).GetTempList();
        if (toRemove != null)
        {
            foreach (Collider col in toRemove)
            {
                instance.UnregisterStaticCollider(col);
            }
        }

        return surfaces.ToArray();

        bool InvalidCollider((MeshCollider collider, SM64SurfaceType, SM64TerrainType, int) col) => col.collider.Mesh.Target == null || !col.collider.Mesh.IsAssetAvailable;
    }

    // Function used for static colliders. Returns correct global positions, rotations and scales.
    public static void GetTransformedSurfaces(Collider collider, List<SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, force, x => collider.Slot.LocalPointToGlobal(x + collider.Offset));
    }

    // Function used for dynamic colliders. Returns correct scales. (rotation and position are set dynamically)
    public static void GetScaledSurfaces(Collider collider, List<SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, int force)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, force, x => collider.Slot.GlobalScale * (x + collider.Offset));
    }
    
    public static void TryParseTagParts(string[] tagParts, out SM64SurfaceType surfaceType, out SM64TerrainType terrainType, out SM64InteractableType interactableType, out int idx)
    {
        surfaceType = SM64SurfaceType.Default;
        terrainType = SM64TerrainType.Grass;
        interactableType = SM64InteractableType.None;
        idx = -1;

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
                        idx = parsedIndex;
                    }
                }
            }
            else if (trimmed.StartsWith("Force_", StringComparison.OrdinalIgnoreCase))
            {
                string idxString = trimmed.Substring("Force_".Length);
                string[] ids = idxString.Split('.');

                if (ids.Length == 2 && int.TryParse(ids[0], out int speedIndex) && int.TryParse(ids[1], out int angleIndex))
                {
                    idx = speedIndex << 8 | angleIndex;
                }
                else if (int.TryParse(idxString, out int forceIndex))
                {
                    idx = forceIndex;
                }
            }
        }
    }

    public static bool CheckDebug()
    {
        bool debug = false;
#if DEBUG
        debug = true;
#endif
        return debug || Config.DebugEnabled.Value;
    }

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