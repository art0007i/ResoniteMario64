using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using System.IO;
using System.Security.Cryptography;
using FrooxEngine.UIX;

namespace ResoniteMario64;

public class ResoniteMario64 : ResoniteMod
{
    public override string Name => "ResoniteMario64";
    public override string Author => "art0007i";
    public override string Version => "1.0.0";
    public override string Link => "https://github.com/art0007i/ResoniteMario64/";


    public static bool ShouldAttemptToLoadWorldColliders()
    {
        // TODO: make this func load a per world setting n stuff
        return true;
    }


    // CURRENT WORLD (this setting differs per world... idk how to handle it :/
    /*

    ONCHANGE -> CVRSM64Context.QueueStaticSurfacesUpdate();

    Whether to attempt to auto generate colliders for this world or not. Some worlds are just too laggy to have their colliders generated... If that's the case disable this and use props to create colliders!
    
     */
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_ATTEMPT_LOADING_WORLD_COLLIDERS = new("attempt_loading_world_colliders", "Whether to attempt to auto generate colliders for worlds or not. Some worlds are just too laggy to have their colliders generated... If that's the case disable this and create some colliders manually!", () => true); // // Default option for whether it should attempt to load world colliders or not when joining new worlds.
    // AUDIO
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_DISABLE_AUDIO = new("disable_audio", "Whether to disable all Super Mario 64 Music/Sounds or not.", () => false);
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_PLAY_RANDOM_MUSIC = new("play_random_music", "Whether to play a random music when a mario joins or not.", () => true);
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> KEY_AUDIO_VOLUME = new("audio_volume", "The audio volume.", () => 0.1f); // slider 0f, 1f, 3 (whatever 3 means in BKTUILib.AddSlider)
    // PERFORMANCE
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_DELETE_AFTER_DEATH = new("delete_after_death", "Whether to automatically delete our marios after 15 seconds of being dead or not.", () => true);
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> KEY_MARIO_CULL_DISTANCE = new("mario_cull_distance", "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios.", () => 5f); // slider 0f, 50f, 2 // The max distance that we're going to calculate the mario animations for other people.
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> KEY_MAX_MARIOS_PER_PERSON = new("max_marios_per_person", "Max number of Marios per player that will be animated using the Super Mario 64 Engine.", () => 1); // slider 0, 20, 0 (still dk what the last arg means)
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> KEY_MAX_MESH_COLLIDER_TRIS = new("max_mesh_collider_tris", "Maximum total number of triangles of automatically generated from mesh colliders allowed.", () => 50000); // slider 0 250000 0 // The max total number of collision tris loaded from automatically generated static mesh colliders.
    // ENGINE
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> KEY_AUDIO_PITCH = new("audio_pitch", "The audio pitch of the game sounds. You can use this to fine tune the Engine Sounds.", () => 0.74f); // slider 0f, 1f, 2
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> KEY_GAME_TICK_MS = new("game_tick_ms", "How many Milliseconds should a game tick last. This will directly impact the speed of Mario's behavior.", () => 25); // slider 1, 100, 0

    public static ModConfiguration config;

    // Rom
    private const string SuperMario64UsZ64RomHashHex = "20b854b239203baf6c961b850a4a51a2"; // MD5 hash
    private const string SuperMario64UsZ64RomName = "baserom.us.z64";
    internal static byte[] SuperMario64UsZ64RomBytes;

    // Internal
    internal static bool FilesLoaded = false;

    public override void OnEngineInit()
    {
        config = GetConfiguration();
        /*
         * use rml_libs
        // Extract the native binary to the plugins folder
        const string dllName = "sm64.dll";
        var dstPath = Path.GetFullPath(Path.Combine("Resonite_Data", "Plugins", "x86_64", dllName));

        try
        {
            Msg($"Copying the sm64.dll to {dstPath}");
            // TODO: hopefully works :)
            using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(dllName);
            using var fileStream = File.Open(dstPath, FileMode.Create, FileAccess.Write);
            resourceStream!.CopyTo(fileStream);
        }
        catch (IOException ex)
        {
            Error("Failed to copy native library.");
            Error(ex);
            return;
        }
        */
        // Load the ROM
        try
        {
            var smRomPath = Path.GetFullPath(Path.Combine("rml_libs", SuperMario64UsZ64RomName));
            Msg($"Loading the Super Mario 64 [US] z64 ROM from {smRomPath}...");
            var smRomFileInfo = new FileInfo(smRomPath);
            if (!smRomFileInfo.Exists)
            {
                Error($"You need to download the Super Mario 64 [US] z64 ROM " +
                                  $"(MD5 {SuperMario64UsZ64RomHashHex}), rename the file to {SuperMario64UsZ64RomName} " +
                                  $"and save it to the path: {smRomPath}");
                return;
            }
            using var md5 = MD5.Create();
            using var smRomFileSteam = File.OpenRead(smRomPath);
            var smRomFileMd5Hash = md5.ComputeHash(smRomFileSteam);
            var smRomFileMd5HashHex = BitConverter.ToString(smRomFileMd5Hash).Replace("-", "").ToLowerInvariant();

            if (smRomFileMd5HashHex != SuperMario64UsZ64RomHashHex)
            {
                Error($"The file at {smRomPath} MD5 hash is {smRomFileMd5HashHex}. That file needs to be a copy of " +
                                  $"Super Mario 64 [US] z64 ROM, which has a MD5 Hash of {SuperMario64UsZ64RomHashHex}");
                return;
            }

            SuperMario64UsZ64RomBytes = File.ReadAllBytes(smRomPath);
        }
        catch (Exception ex)
        {
            Error("Failed to Load the Super Mario 64 [US] z64 ROM");
            Error(ex);
            return;
        }

        FilesLoaded = true;
        Harmony harmony = new Harmony("me.art0007i.ResoniteMario64");
        harmony.PatchAll();
    }


    [HarmonyPatch(typeof(Slot), nameof(Slot.BuildInspectorUI))]
    class SlotUIAddon
    {
        public static void Postfix(Slot __instance, UIBuilder ui)
        {
            ui.Button("Spawn Mario64!").LocalPressed += (b,e)=>{
                __instance.AttachComponent<SM64Mario>();
            };
        }
    }
}
