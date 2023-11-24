using BepuPhysics.Collidables;
using Elements.Assets;
using Elements.Core;
using FrooxEngine;
using ResoniteMario64;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ResoniteMario64;

internal static class Utils
{

    public static void TransformAndGetSurfaces(List<Interop.SM64Surface> outSurfaces, MeshX mesh, SM64SurfaceType surfaceType, SM64TerrainType terrainType, Func<float3, float3> transformFunc)
    {
        for (var subMeshIndex = 0; subMeshIndex < mesh.SubmeshCount; subMeshIndex++)
        {
            var submesh = mesh.GetSubmesh(subMeshIndex);
            var vertices = mesh.Vertices.Select((v) => transformFunc(v.Position)).ToArray();
            Interop.CreateAndAppendSurfaces(outSurfaces, submesh.RawIndicies, vertices, surfaceType, terrainType);
        }
    }

    public static bool IsGoodCollider(Collider col)
    {
        return
            // Ignore disabled
            col.Enabled
            && col.Slot.IsActive
            // Ignore non character colliders
            && col.CharacterCollider.Value
            // Ignore other mario's colliders
            && col.Slot.GetComponentInChildren<SM64Mario>() == null
            && col.Slot.GetComponentInParents<SM64Mario>() == null
            // Ignore colliders in pickup scripts // TODO: mabe ignore grabbables (equivalent of cvrpickupobjects??)

            // Ignore triggers
            && col.Type.Value != ColliderType.Trigger;
    }

    internal static Interop.SM64Surface[] GetAllStaticSurfaces(World wld)
    {

        var surfaces = new List<Interop.SM64Surface>();
        var meshColliders = new List<MeshCollider>();

        // Should attempt to load world colliders for this world?
        // TODO: Per world setting thing :)
        /*var shouldAutoLoad = Config.ShouldAttemptToLoadWorldColliders();
        if (!shouldAutoLoad) {
            ResoniteMario64.Msg($"[GetAllStaticSurfaces] As per configuration we're skipping auto generating this world's colliders.");
        }*/
        var shouldAutoLoad = ResoniteMario64.config.GetValue(ResoniteMario64.KEY_ATTEMPT_LOADING_WORLD_COLLIDERS);

        foreach (var obj in wld.RootSlot.GetComponentsInChildren<Collider>())
        {

            var cvrSM64ColliderStatic = obj.Slot.GetComponent<SM64ColliderStatic>();
            var hasDedicatedComponent = cvrSM64ColliderStatic != null;

            // Ignore bad colliders if we don't have a dedicated component, or if auto load world colliders is off
            if (!hasDedicatedComponent && (!shouldAutoLoad || !IsGoodCollider(obj))) continue;

            // Ignore the dynamic colliders, those will be handled separately
            var dynamicCollider = obj.Slot.GetComponent<SM64ColliderDynamic>();
            if (dynamicCollider != null) continue;

            // Check if we have surface and terrain data
            var surfaceType = SM64SurfaceType.Default;
            var terrainType = SM64TerrainType.Grass;

            // Check if we have surface and terrain data
            if (hasDedicatedComponent)
            {
                surfaceType = cvrSM64ColliderStatic.SurfaceType;
                terrainType = cvrSM64ColliderStatic.TerrainType;
            }

#if DEBUG
            //ResoniteMario64.Msg($"[GoodCollider] {obj.name}");
#endif

            if (obj is MeshCollider meshCollider && !hasDedicatedComponent)
            {
                // Let's do some more processing to the mesh colliders without dedicated components
                meshColliders.Add(meshCollider);
            }
            else
            {
                // Everything else, let's just add (probably a bad idea)
                GetTransformedSurfaces(obj, surfaces, surfaceType, terrainType, hasDedicatedComponent);
            }
        }

        // Ignore all meshes colliders with a null shared mesh, or non-readable
        var nonReadableMeshColliders = meshColliders.Where(meshCollider => meshCollider.Mesh.Target == null || !meshCollider.Mesh.IsAssetAvailable).ToList();
#if DEBUG
        foreach (var invalidMeshCollider in nonReadableMeshColliders)
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
        var totalMeshColliderTris = 0;
        foreach (var meshCollider in meshColliders)
        {
            var meshTrisCount = meshCollider.Mesh.Asset.Data.TotalTriangleCount;
            var newTotalMeshColliderTris = totalMeshColliderTris + meshTrisCount;
            if (newTotalMeshColliderTris > ResoniteMario64.config.GetValue(ResoniteMario64.KEY_MAX_MESH_COLLIDER_TRIS))
            {
#if DEBUG
                ResoniteMario64.Warn($"[MeshCollider] {meshCollider.Slot.Name} will be ignored because it exceeds the " +
                                    $"maximum mesh collider total tris count. Tris: {meshTrisCount}, Total Tris: " +
                                    $"{newTotalMeshColliderTris}/{ResoniteMario64.config.GetValue(ResoniteMario64.KEY_MAX_MESH_COLLIDER_TRIS)}");
#endif
            }
            else
            {
                GetTransformedSurfaces(meshCollider, surfaces, SM64SurfaceType.Default, SM64TerrainType.Grass, false);
#if DEBUG
                ResoniteMario64.Msg($"[MeshCollider] {meshCollider.Slot.Name} will be added. Tris: {meshTrisCount}, Total " +
                                $"Tris: {newTotalMeshColliderTris}/{ResoniteMario64.config.GetValue(ResoniteMario64.KEY_MAX_MESH_COLLIDER_TRIS)}");
#endif
            }
            totalMeshColliderTris = newTotalMeshColliderTris;
        }

        return surfaces.ToArray();
    }



    // Function used for static colliders. Returns correct global positions, rotations and scales.
    public static List<Interop.SM64Surface> GetTransformedSurfaces(Collider collider, List<Interop.SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, bool bypassPolygonLimit)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, x => collider.Slot.LocalPointToGlobal(x + collider.Offset));
        /*
        var debg = collider.World.RootSlot.AddSlot(collider.Slot.Name + " -debug surfaces");
        debg.GlobalScale = float3.One / 1000f;
        var grassMat = debg.AttachComponent<UnlitMaterial>();
        grassMat.TintColor.Value = colorX.Azure.SetA(0.5f);
        grassMat.BlendMode.Value = BlendMode.Alpha;

        foreach (var surf in surfaces)
        {
            var s = debg.AddSlot("surf");
            var tri = s.AttachComponent<TriangleMesh>();
            var rend = s.AttachComponent<MeshRenderer>();
            rend.Mesh.Target = tri;
            rend.Materials.Add(grassMat);
            tri.Vertex0.Position.Value = new float3(surf.v0x, surf.v0y, surf.v0z);
            tri.Vertex1.Position.Value = new float3(surf.v1x, surf.v1y, surf.v1z);
            tri.Vertex2.Position.Value = new float3(surf.v2x, surf.v2y, surf.v2z);
        }
        */
        return surfaces;
    }


    // Function used for dynamic colliders. Returns correct scales. (rotation and position is set dynamically)
    internal static List<Interop.SM64Surface> GetScaledSurfaces(Collider collider, List<Interop.SM64Surface> surfaces, SM64SurfaceType surfaceType, SM64TerrainType terrainType, bool bypassPolygonLimit)
    {
        TransformAndGetSurfaces(surfaces, collider.GetColliderMesh(), surfaceType, terrainType, x => collider.Slot.GlobalScale * (x + collider.Offset));
        
        return surfaces;
    }

    public enum MarioCapType
    {
        None,
        VanishCap,
        MetalCap,
        WingCap,
    }

    public static bool HasCapType(uint flags, MarioCapType capType)
    {
        switch (capType)
        {
            case MarioCapType.VanishCap: return (flags & (uint)FlagsFlags.MARIO_VANISH_CAP) != 0;
            case MarioCapType.MetalCap: return (flags & (uint)FlagsFlags.MARIO_METAL_CAP) != 0;
            case MarioCapType.WingCap: return (flags & (uint)FlagsFlags.MARIO_WING_CAP) != 0;
        }
        return capType == MarioCapType.None;
    }

    public static bool IsTeleporting(uint flags)
    {
        return (flags & (uint)FlagsFlags.MARIO_TELEPORTING) != 0;
    }

    public static readonly Dictionary<SoundBitsKeys, uint> SoundBits = new() {
        { SoundBitsKeys.SOUND_GENERAL_COIN,              SoundArgLoad(3, 8, 0x11, 0x80, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_WATER,        SoundArgLoad(3, 8, 0x12, 0x80, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT,        SoundArgLoad(3, 0, 0x30, 0x00, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT_2,      SoundArgLoad(3, 8, 0x30, 0x00, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_SPURT_EU,     SoundArgLoad(3, 8, 0x30, 0x20, 8) },
        { SoundBitsKeys.SOUND_GENERAL_COIN_DROP,         SoundArgLoad(3, 0, 0x36, 0x40, 8) },
        { SoundBitsKeys.SOUND_GENERAL_RED_COIN,          SoundArgLoad(3, 0, 0x68, 0x90, 8) },
        { SoundBitsKeys.SOUND_MENU_COIN_ITS_A_ME_MARIO,  SoundArgLoad(7, 0, 0x14, 0x00, 8) },
        { SoundBitsKeys.SOUND_MENU_COLLECT_RED_COIN,     SoundArgLoad(7, 8, 0x28, 0x90, 8) },
    };

    public static uint SoundArgLoad(uint bank, uint playFlags, uint soundID, uint priority, uint flags2)
    {
        // Sound Magic Definition:
        // First Byte (Upper Nibble): Sound Bank (not the same as audio bank!)
        // First Byte (Lower Nibble): Bitflags for audio playback?
        // Second Byte: Sound ID
        // Third Byte: Priority
        // Fourth Byte (Upper Nibble): More bitflags
        // Fourth Byte (Lower Nibble): Sound Status (this is set to SOUND_STATUS_PLAYING when passed to the audio driver.)
        return (bank << 28) | (playFlags << 24) | (soundID << 16) | (priority << 8) | (flags2 << 4) | 1;
    }
}
