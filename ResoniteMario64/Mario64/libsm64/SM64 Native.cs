using System.Runtime.InteropServices;

namespace ResoniteMario64.Mario64.libsm64;

internal unsafe static partial class SM64Native
{
    private const string LibName = "sm64";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SM64RumbleCallbackFunctionPtr(int marioId, short level, short time);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SM64DebugPrintFunctionPtr(nint message);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SM64PlaySoundFunctionPtr(uint soundBits, float* pos);

    // Initialization & Setup
    [LibraryImport(LibName)]
    public static partial void sm64_register_rumble_callback_function(nint rumbleCallbackFunctionPtr);

    [LibraryImport(LibName)]
    public static partial void sm64_register_debug_print_function(nint debugPrintFunctionPtr);

    [LibraryImport(LibName)]
    public static partial void sm64_register_play_sound_function(nint playSoundFunction);

    [LibraryImport(LibName)]
    public static partial void sm64_global_init(byte* rom, byte* outTexture);

    [LibraryImport(LibName)]
    public static partial void sm64_global_terminate();

    [LibraryImport(LibName)]
    public static partial void sm64_audio_init(byte* rom);

    // Audio & Music
    [LibraryImport(LibName)]
    public static partial uint sm64_audio_tick(uint numQueuedSamples, uint numDesiredSamples, short* audioBuffer);

    [LibraryImport(LibName)]
    public static partial void sm64_seq_player_play_sequence(byte player, byte seqId, ushort arg2);

    [LibraryImport(LibName)]
    public static partial void sm64_play_music(byte player, ushort seqArgs, ushort fadeTimer);

    [LibraryImport(LibName)]
    public static partial void sm64_stop_background_music(ushort seqId);

    [LibraryImport(LibName)]
    public static partial void sm64_fadeout_background_music(ushort arg0, ushort fadeOut);

    [LibraryImport(LibName)]
    public static partial void sm64_play_cap_music(ushort playMusic);

    [LibraryImport(LibName)]
    public static partial void sm64_stop_cap_music();

    [LibraryImport(LibName)]
    public static partial void sm64_play_shell_music();

    [LibraryImport(LibName)]
    public static partial void sm64_stop_shell_music();

    [LibraryImport(LibName)]
    public static partial ushort sm64_get_current_background_music();

    [LibraryImport(LibName)]
    public static partial void sm64_play_sound(int soundBits, float* pos);

    [LibraryImport(LibName)]
    public static partial void sm64_play_sound_global(int soundBits);

    [LibraryImport(LibName)]
    public static partial void sm64_set_sound_volume(float vol);

    // Mario Lifecycle
    [LibraryImport(LibName)]
    public static partial int sm64_mario_create(float x, float y, float z, byte isLocal);

    [LibraryImport(LibName)]
    public static partial void sm64_mario_tick(int marioId, SM64MarioInputs* inputs, SM64MarioState* outState, SM64MarioGeometryBuffers* outBuffers);

    [LibraryImport(LibName)]
    public static partial void sm64_mario_delete(int marioId);

    // Mario Actions & Status
    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_action(int marioId, uint action);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_action_arg(int marioId, uint action, uint actionArg);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_state(int marioId, uint flags);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_health(int marioId, ushort health);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_invincibility(int marioId, short timer);

    [LibraryImport(LibName)]
    public static partial void sm64_mario_take_damage(int marioId, uint damage, uint subtype, float x, float y, float z);

    [LibraryImport(LibName)]
    public static partial void sm64_mario_heal(int marioId, byte healCounter);

    [LibraryImport(LibName)]
    public static partial void sm64_mario_kill(int marioId);

    [LibraryImport(LibName)]
    public static partial void sm64_mario_interact_cap(int marioId, uint capFlag, ushort capTime, byte playMusic);

    [LibraryImport(LibName)]
    public static partial void sm64_mario_extend_cap(int marioId, ushort capTime);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_animation(int marioId, ushort animId);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_anim_frame(int marioId, short frame);

    [LibraryImport(LibName)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static partial bool sm64_mario_attack(int marioId, float x, float y, float z, float hitboxHeight);

    // Mario Transform 
    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_position(int marioId, float x, float y, float z);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_angle(int marioId, float x, float y, float z);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_faceangle(int marioId, float y);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_velocity(int marioId, float x, float y, float z);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_forward_velocity(int marioId, float vel);

    /* Mario Environmental Effects */
    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_water_level(int marioId, int level);

    [LibraryImport(LibName)]
    public static partial void sm64_set_mario_gas_level(int marioId, int level);

    // Static & Dynamic Surfaces
    [LibraryImport(LibName)]
    public static partial void sm64_static_surfaces_load(SM64Surface* surfaces, uint numSurfaces);

    [LibraryImport(LibName)]
    public static partial uint sm64_surface_object_create(SM64SurfaceObject* surfaceObject);

    [LibraryImport(LibName)]
    public static partial void sm64_surface_object_move(uint objectId, SM64ObjectTransform* transform);

    [LibraryImport(LibName)]
    public static partial void sm64_surface_object_delete(uint objectId);

    // Collision & Geometry Queries
    [LibraryImport(LibName)]
    public static partial int sm64_surface_find_wall_collision(float* x, float* y, float* z, float offsetY, float radius);

    [LibraryImport(LibName)]
    public static partial int sm64_surface_find_wall_collisions(SM64WallCollisionData* colData);

    [LibraryImport(LibName)]
    public static partial float sm64_surface_find_ceil(float x, float y, float z, SM64SurfaceCollisionData** ceil);

    [LibraryImport(LibName)]
    public static partial float sm64_surface_find_floor(float x, float y, float z, SM64SurfaceCollisionData** floor);

    [LibraryImport(LibName)]
    public static partial float sm64_surface_find_floor_height(float x, float y, float z);

    [LibraryImport(LibName)]
    public static partial float sm64_surface_find_floor_height_and_data(float x, float y, float z, SM64FloorCollisionData** floorGeo);

    [LibraryImport(LibName)]
    public static partial float sm64_surface_find_water_level(float x, float z);

    [LibraryImport(LibName)]
    public static partial float sm64_surface_find_poison_gas_level(float x, float z);
    
    [LibraryImport(LibName)]    
    public static partial int sm64_fake_object_create(float x, float y, float z, int preset);
    
    [LibraryImport(LibName)]
    public static partial void sm64_fake_object_delete(int objectId);
    
    [LibraryImport(LibName)]
    public static partial void sm64_fake_object_set_position(int objectId, float x, float y, float z);
    
    [LibraryImport(LibName)]
    public static partial void sm64_fake_object_set_hitbox(int objectId, float radius, float height, float downOffset);
    
    [LibraryImport(LibName)]
    public static partial void sm64_fake_object_tick(int objectId);
}