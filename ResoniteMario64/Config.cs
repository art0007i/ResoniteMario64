using BepInEx.Configuration;
using Renderite.Shared;

namespace ResoniteMario64;

public class Config
{
    // ENGINE
    public static ConfigEntry<int> GameTickMs;
    public static ConfigEntry<int> MarioScaleFactor;
    public static ConfigEntry<int> MarioCollisionChecks;
    public static ConfigEntry<Uri> MarioUrl;
    
    // CONTROLS
    public static ConfigEntry<bool> UseGamepad;
    public static ConfigEntry<bool> BlockDashWithMarios;
    public static ConfigEntry<Key> UnlockMovementKey;
    public static ConfigEntry<bool> UnlockMovementKeyToggle;

    // AUDIO
    public static ConfigEntry<bool> DisableAudio;
    public static ConfigEntry<bool> PlayRandomMusic;
    public static ConfigEntry<bool> PlayCapMusic;
    public static ConfigEntry<float> AudioVolume;
    public static ConfigEntry<bool> LocalAudio;

    // PERFORMANCE
    public static ConfigEntry<bool> DeleteAfterDeath;
    public static ConfigEntry<float> MarioCullDistance;
    public static ConfigEntry<int> MaxMariosPerPerson;
    public static ConfigEntry<int> MaxMeshColliderTris;

    // DEBUG
    public static ConfigEntry<bool> DebugEnabled;
    public static ConfigEntry<bool> RenderSlotPublic;
    public static ConfigEntry<bool> LogColliderChanges;

    internal static bool ConfigInit(ConfigFile config)
    {
        try
        {
            GameTickMs = config.Bind("Engine", "Game Tick Ms", 25, "How many Milliseconds should a game tick last. This will directly impact the speed of Mario's behavior.");                                                       // slider 1, 100, 0
            MarioScaleFactor = config.Bind("Engine", "Mario Scale Factor", 200, "The base scaling factor used to size Mario and his colliders. Lower values make Mario larger; higher values make him smaller.");                    // slider 1, 1000, 0
            MarioCollisionChecks = config.Bind("Engine", "Mario Collision Checks", 10, "The number of evenly spaced points to check along Mario's body for collisions. Higher values increase accuracy but cost more performance."); // slider 1, 100, 0
            MarioUrl = config.Bind<Uri>("Engine", "Mario Url", null, "The URL for the Non-Modded Renderer for Mario - Null = Default Mario");
            
            UseGamepad = config.Bind("Controls", "Use Gamepad", false, "Whether to use gamepads for input or not.");
            BlockDashWithMarios = config.Bind("Controls", "Block Dash with Marios", true, "Whether to Block opening the dash with marios or not. !VR-Only!");
            UnlockMovementKey = config.Bind("Controls", "Unlock Movement Key", Key.Period, "The key to unlock movement when marios are present.");
            UnlockMovementKeyToggle = config.Bind("Controls", "Unlock Movement Key Toggle", true, "When true the unlock movement key will toggle, when false it needs to be held the entire time.");

            DisableAudio = config.Bind("Audio", "Disable Audio", false, "Whether to disable all Super Mario 64 Music/Sounds or not.");
            PlayRandomMusic = config.Bind("Audio", "Play Random Music", true, "Whether to play a random music when a mario joins or not.");
            PlayCapMusic = config.Bind("Audio", "Play Cap Music", true, "Whether to play the Cap music when a mario picks one up or not.");
            AudioVolume = config.Bind("Audio", "Audio Volume", 0.1f, "The audio volume."); // slider 0f, 1f, 3 (whatever 3 means in BKTUILib.AddSlider) edit: 3 means probably 3 decimal places
            LocalAudio = config.Bind("Audio", "Local Audio", true, "Whether to play the Audio Locally or not.");

            DeleteAfterDeath = config.Bind("Performance", "Delete Mario After Death", true, "Whether to automatically delete our marios after 15 seconds of being dead or not.");
            MarioCullDistance = config.Bind("Performance", "Mario Cull Distance", 15f, "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios."); // slider 0f, 50f, 2 // The max distance that we're going to calculate the mario animations for other people.
            MaxMariosPerPerson = config.Bind("Performance", "Max Marios Per Person", 5, "Max number of Marios per player that will be animated using the Super Mario 64 Engine.");            // slider 0, 20, 0 // The max number of marios per person that we're going to calculate the mario animations for.
            MaxMeshColliderTris = config.Bind("Performance", "Max Mesh Collider Tris", 50000, "Maximum total number of triangles of automatically generated from mesh colliders allowed.");   // slider 0 250000 0 // The max total number of collision tris loaded from automatically generated static mesh colliders.

            DebugEnabled = config.Bind("Debug", "Debug Enabled", false, "Whether to enable debug mode or not.");
            RenderSlotPublic = config.Bind("Debug", "Render Slot Public", true, "When true the renderer slot will not be a local slot.");
            LogColliderChanges = config.Bind("Debug", "Log Collider Changes", false, "Whether to Log Collider changes or not.");

            return true;
        }
        catch (Exception e)
        {
            Logger.Fatal(e);
            return false;
        }
    }
}