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
using BepuPhysics.Constraints;

namespace ResoniteMario64;

public class ResoniteMario64 : ResoniteMod
{
    public override string Name => "ResoniteMario64";
    public override string Author => "art0007i";
    public override string Version => "1.0.0";
    public override string Link => "https://github.com/art0007i/ResoniteMario64/";
    // AUDIO
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_DISABLE_AUDIO = new("disable_audio", "Whether to disable all Super Mario 64 Music/Sounds or not.", () => false);
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_PLAY_RANDOM_MUSIC = new("play_random_music", "Whether to play a random music when a mario joins or not.", () => true);
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> KEY_AUDIO_VOLUME = new("audio_volume", "The audio volume.", () => 0.1f); // slider 0f, 1f, 3 (whatever 3 means in BKTUILib.AddSlider) edit: 3 means probably 3 decimal places
    // PERFORMANCE
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_DELETE_AFTER_DEATH = new("delete_after_death", "Whether to automatically delete our marios after 15 seconds of being dead or not.", () => true);
    // TODO: implement these settings (maybe)
    //[AutoRegisterConfigKey]
    //public static ModConfigurationKey<float> KEY_MARIO_CULL_DISTANCE = new("mario_cull_distance", "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios.", () => 5f); // slider 0f, 50f, 2 // The max distance that we're going to calculate the mario animations for other people.
    //[AutoRegisterConfigKey]
    //public static ModConfigurationKey<int> KEY_MAX_MARIOS_PER_PERSON = new("max_marios_per_person", "Max number of Marios per player that will be animated using the Super Mario 64 Engine.", () => 1); // slider 0, 20, 0 (still dk what the last arg means)
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> KEY_MAX_MESH_COLLIDER_TRIS = new("max_mesh_collider_tris", "Maximum total number of triangles of automatically generated from mesh colliders allowed.", () => 50000); // slider 0 250000 0 // The max total number of collision tris loaded from automatically generated static mesh colliders.
    // ENGINE
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> KEY_AUDIO_PITCH = new("audio_pitch", "The audio pitch of the game sounds. You can use this to fine tune the Engine Sounds.", () => 1.3f); // slider 0f, 1f, 2
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
        
        // TODO: sm64.dll needs to be either in unity's plugin folder, or game main dir.
        
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

    [HarmonyPatch(typeof(World), nameof(World.Destroy))]
    class WorldCleanupPatch
    {
        public static void Prefix(World __instance)
        {
            if (SM64Context._instance?.World == __instance)
            {
                SM64Context._instance.DestroyInstance();
            }
        }
    }

    [HarmonyPatch(typeof(UpdateManager), nameof(UpdateManager.RunUpdates))]
    class WorldUpdatePatch
    {
        public static void Prefix(UpdateManager __instance)
        {
            if (SM64Context._instance?.World == __instance.World)
            {
                SM64Context._instance.OnCommonUpdate();
            }
        }
    }

    // TODO: figure out a physical synchronizable representation of mario.
    [HarmonyPatch(typeof(Slot), nameof(Slot.BuildInspectorUI))]
    class SlotUIAddon
    {
        public static void Postfix(Slot __instance, UIBuilder ui)
        {
            var btn = ui.Button("Spawn Mario64!");
            btn.LocalPressed += (b,e)=> {
                if (SM64Context.EnsureInstanceExists(__instance.World))
                {
                    var mar = __instance.World.AddSlot($"{__instance.LocalUser.UserName}'s Mario");
                    mar.GlobalPosition = __instance.GlobalPosition;
                    SM64Context._instance.AddMario(mar);
                }
                else
                {
                    btn.LabelText = "Failed to spawn mario!";
                }
            };
            ui.Button("Rebuild Static Surfaces").LocalPressed += (b,e)=> {
                if (SM64Context._instance != null)
                {
                    SM64Context.QueueStaticSurfacesUpdate();
                }
            };
            ui.Button("Destroy Mario64 Context").LocalPressed += (b,e)=> {
                if (SM64Context._instance != null)
                {
                    SM64Context._instance.DestroyInstance();
                }
            };
        }
    }
}
