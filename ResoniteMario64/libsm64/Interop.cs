using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Elements.Assets;
using Elements.Core;
using ResoniteModLoader;
using static ResoniteMario64.libsm64.SM64Constants;
#if IsNet9
using Renderite.Shared;
#endif

namespace ResoniteMario64.libsm64;

public static class MarioExtensions
{
    public static float3 ToMarioRotation(this float3 rot)
    {
        return new float3(FixAngle(-rot.x), FixAngle(rot.y), FixAngle(rot.z));

        float Fmod(float a, float b) => a - b * MathX.Floor(a / b);
        float FixAngle(float a) => Fmod(a + 180.0f, 360.0f) - 180.0f;
    }
    
    public static float3 FromMarioRotation(this float3 rot)
    {
        return new float3(FixAngle(-rot.x), FixAngle(rot.y), FixAngle(rot.z));

        float Fmod(float a, float b) => a - b * MathX.Floor(a / b);
        float FixAngle(float a) => Fmod(a + 180.0f, 360.0f) - 180.0f;
    }

    public static float3 ToMarioPosition(this float3 pos) => Interop.ScaleFactor * pos * new float3(-1, 1, 1);
    public static float3 FromMarioPosition(this float3 pos) => pos / Interop.ScaleFactor * new float3(-1, 1, 1);
}

internal static class Interop
{
    public static float ScaleFactor => ResoniteMario64.Config.GetValue(ResoniteMario64.KeyMarioScaleFactor);

    public const int SM64TextureWidth = 64 * 11;
    public const int SM64TextureHeight = 64;
    public const int SM64GeoMaxTriangles = 1024;

    public const float SM64HealthPerHealthPoint = 256;

    public const byte SecondsMultiplier = 40;

    public const int SM64LevelResetValue = -10000;

    // It seems a collider can't be too big, otherwise it will be ignored
    // This seems like too much of a pain to fix rn, let the future me worry about it
    public static int SM64MaxVertexDistance = 250000 * (int)ScaleFactor;

    public const float SM64Deg2Angle = 182.04459f;

    public static Bitmap2D MarioTexture { get; private set; }
    public static bool IsGlobalInit { get; private set; }
    

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SM64DebugPrintFunctionPtr(string message);

    private delegate void SM64PlaySoundFunctionPtr(uint soundBits, float[] pos);

    // Initialization & Setup
    [DllImport("sm64")]
    private static extern void sm64_register_debug_print_function(IntPtr debugPrintFunctionPtr);

    [DllImport("sm64")]
    private static extern void sm64_register_play_sound_function(IntPtr playSoundFunction);

    [DllImport("sm64")]
    private static extern void sm64_global_init(IntPtr rom, IntPtr outTexture);

    [DllImport("sm64")]
    private static extern void sm64_global_terminate();

    [DllImport("sm64")]
    private static extern void sm64_audio_init(IntPtr rom);

    // Audio & Music
    [DllImport("sm64")]
    private static extern uint sm64_audio_tick(uint numQueuedSamples, uint numDesiredSamples, IntPtr audioBuffer);

    [DllImport("sm64")]
    private static extern void sm64_seq_player_play_sequence(byte player, byte seqId, ushort arg2);

    [DllImport("sm64")]
    private static extern void sm64_play_music(byte player, ushort seqArgs, ushort fadeTimer);

    [DllImport("sm64")]
    private static extern void sm64_stop_background_music(ushort seqId);

    [DllImport("sm64")]
    private static extern void sm64_fadeout_background_music(ushort arg0, ushort fadeOut);

    [DllImport("sm64")]
    private static extern ushort sm64_get_current_background_music();

    [DllImport("sm64")]
    private static extern void sm64_play_sound(int soundBits, IntPtr pos);

    [DllImport("sm64")]
    private static extern void sm64_play_sound_global(int soundBits);

    [DllImport("sm64")]
    private static extern void sm64_set_sound_volume(float vol);

    // Mario Lifecycle
    [DllImport("sm64")]
    private static extern uint sm64_mario_create(float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_mario_tick(uint marioId, ref SM64MarioInputs inputs, ref SM64MarioState outState, ref SM64MarioGeometryBuffers outBuffers);

    [DllImport("sm64")]
    private static extern void sm64_mario_delete(uint marioId);

    // Mario Actions & Status
    [DllImport("sm64")]
    private static extern void sm64_set_mario_action(uint marioId, uint action);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_action(uint marioId, uint action, uint actionArg);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_state(uint marioId, uint flags);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_health(uint marioId, ushort health);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_invincibility(uint marioId, short timer);

    [DllImport("sm64")]
    private static extern void sm64_mario_take_damage(uint marioId, uint damage, uint subtype, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_mario_heal(uint marioId, byte healCounter);

    [DllImport("sm64")]
    private static extern void sm64_mario_kill(uint marioId);

    [DllImport("sm64")]
    private static extern void sm64_mario_interact_cap(uint marioId, uint capFlag, ushort capTime, byte playMusic);

    [DllImport("sm64")]
    private static extern void sm64_mario_extend_cap(uint marioId, ushort capTime);
    
    [DllImport("sm64")]
    private static extern void sm64_set_mario_animation(uint marioId, ushort animId);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_anim_frame(uint marioId, short frame);

    [DllImport("sm64")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool sm64_mario_attack(uint marioId, float x, float y, float z, float hitboxHeight);

    // Mario Transform 
    [DllImport("sm64")]
    private static extern void sm64_set_mario_position(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_angle(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_faceangle(uint marioId, float y);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_velocity(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_forward_velocity(uint marioId, float vel);

    /* Mario Environmental Effects */
    [DllImport("sm64")]
    private static extern void sm64_set_mario_water_level(uint marioId, int level);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_gas_level(uint marioId, int level);

    // Static & Dynamic Surfaces
    [DllImport("sm64")]
    private static extern void sm64_static_surfaces_load(SM64Surface[] surfaces, ulong numSurfaces);

    [DllImport("sm64")]
    private static extern uint sm64_surface_object_create(ref SM64SurfaceObject surfaceObject);

    [DllImport("sm64")]
    private static extern void sm64_surface_object_move(uint objectId, ref SM64ObjectTransform transform);

    [DllImport("sm64")]
    private static extern void sm64_surface_object_delete(uint objectId);

    // Collision & Geometry Queries
    [DllImport("sm64")]
    private static extern int sm64_surface_find_wall_collision(ref float x, ref float y, ref float z, float offsetY, float radius);

    [DllImport("sm64")]
    private static extern int sm64_surface_find_wall_collisions(ref IntPtr colData);

    [DllImport("sm64")]
    private static extern float sm64_surface_find_ceil(float x, float y, float z, out IntPtr ceil);

    [DllImport("sm64")]
    private static extern float sm64_surface_find_floor(float x, float y, float z, out IntPtr floor);

    [DllImport("sm64")]
    private static extern float sm64_surface_find_floor_height(float x, float y, float z);

    [DllImport("sm64")]
    private static extern float sm64_surface_find_floor_height_and_data(float x, float y, float z, out IntPtr floorGeo);

    [DllImport("sm64")]
    private static extern float sm64_surface_find_water_level(float x, float z);

    [DllImport("sm64")]
    private static extern float sm64_surface_find_poison_gas_level(float x, float z);

    public static void GlobalInit(byte[] rom)
    {
        GCHandle romHandle = GCHandle.Alloc(rom, GCHandleType.Pinned);
        byte[] textureData = new byte[4 * SM64TextureWidth * SM64TextureHeight];
        GCHandle textureDataHandle = GCHandle.Alloc(textureData, GCHandleType.Pinned);

        sm64_global_init(romHandle.AddrOfPinnedObject(), textureDataHandle.AddrOfPinnedObject());
        sm64_audio_init(romHandle.AddrOfPinnedObject());

        // With audio this has became waaaay too spammy ;_;
        // #if DEBUG
        // var callbackDelegate = new DebugPrintFuncDelegate(DebugPrintCallback);
        // sm64_register_debug_print_function(Marshal.GetFunctionPointerForDelegate(callbackDelegate));
        // #endif
        
        MarioTexture = new Bitmap2D(SM64TextureWidth, SM64TextureHeight, TextureFormat.RGBA32, false, ColorProfile.sRGB, false);
        for (int ix = 0; ix < SM64TextureWidth; ix++)
        for (int iy = 0; iy < SM64TextureHeight; iy++)
        {
            color32 color = new color32(
                textureData[4 * (ix + SM64TextureWidth * iy) + 0],
                textureData[4 * (ix + SM64TextureWidth * iy) + 1],
                textureData[4 * (ix + SM64TextureWidth * iy) + 2],
                textureData[4 * (ix + SM64TextureWidth * iy) + 3]
            );
            // Make the 100% transparent colors white. So we can multiply with the vertex colors.
            if (color.a == 0)
            {
                color = new color32(255, 255, 255, 0);
            }

            MarioTexture.SetPixel32(ix, iy, color);
        }

        // marioTexture.Save("mario.png");

        romHandle.Free();
        textureDataHandle.Free();

        IsGlobalInit = true;
    }

    public static void GlobalTerminate()
    {
        StopMusic();
        sm64_global_terminate();
        MarioTexture = null;
        IsGlobalInit = false;
    }

    public static void PlayRandomMusic()
    {
        StopMusic();
        sm64_play_music(0, Musics[RandomX.Range(0, Musics.Length)], 0);
    }

    public static void StopMusic()
    {
        // Stop all music that was queued
        while (sm64_get_current_background_music() is var currentMusic && currentMusic != (ushort)MusicSequence.None)
        {
            sm64_stop_background_music(currentMusic);
        }
    }

    public static void StaticSurfacesLoad(SM64Surface[] surfaces)
    {
        ResoniteMod.Debug($"Reloading all static collider surfaces - Total Polygons: {surfaces.Length}");
        sm64_static_surfaces_load(surfaces, (ulong)surfaces.Length);
    }

    public static uint MarioCreate(float3 marioPos) => sm64_mario_create(marioPos.x, marioPos.y, marioPos.z);

    public static SM64MarioState MarioTick(uint marioId, SM64MarioInputs inputs, float3[] positionBuffer, float3[] normalBuffer, float3[] colorBuffer, float2[] uvBuffer, out ushort numTrianglesUsed)
    {
        SM64MarioState outState = new SM64MarioState();

        GCHandle posHandle = GCHandle.Alloc(positionBuffer, GCHandleType.Pinned);
        GCHandle normHandle = GCHandle.Alloc(normalBuffer, GCHandleType.Pinned);
        GCHandle colorHandle = GCHandle.Alloc(colorBuffer, GCHandleType.Pinned);
        GCHandle uvHandle = GCHandle.Alloc(uvBuffer, GCHandleType.Pinned);

        SM64MarioGeometryBuffers buff = new SM64MarioGeometryBuffers
        {
            position = posHandle.AddrOfPinnedObject(),
            normal = normHandle.AddrOfPinnedObject(),
            color = colorHandle.AddrOfPinnedObject(),
            uv = uvHandle.AddrOfPinnedObject()
        };


        sm64_mario_tick(marioId, ref inputs, ref outState, ref buff);


        numTrianglesUsed = buff.numTrianglesUsed;

        posHandle.Free();
        normHandle.Free();
        colorHandle.Free();
        uvHandle.Free();

        return outState;
    }

    public static uint AudioTick(short[] audioBuffer, uint numDesiredSamples, uint numQueuedSamples = 0)
    {
        GCHandle audioBufferPointer = GCHandle.Alloc(audioBuffer, GCHandleType.Pinned);
        uint numSamples = sm64_audio_tick(numQueuedSamples, numDesiredSamples, audioBufferPointer.AddrOfPinnedObject());
        audioBufferPointer.Free();
        return numSamples;
    }

    public static void PlaySoundGlobal(Sounds soundKey)
    {
        sm64_play_sound_global((int)SoundBank[soundKey]);
    }

    public static void PlaySound(Sounds soundKey, float3 unityPosition)
    {
        float3 marioPos = unityPosition.ToMarioPosition();
        float[] position = { marioPos.x, marioPos.y, marioPos.z };
        GCHandle posPointer = GCHandle.Alloc(position, GCHandleType.Pinned);

        sm64_play_sound((int)SoundBank[soundKey], posPointer.AddrOfPinnedObject());

        posPointer.Free();
    }

    public static void MarioDelete(uint marioId)
    {
        sm64_mario_delete(marioId);
    }

    public static void MarioTakeDamage(uint marioId, float3 unityPosition, uint damage)
    {
        float3 marioPos = unityPosition.ToMarioPosition();
        sm64_mario_take_damage(marioId, damage, 0, marioPos.x, marioPos.y, marioPos.z);
    }

    public static void MarioSetVelocity(uint marioId, SM64MarioState previousState, SM64MarioState currentState)
    {
        sm64_set_mario_velocity(marioId,
                                currentState.Position[0] - previousState.Position[0],
                                currentState.Position[1] - previousState.Position[1],
                                currentState.Position[2] - previousState.Position[2]);
    }

    public static void MarioSetVelocity(uint marioId, float3 unityVelocity)
    {
        float3 marioVelocity = unityVelocity.ToMarioPosition();
        sm64_set_mario_velocity(marioId, marioVelocity.x, marioVelocity.y, marioVelocity.z);
    }

    public static void MarioSetForwardVelocity(uint marioId, float unityVelocity)
    {
        sm64_set_mario_forward_velocity(marioId, unityVelocity * ScaleFactor);
    }

    public static void CreateAndAppendSurfaces(List<SM64Surface> outSurfaces, int[] triangles, float3[] vertices, SM64SurfaceType surfaceType, SM64TerrainType terrainType)
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            outSurfaces.Add(new SM64Surface
            {
                Force = 0,
                Type = (short)surfaceType,
                Terrain = (ushort)terrainType,

                v0x = (int)(ScaleFactor * -vertices[triangles[i]].x),
                v0y = (int)(ScaleFactor * vertices[triangles[i]].y),
                v0z = (int)(ScaleFactor * vertices[triangles[i]].z),

                v1x = (int)(ScaleFactor * -vertices[triangles[i + 2]].x),
                v1y = (int)(ScaleFactor * vertices[triangles[i + 2]].y),
                v1z = (int)(ScaleFactor * vertices[triangles[i + 2]].z),

                v2x = (int)(ScaleFactor * -vertices[triangles[i + 1]].x),
                v2y = (int)(ScaleFactor * vertices[triangles[i + 1]].y),
                v2z = (int)(ScaleFactor * vertices[triangles[i + 1]].z)
            });
        }
    }

    public static float FindFloor(float3 pos, out SM64SurfaceCollisionData data)
    {
        float3 marioPos = pos.ToMarioPosition();
        IntPtr floorPtr;
        float floorHeightMario = sm64_surface_find_floor(marioPos.x, marioPos.y, marioPos.z, out floorPtr);
        if (floorPtr == IntPtr.Zero)
        {
            data = new SM64SurfaceCollisionData();
        }
        else
        {
            data = Marshal.PtrToStructure<SM64SurfaceCollisionData>(floorPtr);
        }
        return floorHeightMario / Interop.ScaleFactor;
    }
    
    public static float FindFloorHeight(float3 pos)
    {
        float3 marioPos = pos.ToMarioPosition();
        float floorHeightMario = sm64_surface_find_floor_height(marioPos.x, marioPos.y, marioPos.z);
        return floorHeightMario / Interop.ScaleFactor;
    }

    public static float FindFloorHeightAndData(float3 pos, out SM64FloorCollisionData data)
    {
        float3 marioPos = pos.ToMarioPosition();
        IntPtr floorGeo;
        float floorHeightMario = sm64_surface_find_floor_height_and_data(marioPos.x, marioPos.y, marioPos.z, out floorGeo);
        if (floorGeo == IntPtr.Zero)
        {
            data = new SM64FloorCollisionData();
        }
        else
        {
            data = Marshal.PtrToStructure<SM64FloorCollisionData>(floorGeo);
        }
        return floorHeightMario / Interop.ScaleFactor;
    }

    public static uint SurfaceObjectCreate(float3 position, floatQ rotation, SM64Surface[] surfaces)
    {
        GCHandle surfListHandle = GCHandle.Alloc(surfaces, GCHandleType.Pinned);
        SM64ObjectTransform t = SM64ObjectTransform.FromUnityWorld(position, rotation);

        SM64SurfaceObject surfObj = new SM64SurfaceObject
        {
            transform = t,
            surfaceCount = (uint)surfaces.Length,
            surfaces = surfListHandle.AddrOfPinnedObject()
        };

        uint result = sm64_surface_object_create(ref surfObj);

        surfListHandle.Free();

        return result;
    }

    public static void SurfaceObjectMove(uint id, float3 position, floatQ rotation)
    {
        SM64ObjectTransform t = SM64ObjectTransform.FromUnityWorld(position, rotation);
        sm64_surface_object_move(id, ref t);
    }

    public static void SurfaceObjectDelete(uint id)
    {
        sm64_surface_object_delete(id);
    }

    public static void MarioCap(uint marioId, StateFlag stateFlag, float durationSeconds, bool playCapMusic)
    {
        sm64_mario_interact_cap(marioId, (uint)stateFlag, (ushort)(durationSeconds * SecondsMultiplier), playCapMusic ? (byte)1 : (byte)0);
    }

    public static void MarioCapExtend(uint marioId, float durationSeconds)
    {
        sm64_mario_extend_cap(marioId, (ushort)(durationSeconds * SecondsMultiplier));
    }

    public static void MarioSetPosition(uint marioId, float3 pos)
    {
        float3 marioPos = pos.ToMarioPosition();
        sm64_set_mario_position(marioId, marioPos.x, marioPos.y, marioPos.z);
    }

    public static void MarioSetFaceAngle(uint marioId, floatQ rot)
    {
        float angleInDegrees = rot.EulerAngles.y;
        if (angleInDegrees > 180f)
        {
            angleInDegrees -= 360f;
        }

        sm64_set_mario_faceangle(marioId, -MathX.Deg2Rad * angleInDegrees);
    }

    public static void MarioSetRotation(uint marioId, floatQ rotation)
    {
        float3 marioRotation = rotation.EulerAngles.ToMarioRotation();
        sm64_set_mario_angle(marioId, marioRotation.x, marioRotation.y, marioRotation.z);
    }

    public static void MarioSetHealthPoints(uint marioId, float healthPoints)
    {
        sm64_set_mario_health(marioId, (ushort)(healthPoints * SM64HealthPerHealthPoint));
    }

    public static void MarioHeal(uint marioId, byte healthPoints)
    {
        // It was healing 0.25 with 1, so we multiplied by 4 EZ FIX
        sm64_mario_heal(marioId, (byte)(healthPoints * 4));
    }

    public static void MarioSetAction(uint marioId, ActionFlag actionFlag)
    {
        sm64_set_mario_action(marioId, (uint)actionFlag);
    }

    public static void MarioSetAction(uint marioId, uint actionFlags)
    {
        sm64_set_mario_action(marioId, actionFlags);
    }

    public static void MarioSetState(uint marioId, uint flags)
    {
        sm64_set_mario_state(marioId, flags);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SM64Surface
{
    public short Type;
    public short Force;
    public ushort Terrain;
    public int v0x, v0y, v0z;
    public int v1x, v1y, v1z;
    public int v2x, v2y, v2z;
}

[StructLayout(LayoutKind.Sequential)]
public struct SM64MarioInputs
{
    public float camLookX, camLookZ;
    public float stickX, stickY;
    public byte buttonA, buttonB, buttonZ;
}

[StructLayout(LayoutKind.Sequential)]
public struct SM64MarioState
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] Position;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] Velocity;

    public float FacingAngle;

    public short Health;

    public uint ActionFlags;
    public uint StateFlags;
    public uint ParticleFlags;
    public short InvincibleTimer;

    public float3 ScaledPosition => Position != null ? new float3(-Position[0], Position[1], Position[2]) / Interop.ScaleFactor : float3.Zero;
    public floatQ ScaledRotation => floatQ.Euler(0f, MathX.Repeat(-MathX.Rad2Deg * FacingAngle + 180f, 360f) - 180f, 0f);

    public float HealthPoints => Health / Interop.SM64HealthPerHealthPoint;

    public bool IsDead => Health < 1 * Interop.SM64HealthPerHealthPoint || (ActionFlags & (uint)ActionFlag.QuicksandDeath) == (uint)ActionFlag.QuicksandDeath;
    public bool IsAttacking => (ActionFlags & (uint)ActionFlag.Attacking) != 0;
    public bool IsFirstPerson => IsFlyingOrSwimming;
    public bool IsFlyingOrSwimming => (ActionFlags & (uint)ActionFlag.SwimmingOrFlying) != 0;
    public bool IsSwimming => (ActionFlags & (uint)ActionFlag.Swimming) != 0;
    public bool IsFlying => (ActionFlags & (uint)ActionFlag.Flying) != 0;
    public bool IsTeleporting => (ActionFlags & (uint)StateFlag.Teleporting) != 0;

    public bool IsWearingCap(MarioCapType capType)
    {
        return capType switch
        {
            MarioCapType.VanishCap => (StateFlags & (uint)StateFlag.VanishCap) != 0,
            MarioCapType.MetalCap  => (StateFlags & (uint)StateFlag.MetalCap) != 0,
            MarioCapType.WingCap   => (StateFlags & (uint)StateFlag.WingCap) != 0,
            _                      => capType == MarioCapType.NormalCap
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct SM64MarioGeometryBuffers
{
    public IntPtr position;
    public IntPtr normal;
    public IntPtr color;
    public IntPtr uv;
    public ushort numTrianglesUsed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SM64ObjectTransform
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    private float[] Position;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    private float[] EulerRotation;

    public static SM64ObjectTransform FromUnityWorld(float3 position, floatQ rotation)
    {
        return new SM64ObjectTransform
        {
            Position = VecToArr(position.ToMarioPosition()),
            EulerRotation = VecToArr(rotation.EulerAngles.ToMarioRotation())
        };

        float[] VecToArr(float3 v) => new[] { v.x, v.y, v.z };
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SM64SurfaceObjectTransform
{
    public float aPosX;
    public float aPosY;
    public float aPosZ;

    public float aVelX;
    public float aVelY;
    public float aVelZ;

    public short aFaceAnglePitch;
    public short aFaceAngleYaw;
    public short aFaceAngleRoll;

    public short aAngleVelPitch;
    public short aAngleVelYaw;
    public short aAngleVelRoll;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SM64SurfaceObject
{
    public SM64ObjectTransform transform;
    public uint surfaceCount;
    public IntPtr surfaces;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SM64SurfaceCollisionData
{
    public short type;
    public short force;
    public byte flags;
    public byte room;
    public int lowerY;
    public int upperY;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] vertex1;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] vertex2;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] vertex3;

    public float3 normal;

    public float originOffset;

    public byte isValid;

    public IntPtr transform;

    public ushort terrain;
}

[StructLayout(LayoutKind.Sequential)]
public struct SM64FloorCollisionData
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] unused;

    public float normalX;
    public float normalY;
    public float normalZ;
    public float originOffset;
}