using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using ResoniteMario64.libsm64;

namespace ResoniteMario64;

public static class Utils
{
    public static readonly Dictionary<SoundBitsKeys, uint> SoundBits = new Dictionary<SoundBitsKeys, uint>
    {
        { SoundBitsKeys.SOUND_GENERAL_COIN, SoundArgLoad(3, 8, 0x11, 0x80, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_WATER, SoundArgLoad(3, 8, 0x12, 0x80, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT, SoundArgLoad(3, 0, 0x30, 0x00, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT_2, SoundArgLoad(3, 8, 0x30, 0x00, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT_EU, SoundArgLoad(3, 8, 0x30, 0x20, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_DROP, SoundArgLoad(3, 0, 0x36, 0x40, 8) },
        { SoundBitsKeys.SOUND_GENERAL_RED_COIN, SoundArgLoad(3, 0, 0x68, 0x90, 8) },
        { SoundBitsKeys.SOUND_MENU_COIN_ITS_A_ME_MARIO, SoundArgLoad(7, 0, 0x14, 0x00, 8) },
        { SoundBitsKeys.SOUND_MENU_COLLECT_RED_COIN, SoundArgLoad(7, 8, 0x28, 0x90, 8) }
    };

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
            // Ignore non character colliders
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

#if DEBUG
            // ResoniteMario64.Msg($"[GoodCollider] {obj.name}");
#endif

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
            ResoniteMario64.Warn($"[MeshCollider] {invalidMeshCollider.Slot.Name} Mesh is " +
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
                ResoniteMario64.Debug("[MeshCollider] Collider has too many triangles. " + meshCollider);
                continue;
            }

            ResoniteMario64.Debug($"[MeshCollider] Adding mesh collider. (Remaining tris: {maxTris - newTotalMeshColliderTris}) " + meshCollider);

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
    
    public static uint SoundArgLoad(uint bank, uint playFlags, uint soundId, uint priority, uint flags2) =>
            // Sound Magic Definition:
            // First Byte (Upper Nibble): Sound Bank (not the same as audio bank!)
            // First Byte (Lower Nibble): Bitflags for audio playback?
            // Second Byte: Sound ID
            // Third Byte: Priority
            // Fourth Byte (Upper Nibble): More bitflags
            // Fourth Byte (Lower Nibble): Sound Status (this is set to SOUND_STATUS_PLAYING when passed to the audio driver.)
            bank << 28 | playFlags << 24 | soundId << 16 | priority << 8 | flags2 << 4 | 1;
}

public enum MarioCapType
{
    None,
    VanishCap,
    MetalCap,
    WingCap
}