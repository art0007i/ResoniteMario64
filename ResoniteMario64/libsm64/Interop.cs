using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Elements.Assets;
using Elements.Core;
using ResoniteModLoader;

namespace ResoniteMario64.libsm64;

public static class MarioExtensions
{
    public static float3 ToMarioRotation(this float3 rot)
    {
        float Fmod(float a, float b)
        {
            return a - b * MathX.Floor(a / b);
        }

        float FixAngle(float a)
        {
            return Fmod(a + 180.0f, 360.0f) - 180.0f;
        }

        return new float3(FixAngle(-rot.x), FixAngle(rot.y), FixAngle(rot.z));
    }

    public static float3 ToMarioPosition(this float3 pos) => Interop.ScaleFactor * pos * new float3(-1, 1, 1);
}

internal static class Interop
{
    public const float ScaleFactor = 150.0f;

    public const int SM64TextureWidth = 64 * 11;
    public const int SM64TextureHeight = 64;
    public const int SM64GeoMaxTriangles = 1024;

    public const float SM64HealthPerHealthPoint = 256;

    public const byte SecondsMultiplier = 40;

    public const int SM64LevelResetValue = -10000;

    // It seems a collider can't be too big, otherwise it will be ignored
    // This seems like too much of a pain to fix rn, let the future me worry about it
    public const int SM64MaxVertexDistance = 250000 * (int)ScaleFactor;

    public const float SM64Deg2Angle = 182.04459f;

    private static readonly ushort[] Musics =
    {
        (ushort)MusicId.SEQ_MENU_TITLE_SCREEN,
        (ushort)(MusicId.SEQ_MENU_TITLE_SCREEN | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_GRASS,
        (ushort)(MusicId.SEQ_LEVEL_GRASS | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_INSIDE_CASTLE,
        (ushort)(MusicId.SEQ_LEVEL_INSIDE_CASTLE | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_WATER,
        (ushort)(MusicId.SEQ_LEVEL_WATER | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_HOT,
        (ushort)(MusicId.SEQ_LEVEL_HOT | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_BOSS_KOOPA,
        (ushort)(MusicId.SEQ_LEVEL_BOSS_KOOPA | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_SNOW,
        (ushort)(MusicId.SEQ_LEVEL_SNOW | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_SLIDE,
        (ushort)(MusicId.SEQ_LEVEL_SLIDE | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_SPOOKY,
        (ushort)(MusicId.SEQ_LEVEL_SPOOKY | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_EVENT_POWERUP,
        (ushort)(MusicId.SEQ_EVENT_POWERUP | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_EVENT_METAL_CAP,
        (ushort)(MusicId.SEQ_EVENT_METAL_CAP | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_LEVEL_KOOPA_ROAD,
        (ushort)(MusicId.SEQ_LEVEL_KOOPA_ROAD | MusicId.SEQ_VARIATION),

        (ushort)MusicId.SEQ_LEVEL_BOSS_KOOPA_FINAL,
        (ushort)(MusicId.SEQ_LEVEL_BOSS_KOOPA_FINAL | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_MENU_FILE_SELECT,
        (ushort)(MusicId.SEQ_MENU_FILE_SELECT | MusicId.SEQ_VARIATION),
        (ushort)MusicId.SEQ_EVENT_CUTSCENE_CREDITS,
        (ushort)(MusicId.SEQ_EVENT_CUTSCENE_CREDITS | MusicId.SEQ_VARIATION)
    };

    public static Bitmap2D MarioTexture { get; private set; }

    public static bool IsGlobalInit { get; private set; }

    [DllImport("sm64")]
    private static extern void sm64_register_debug_print_function(IntPtr debugPrintFunctionPtr);

    [DllImport("sm64")]
    private static extern void sm64_global_init(IntPtr rom, IntPtr outTexture);

    [DllImport("sm64")]
    private static extern void sm64_global_terminate();

    [DllImport("sm64")]
    private static extern void sm64_set_mario_position(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_angle(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_faceangle(uint marioId, float y);

    [DllImport("sm64")]
    private static extern void sm64_audio_init(IntPtr rom);

    [DllImport("sm64")]
    private static extern uint sm64_audio_tick(uint numQueuedSamples, uint numDesiredSamples, IntPtr audioBuffer);

    [DllImport("sm64")]
    private static extern void sm64_play_music(byte player, ushort seqArgs, ushort fadeTimer);

    [DllImport("sm64")]
    private static extern ushort sm64_get_current_background_music();

    [DllImport("sm64")]
    private static extern void sm64_stop_background_music(ushort seqId);

    [DllImport("sm64")]
    private static extern void sm64_play_sound_global(int soundBits);

    [DllImport("sm64")]
    private static extern void sm64_play_sound(int soundBits, IntPtr pos);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_water_level(uint marioId, int level);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_gas_level(uint marioId, int level);

    [DllImport("sm64")]
    private static extern void sm64_mario_interact_cap(uint marioId, uint capFlag, ushort capTime, byte playMusic);

    [DllImport("sm64")]
    private static extern void sm64_mario_extend_cap(uint marioId, ushort capTime);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_state(uint marioId, uint flags);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_action(uint marioId, uint action);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_action(uint marioId, uint action, uint actionArg);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_invincibility(uint marioId, short timer);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_velocity(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_forward_velocity(uint marioId, float vel);

    [DllImport("sm64")]
    private static extern void sm64_mario_take_damage(uint marioId, uint damage, uint subtype, float x, float y, float z);

    [DllImport("sm64")]
    private static extern void sm64_mario_attack(uint marioId, float x, float y, float z, float hitboxHeight);

    [DllImport("sm64")]
    private static extern void sm64_mario_heal(uint marioId, byte healCounter);

    [DllImport("sm64")]
    private static extern void sm64_set_mario_health(uint marioId, ushort health);

    [DllImport("sm64")]
    private static extern void sm64_mario_kill(uint marioId);

    [DllImport("sm64")]
    private static extern void sm64_static_surfaces_load(SM64Surface[] surfaces, ulong numSurfaces);

    [DllImport("sm64")]
    private static extern uint sm64_mario_create(float marioX, float marioY, float marioZ);

    [DllImport("sm64")]
    private static extern void sm64_mario_tick(uint marioId, ref SM64MarioInputs inputs, ref SM64MarioState outState, ref SM64MarioGeometryBuffers outBuffers);

    [DllImport("sm64")]
    private static extern void sm64_mario_delete(uint marioId);

    [DllImport("sm64")]
    private static extern uint sm64_surface_object_create(ref SM64SurfaceObject surfaceObject);

    [DllImport("sm64")]
    private static extern void sm64_surface_object_move(uint objectId, ref SM64ObjectTransform transform);

    [DllImport("sm64")]
    private static extern void sm64_surface_object_delete(uint objectId);

    private static void DebugPrintCallback(string str)
    {
        ResoniteMod.Msg($"[libsm64] {str}");
    }

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

        // TODO: Use mario texture that we read here, instead of a hardcoded resdb link
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
        while (sm64_get_current_background_music() is var currentMusic && currentMusic != (ushort)MusicId.SEQ_NONE)
        {
            sm64_stop_background_music(currentMusic);
        }
    }

    public static void StaticSurfacesLoad(SM64Surface[] surfaces)
    {
        ResoniteMod.Msg("Reloading all static collider surfaces, this can be caused by the Game Engine " +
                            "Initialized/Destroyed or some component with static colliders was loaded/deleted. " +
                            $"You might notice some lag spike... Total Polygons: {surfaces.Length}");
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

    public static void PlaySoundGlobal(SoundBitsKeys soundBitsKey)
    {
        sm64_play_sound_global((int)Utils.SoundBits[soundBitsKey]);
    }

    public static void PlaySound(SoundBitsKeys soundBitsKey, float3 unityPosition)
    {
        float3 marioPos = unityPosition.ToMarioPosition();
        float[] position = { marioPos.x, marioPos.y, marioPos.z };
        GCHandle posPointer = GCHandle.Alloc(position, GCHandleType.Pinned);

        sm64_play_sound((int)Utils.SoundBits[soundBitsKey], posPointer.AddrOfPinnedObject());
        
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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DebugPrintFuncDelegate(string str);
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

    public MarioCapType FirstActiveCap
    {
        get
        {
            if ((StateFlags & (uint)StateFlag.MARIO_VANISH_CAP) != 0) return MarioCapType.VanishCap;
            if ((StateFlags & (uint)StateFlag.MARIO_METAL_CAP)  != 0) return MarioCapType.MetalCap;
            if ((StateFlags & (uint)StateFlag.MARIO_WING_CAP)   != 0) return MarioCapType.WingCap;
            return MarioCapType.None;
        }
    }

    public List<MarioCapType> AllActiveCaps
    {
        get
        {
            List<MarioCapType> activeCaps = new List<MarioCapType>();

            if ((StateFlags & (uint)StateFlag.MARIO_VANISH_CAP) != 0) activeCaps.Add(MarioCapType.VanishCap);
            if ((StateFlags & (uint)StateFlag.MARIO_METAL_CAP)  != 0) activeCaps.Add(MarioCapType.MetalCap);
            if ((StateFlags & (uint)StateFlag.MARIO_WING_CAP)   != 0) activeCaps.Add(MarioCapType.WingCap);

            if (activeCaps.Count == 0) activeCaps.Add(MarioCapType.None);
            return activeCaps;
        }
    }
    
    public float HealthPoints => Health / Interop.SM64HealthPerHealthPoint;
    
    public bool IsDead => Health < 1 * Interop.SM64HealthPerHealthPoint;
    public bool IsAttacking => (ActionFlags & (uint)ActionFlag.ACT_FLAG_ATTACKING) != 0;
    public bool IsFirstPerson => IsFlyingOrSwimming;
    public bool IsFlyingOrSwimming => (ActionFlags & (uint)ActionFlag.ACT_FLAG_SWIMMING_OR_FLYING) != 0;
    public bool IsSwimming => (ActionFlags & (uint)ActionFlag.ACT_FLAG_SWIMMING) != 0;
    public bool IsFlying => (ActionFlags & (uint)ActionFlag.ACT_FLYING) != 0;
    public bool IsTeleporting => (ActionFlags & (uint)StateFlag.MARIO_TELEPORTING) != 0;

    public bool IsWearingCap(MarioCapType capType) => HasCap(capType);
    public bool HasCap(MarioCapType capType)
    {
        return capType switch
        {
            MarioCapType.VanishCap => (StateFlags & (uint)StateFlag.MARIO_VANISH_CAP) != 0,
            MarioCapType.MetalCap  => (StateFlags & (uint)StateFlag.MARIO_METAL_CAP) != 0,
            MarioCapType.WingCap   => (StateFlags & (uint)StateFlag.MARIO_WING_CAP) != 0,
            _                      => capType == MarioCapType.None
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
internal struct SM64SurfaceObject
{
    public SM64ObjectTransform transform;
    public uint surfaceCount;
    public IntPtr surfaces;
}