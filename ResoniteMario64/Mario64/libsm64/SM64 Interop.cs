using System.Runtime.InteropServices;
using Elements.Core;
using ResoniteMario64.Mario64.Components.Context;
using static ResoniteMario64.Mario64.libsm64.SM64Constants;

namespace ResoniteMario64.Mario64.libsm64;

public static class MarioExtensions
{
    public static float3 ToMarioRotation(this float3 rot) => new float3(FixAngle(-rot.x), FixAngle(rot.y), FixAngle(rot.z));

    public static float3 FromMarioRotation(this float3 rot) => new float3(FixAngle(-rot.x), FixAngle(rot.y), FixAngle(rot.z));

    public static float3 ToMarioPosition(this float3 pos) => SM64Interop.ScaleFactor * pos * new float3(-1, 1, 1);

    public static float3 FromMarioPosition(this float3 pos) => pos / SM64Interop.ScaleFactor * new float3(-1, 1, 1);

    public static float ToMarioFloat(this float value) => SM64Interop.ScaleFactor * value;

    public static float3 ToMarioFloat(this float3 value) => SM64Interop.ScaleFactor * value;

    public static float FromMarioFloat(this float value) => value / SM64Interop.ScaleFactor;

    public static float3 FromMarioFloat(this float3 value) => value / SM64Interop.ScaleFactor;

    private static float FixAngle(float a) => Fmod(a + 180.0f, 360.0f) - 180.0f;

    private static float Fmod(float a, float b) => a - b * MathX.Floor(a / b);
}

public unsafe static class SM64Interop
{
    public static float ScaleFactor => SM64Context.Instance?.ContextVariableSpace?.TryReadValue("Scale", out float scale) ?? false ? scale : Config.MarioScaleFactor.Value;

    private const int SM64TextureWidth = 64 * 11;
    private const int SM64TextureHeight = 64;
    public const int SM64GeoMaxTriangles = 1024;

    public const float SM64HealthPerHealthPoint = 256;
    private const byte HealPointMultiplier = 4;
    public const float SM64MaxHealth = 8.5f;
    public const ushort SM64MaxHealthRaw = 0x880;

    private const byte SecondsMultiplier = 40;

    public const int SM64LevelResetValue = -10000;

    private const float SM64MaxVertexDistance = float.MaxValue;

    public const float SM64Deg2Angle = 182.04459f;

    // public static Bitmap2D MarioTexture { get; private set; }
    // public static Texture2D MarioTexture2D { get; private set; }
    // public static Uri MarioTextureUri { get; private set; }
    public static bool IsGlobalInit;

    private static readonly SM64Native.SM64RumbleCallbackFunctionPtr RumbleCallback = SM64Context.VibrateCallback;
    private static readonly SM64Native.SM64DebugPrintFunctionPtr DebugPrintCallback = DebugPrint;

    private static void DebugPrint(string str)
    {
        if (!Config.DebugEnabled.Value || !Config.LibSM64DebugEnabled.Value) return;
        Logger.Debug($"[libsm64] {str}");
    }

    public static void GlobalInit(byte[] rom)
    {
        byte[] textureData = new byte[4 * SM64TextureWidth * SM64TextureHeight];

        fixed (byte* romPtr = rom)
        fixed (byte* texturePtr = textureData)
        {
            SM64Native.sm64_global_init(romPtr, texturePtr);
            SM64Native.sm64_audio_init(romPtr);

            SM64Native.sm64_register_rumble_callback_function(Marshal.GetFunctionPointerForDelegate(RumbleCallback));

            // This is laggy as all balls with audio.
            SM64Native.sm64_register_debug_print_function(Marshal.GetFunctionPointerForDelegate(DebugPrintCallback));

            // MarioTexture = new Bitmap2D(SM64TextureWidth, SM64TextureHeight, TextureFormat.RGBA32, false, ColorProfile.sRGB, false, null, Engine.Current.AssetManager.TextureAllocator);
            // for (int ix = 0; ix < SM64TextureWidth; ix++)
            // for (int iy = 0; iy < SM64TextureHeight; iy++)
            // {
            //     color32 color = new color32(
            //         textureData[4 * (ix + SM64TextureWidth * iy) + 0],
            //         textureData[4 * (ix + SM64TextureWidth * iy) + 1],
            //         textureData[4 * (ix + SM64TextureWidth * iy) + 2],
            //         textureData[4 * (ix + SM64TextureWidth * iy) + 3]
            //     );
            //     // Make the 100% transparent colors white. so we can multiply with the vertex colors.
            //     if (color.a == 0)
            //     {
            //         color = new color32(255, 255, 255, 0);
            //     }
            //
            //     MarioTexture.SetPixel32(ix, iy, color);
            // }
            //
            // MarioTexture2D = new Texture2D();
            // MarioTexture2D.InitializeDynamic(Engine.Current.AssetManager);
            // MarioTexture2D.SetFromBitmap2D(SM64Interop.MarioTexture, new TextureUploadHint { readable = true }, TextureFilterMode.Point, 0, TextureWrapMode.Clamp, TextureWrapMode.Clamp, 0, delegate { });
            // MarioTextureUri = Engine.Current.LocalDB.SaveAssetAsync(MarioTexture2D.Data).Result;

            // MarioTexture.Save("mario.png");
        }

        IsGlobalInit = true;
    }

    public static void GlobalTerminate()
    {
        StopMusic();
        SM64Native.sm64_global_terminate();
        // MarioTexture = null;
        IsGlobalInit = false;
    }

    public static bool IsAnyMusicPlaying() => SM64Native.sm64_get_current_background_music() != (ushort)MusicSequence.None;

    public static bool IsMusicPlaying(MusicSequence music) => SM64Native.sm64_get_current_background_music() == (ushort)music;

    public static void PlayMusic(byte player, ushort seqArgs, ushort fadeTimer)
    {
        StopMusic();
        SM64Native.sm64_play_music(player, seqArgs, fadeTimer);
    }
    
    public static void PlayMusic(MusicSequence music)
    {
        PlayMusic(0, (ushort)music, 0);
    }

    public static void PlayRandomMusic()
    {
        PlayMusic(0, Musics[RandomX.Range(0, Musics.Length)], 0);
    }

    public static void StopMusic()
    {
        // Stop all music that was queued
        while (SM64Native.sm64_get_current_background_music() is var currentMusic && currentMusic != (ushort)MusicSequence.None)
        {
            SM64Native.sm64_stop_background_music(currentMusic);
        }
    }

    public static void PlayCapMusic(ushort seq)
    {
        SM64Native.sm64_play_cap_music(seq);
    }

    public static void StopCapMusic()
    {
        SM64Native.sm64_stop_cap_music();
    }

    public static void PlayShellMusic()
    {
        SM64Native.sm64_play_shell_music();
    }

    public static void StopShellMusic()
    {
        SM64Native.sm64_stop_shell_music();
    }

    public static void FadeoutBackgroundMusic(ushort fadeOut)
    {
        ushort currentMusic = SM64Native.sm64_get_current_background_music();
        if (currentMusic != (ushort)MusicSequence.None)
        {
            SM64Native.sm64_fadeout_background_music(currentMusic, fadeOut);
        }
    }

    public static void StaticSurfacesLoad(SM64Surface[] surfaces)
    {
        Logger.Debug($"Reloading all static collider surfaces - Total Polygons: {surfaces.Length}");
        fixed (SM64Surface* surface = surfaces)
        {
            SM64Native.sm64_static_surfaces_load(surface, (uint)surfaces.Length);
        }
    }

    public static int MarioCreate(float3 marioPos) => SM64Native.sm64_mario_create(marioPos.x, marioPos.y, marioPos.z);

    public static SM64MarioState MarioTick(int marioId, SM64MarioInputs inputs, float3[] positionBuffer, float3[] normalBuffer, float3[] colorBuffer, float2[] uvBuffer, out ushort numTrianglesUsed)
    {
        SM64MarioState outState = new SM64MarioState();

        fixed (float3* position = positionBuffer)
        fixed (float3* normal = normalBuffer)
        fixed (float3* color = colorBuffer)
        fixed (float2* uv = uvBuffer)
        {
            SM64MarioGeometryBuffers buff = new SM64MarioGeometryBuffers
            {
                position = (float*)position,
                normal = (float*)normal,
                color = (float*)color,
                uv = (float*)uv
            };

            SM64Native.sm64_mario_tick(marioId, &inputs, &outState, &buff);

            numTrianglesUsed = buff.numTrianglesUsed;
        }

        return outState;
    }

    public static uint AudioTick(short[] audioBuffer, uint numDesiredSamples, uint numQueuedSamples = 0)
    {
        fixed (short* audio = audioBuffer)
        {
            return SM64Native.sm64_audio_tick(numQueuedSamples, numDesiredSamples, audio);
        }
    }

    public static void PlaySoundGlobal(Sounds soundKey)
    {
        SM64Native.sm64_play_sound_global((int)SoundBank[soundKey]);
    }

    public static void PlaySound(Sounds soundKey, float3 frooxPosition)
    {
        float3 marioPos = frooxPosition.ToMarioPosition();
        float[] position = { marioPos.x, marioPos.y, marioPos.z };

        fixed (float* pos = position)
        {
            SM64Native.sm64_play_sound((int)SoundBank[soundKey], pos);
        }
    }

    public static void MarioDelete(int marioId)
    {
        SM64Native.sm64_mario_delete(marioId);
    }

    public static bool MarioAttack(int marioId, float3 frooxPosition, float hitboxHeight)
    {
        float3 marioPos = frooxPosition.ToMarioPosition();
        return SM64Native.sm64_mario_attack(marioId, marioPos.x, marioPos.y, marioPos.z, hitboxHeight.ToMarioFloat());
    }

    public static void MarioTakeDamage(int marioId, float3 frooxPosition, uint damage, uint subtype = 0)
    {
        float3 marioPos = frooxPosition.ToMarioPosition();
        SM64Native.sm64_mario_take_damage(marioId, damage, subtype, marioPos.x, marioPos.y, marioPos.z);
    }

    public static void MarioSetVelocity(int marioId, SM64MarioState previousState, SM64MarioState currentState)
    {
        SM64Native.sm64_set_mario_velocity(marioId, currentState.Position[0] - previousState.Position[0], currentState.Position[1] - previousState.Position[1], currentState.Position[2] - previousState.Position[2]);
    }

    public static void MarioSetVelocity(int marioId, float3 frooxVelocity)
    {
        float3 marioVelocity = frooxVelocity.ToMarioPosition();
        SM64Native.sm64_set_mario_velocity(marioId, marioVelocity.x, marioVelocity.y, marioVelocity.z);
    }

    public static void MarioSetForwardVelocity(int marioId, float frooxVelocity)
    {
        SM64Native.sm64_set_mario_forward_velocity(marioId, frooxVelocity * ScaleFactor);
    }

    public static void CreateAndAppendSurfaces(List<SM64Surface> outSurfaces, int[] triangles, float3[] vertices, SurfaceType surfaceType, TerrainType terrainType, SurfaceFlag flags, int force)
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            SM64Surface? surface = Config.ClampedSurfaces.Value ? ClampedSurface(i, triangles, vertices, surfaceType, terrainType, flags, force) : NonClampedSurface(i, triangles, vertices, surfaceType, terrainType, flags, force);

            if (!surface.HasValue) continue;

            outSurfaces.Add(surface.Value);
        }
    }

    private static SM64Surface? ClampedSurface(int triangleIndex, int[] triangles, float3[] vertices, SurfaceType surfaceType, TerrainType terrainType, SurfaceFlag flags, int force)
    {
        float3 v1 = vertices[triangles[triangleIndex]];
        float3 v2 = vertices[triangles[triangleIndex + 1]];
        float3 v3 = vertices[triangles[triangleIndex + 2]];

        float3 p1 = new float3(-v1.x, v1.y, v1.z);
        float3 p2 = new float3(-v2.x, v2.y, v2.z);
        float3 p3 = new float3(-v3.x, v3.y, v3.z);

        (p2, p3) = (p3, p2);

        float3 e1 = p2 - p1;
        float3 e2 = p3 - p1;
        float3 normal = new float3(e1.y * e2.z - e1.z * e2.y, e1.z * e2.x - e1.x * e2.z, e1.x * e2.y - e1.y * e2.x);

        float normalLengthSquared = normal.x * normal.x + normal.y * normal.y + normal.z * normal.z;
        if (normalLengthSquared <= 1e-6f)
            return null;

        return new SM64Surface
        {
            Force = (short)(force == -1 ? 0 : force),
            Type = surfaceType,
            Terrain = terrainType,
            Flags = flags,

            v0x = ClampToSm64(p1.x),
            v0y = ClampToSm64(p1.y),
            v0z = ClampToSm64(p1.z),

            v1x = ClampToSm64(p2.x),
            v1y = ClampToSm64(p2.y),
            v1z = ClampToSm64(p2.z),

            v2x = ClampToSm64(p3.x),
            v2y = ClampToSm64(p3.y),
            v2z = ClampToSm64(p3.z)
        };
    }

    private static float ClampToSm64(float value)
    {
        float scaled = ScaleFactor * value;
        return Math.Clamp(scaled, -SM64MaxVertexDistance, SM64MaxVertexDistance);
    }

    private static SM64Surface NonClampedSurface(int i, int[] triangles, float3[] vertices, SurfaceType surfaceType, TerrainType terrainType, SurfaceFlag flags, int force)
    {
        return new SM64Surface
        {
            Force = (short)(force == -1 ? 0 : force),
            Type = surfaceType,
            Terrain = terrainType,
            Flags = flags,

            v0x = ScaleFactor * -vertices[triangles[i]].x,
            v0y = ScaleFactor * vertices[triangles[i]].y,
            v0z = ScaleFactor * vertices[triangles[i]].z,

            v1x = ScaleFactor * -vertices[triangles[i + 2]].x,
            v1y = ScaleFactor * vertices[triangles[i + 2]].y,
            v1z = ScaleFactor * vertices[triangles[i + 2]].z,

            v2x = ScaleFactor * -vertices[triangles[i + 1]].x,
            v2y = ScaleFactor * vertices[triangles[i + 1]].y,
            v2z = ScaleFactor * vertices[triangles[i + 1]].z
        };
    }

    public static void SetWaterLevel(int marioId, float waterLevel)
    {
        SM64Native.sm64_set_mario_water_level(marioId, (int)waterLevel.ToMarioFloat());
    }

    public static void SetGasLevel(int marioId, float gasLevel)
    {
        SM64Native.sm64_set_mario_gas_level(marioId, (int)gasLevel.ToMarioFloat());
    }

    public static float FindFloor(float3 pos, out SM64SurfaceCollisionData? data)
    {
        float3 marioPos = pos.ToMarioPosition();
        SM64SurfaceCollisionData* floorPtr;
        float floorHeightMario = SM64Native.sm64_surface_find_floor(marioPos.x, marioPos.y, marioPos.z, &floorPtr);
        data = floorPtr == null ? null : *floorPtr;
        return floorHeightMario.FromMarioFloat();
    }

    public static float FindFloorHeight(float3 pos)
    {
        float3 marioPos = pos.ToMarioPosition();
        float floorHeightMario = SM64Native.sm64_surface_find_floor_height(marioPos.x, marioPos.y, marioPos.z);
        return floorHeightMario.FromMarioFloat();
    }

    public static float FindFloorHeightAndData(float3 pos, out SM64FloorCollisionData? data)
    {
        float3 marioPos = pos.ToMarioPosition();
        SM64FloorCollisionData* floorGeoPtr;
        float floorHeightMario = SM64Native.sm64_surface_find_floor_height_and_data(marioPos.x, marioPos.y, marioPos.z, &floorGeoPtr);
        data = floorGeoPtr == null ? null : *floorGeoPtr;
        return floorHeightMario.FromMarioFloat();
    }

    public static float FindCeil(float3 pos, out SM64SurfaceCollisionData? data)
    {
        float3 marioPos = pos.ToMarioPosition();
        SM64SurfaceCollisionData* ceilPtr;
        float ceilHeightMario = SM64Native.sm64_surface_find_ceil(marioPos.x, marioPos.y, marioPos.z, &ceilPtr);
        data = ceilPtr == null ? null : *ceilPtr;
        return ceilHeightMario.FromMarioFloat();
    }

    public static float FindWaterLevel(float3 pos)
    {
        float3 marioPos = pos.ToMarioPosition();
        return SM64Native.sm64_surface_find_water_level(marioPos.x, marioPos.z).FromMarioFloat();
    }

    public static float FindPoisonGasLevel(float3 pos)
    {
        float3 marioPos = pos.ToMarioPosition();
        return SM64Native.sm64_surface_find_poison_gas_level(marioPos.x, marioPos.z).FromMarioFloat();
    }

    public static uint SurfaceObjectCreate(float3 position, floatQ rotation, SM64Surface[] surfaces)
    {
        SM64ObjectTransform transform = SM64ObjectTransform.FromFrooxWorld(position, rotation);

        fixed (SM64Surface* surface = surfaces)
        {
            SM64SurfaceObject surfObj = new SM64SurfaceObject
            {
                transform = transform,
                surfaceCount = (uint)surfaces.Length,
                surfaces = surface
            };

            return SM64Native.sm64_surface_object_create(&surfObj);
        }
    }

    public static void SurfaceObjectMove(uint id, float3 position, floatQ rotation)
    {
        SM64ObjectTransform t = SM64ObjectTransform.FromFrooxWorld(position, rotation);
        SM64Native.sm64_surface_object_move(id, &t);
    }

    public static void SurfaceObjectDelete(uint id)
    {
        SM64Native.sm64_surface_object_delete(id);
    }

    public static void MarioCap(int marioId, StateFlag stateFlag, float durationSeconds, bool playCapMusic)
    {
        SM64Native.sm64_mario_interact_cap(marioId, (uint)stateFlag, (ushort)(durationSeconds * SecondsMultiplier), (byte)(playCapMusic ? 1 : 0));
    }

    public static void MarioCap(int marioId, uint flag, float durationSeconds, bool playCapMusic)
    {
        SM64Native.sm64_mario_interact_cap(marioId, flag, (ushort)(durationSeconds * SecondsMultiplier), (byte)(playCapMusic ? 1 : 0));
    }

    public static void MarioCapExtend(int marioId, float durationSeconds)
    {
        SM64Native.sm64_mario_extend_cap(marioId, (ushort)(durationSeconds * SecondsMultiplier));
    }

    public static void MarioSetPosition(int marioId, float3 pos)
    {
        float3 marioPos = pos.ToMarioPosition();
        SM64Native.sm64_set_mario_position(marioId, marioPos.x, marioPos.y, marioPos.z);
    }

    public static void MarioSetFaceAngle(int marioId, floatQ rot)
    {
        float angleInDegrees = rot.EulerAngles.y;
        if (angleInDegrees > 180f)
        {
            angleInDegrees -= 360f;
        }

        SM64Native.sm64_set_mario_faceangle(marioId, -MathX.Deg2Rad * angleInDegrees);
    }

    public static void MarioSetRotation(int marioId, floatQ rotation)
    {
        float3 marioRotation = rotation.EulerAngles.ToMarioRotation();
        SM64Native.sm64_set_mario_angle(marioId, marioRotation.x, marioRotation.y, marioRotation.z);
    }

    public static void MarioSetHealthPointsRaw(int marioId, ushort healthPoints)
    {
        SM64Native.sm64_set_mario_health(marioId, healthPoints);
    }

    public static void MarioSetHealthPoints(int marioId, float healthPoints)
    {
        SM64Native.sm64_set_mario_health(marioId, (ushort)(healthPoints * SM64HealthPerHealthPoint));
    }

    public static void MarioSetFullHealth(int marioId)
    {
        SM64Native.sm64_set_mario_health(marioId, SM64MaxHealthRaw);
    }

    public static void MarioSetInvincibility(int marioId, float timeMs)
    {
        short frames = (short)(timeMs / 1000.0f * Config.GameTickMs.Value);
        SM64Native.sm64_set_mario_invincibility(marioId, frames);
    }

    public static void MarioHeal(int marioId, byte healthPoints)
    {
        // It was healing 0.25 with 1, so we multiplied by 4 EZ FIX
        SM64Native.sm64_mario_heal(marioId, (byte)(healthPoints * HealPointMultiplier));
    }

    public static void MarioKill(int marioId)
    {
        SM64Native.sm64_mario_kill(marioId);
    }

    public static void MarioSetAction(int marioId, ActionFlag actionFlag)
    {
        SM64Native.sm64_set_mario_action(marioId, (uint)actionFlag);
    }

    public static void MarioSetAction(int marioId, uint actionFlags)
    {
        SM64Native.sm64_set_mario_action(marioId, actionFlags);
    }

    public static void MarioSetAction(int marioId, ActionFlag actionFlag, uint actionArg)
    {
        SM64Native.sm64_set_mario_action_arg(marioId, (uint)actionFlag, actionArg);
    }

    public static void MarioSetState(int marioId, StateFlag stateFlag)
    {
        SM64Native.sm64_set_mario_state(marioId, (uint)stateFlag);
    }

    public static void MarioSetState(int marioId, uint stateFlags)
    {
        SM64Native.sm64_set_mario_state(marioId, stateFlags);
    }

    public static void MarioSetAnimation(int marioId, ushort animId)
    {
        SM64Native.sm64_set_mario_animation(marioId, animId);
    }

    public static void MarioSetAnimFrame(int marioId, short frame)
    {
        SM64Native.sm64_set_mario_anim_frame(marioId, frame);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct SM64Surface
{
    public SurfaceType Type;
    public short Force;
    public TerrainType Terrain;
    public SurfaceFlag Flags;
    public float v0x, v0y, v0z;
    public float v1x, v1y, v1z;
    public float v2x, v2y, v2z;
}

[StructLayout(LayoutKind.Sequential)]
public struct SM64MarioInputs
{
    public float camLookX, camLookZ;
    public float stickX, stickY;
    public byte buttonA, buttonB, buttonZ;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SM64Animation
{
    public AnimationFlags Flags;
    public short AnimYTransDivisor;
    public short StartFrame;
    public short LoopStart;
    public short LoopEnd;
    public short UnusedBoneCount;

    public short* values;
    public ushort* index;

    public uint Length;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SM64AnimInfo
{
    public MarioAnimationID AnimID;
    public short AnimYTrans;
    public SM64Animation* CurrentAnimPtr;
    public SM64Animation CurrentAnim => CurrentAnimPtr == null ? default(SM64Animation) : *CurrentAnimPtr;

    public short AnimFrame;
    public ushort AnimTimer;

    public int AnimFrameAccelAssist;
    public int AnimAccel;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SM64MarioState
{
    public fixed float Position[3];
    public fixed float Velocity[3];
    public float FacingAngle;
    public float ForwardVelocity;
    public short Health;
    public ActionFlag ActionFlags;
    public SM64AnimInfo* AnimInfoPtr;
    public SM64AnimInfo AnimInfo => AnimInfoPtr == null ? default(SM64AnimInfo) : *AnimInfoPtr;
    public StateFlag StateFlags;
    public ParticleFlags ParticleFlags;
    public short InvincibleTimer;

    public float3 ScaledPosition => new float3(-Position[0], Position[1], Position[2]).FromMarioFloat();
    public floatQ ScaledRotation => floatQ.Euler(0f, MathX.Repeat(-MathX.Rad2Deg * FacingAngle + 180f, 360f) - 180f, 0f);

    public float HealthPoints => Health / SM64Interop.SM64HealthPerHealthPoint;

    public bool IsDead => Health <= 0xFF;
    public bool IsAttacking => (ActionFlags & ActionFlag.Attacking) != 0;
    public bool IsFirstPerson => IsFlyingOrSwimming;
    public bool IsFlyingOrSwimming => (ActionFlags & ActionFlag.SwimmingOrFlying) != 0;
    public bool IsSwimming => (ActionFlags & ActionFlag.Swimming) != 0;
    public bool IsFlying => (ActionFlags & ActionFlag.Flying) == ActionFlag.Flying;
    public bool IsTeleporting => (StateFlags & StateFlag.Teleporting) != 0;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SM64MarioGeometryBuffers
{
    public float* position;
    public float* normal;
    public float* color;
    public float* uv;
    public ushort numTrianglesUsed;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SM64ObjectTransform
{
    public fixed float Position[3];
    public fixed float EulerRotation[3];

    public static SM64ObjectTransform FromFrooxWorld(float3 position, floatQ rotation)
    {
        SM64ObjectTransform result = new SM64ObjectTransform();
        float3 pos = position.ToMarioPosition();
        float3 rot = rotation.EulerAngles.ToMarioRotation();

        result.Position[0] = pos.x;
        result.Position[1] = pos.y;
        result.Position[2] = pos.z;

        result.EulerRotation[0] = rot.x;
        result.EulerRotation[1] = rot.y;
        result.EulerRotation[2] = rot.z;

        return result;
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
public unsafe struct SM64SurfaceObject
{
    public SM64ObjectTransform transform;
    public uint surfaceCount;
    public SM64Surface* surfaces;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SM64SurfaceCollisionData
{
    public SurfaceType type;
    public short force;
    public SurfaceFlag flags;
    public sbyte room;
    public float lowerY;
    public float upperY;

    public fixed float vertex1[3];
    public fixed float vertex2[3];
    public fixed float vertex3[3];

    public float3 normal;
    public float originOffset;
    public byte isValid;
    public IntPtr transform;
    public TerrainType terrain;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct SM64FloorCollisionData
{
    public fixed float unused[4];

    public float normalX;
    public float normalY;
    public float normalZ;
    public float originOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SM64WallCollisionData
{
    public float x;
    public float y;
    public float z;

    public float offsetY;
    public float radius;

    public short unk14;
    public short numWalls;

    public SM64SurfaceCollisionData* wall0;
    public SM64SurfaceCollisionData* wall1;
    public SM64SurfaceCollisionData* wall2;
    public SM64SurfaceCollisionData* wall3;
}