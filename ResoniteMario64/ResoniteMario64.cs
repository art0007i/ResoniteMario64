using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteMario64.Components;
using ResoniteMario64.Components.Context;
using ResoniteMario64.libsm64;
using ResoniteModLoader;
using static ResoniteMario64.Constants;
using static ResoniteMario64.libsm64.SM64Constants;

namespace ResoniteMario64;

public class ResoniteMario64 : ResoniteMod
{
    // Rom
    private const string SuperMario64UsZ64RomHashHex = "20b854b239203baf6c961b850a4a51a2"; // MD5 hash
    private const string SuperMario64UsZ64RomName = "baserom.us.z64";

    // AUDIO
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KeyDisableAudio = new ModConfigurationKey<bool>("disable_audio", "Whether to disable all Super Mario 64 Music/Sounds or not.", () => false);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KeyPlayRandomMusic = new ModConfigurationKey<bool>("play_random_music", "Whether to play a random music when a mario joins or not.", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<float> KeyAudioVolume = new ModConfigurationKey<float>("audio_volume", "The audio volume.", () => 0.1f); // slider 0f, 1f, 3 (whatever 3 means in BKTUILib.AddSlider) edit: 3 means probably 3 decimal places

    // PERFORMANCE
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KeyDeleteAfterDeath = new ModConfigurationKey<bool>("delete_after_death", "Whether to automatically delete our marios after 15 seconds of being dead or not.", () => true);
    
    // [AutoRegisterConfigKey]
    // public static ModConfigurationKey<float> KEY_MARIO_CULL_DISTANCE = new("mario_cull_distance", "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios.", () => 5f); // slider 0f, 50f, 2 // The max distance that we're going to calculate the mario animations for other people.
    // [AutoRegisterConfigKey]
    // public static ModConfigurationKey<int> KEY_MAX_MARIOS_PER_PERSON = new("max_marios_per_person", "Max number of Marios per player that will be animated using the Super Mario 64 Engine.", () => 1); // slider 0, 20, 0 (still dk what the last arg means)
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> KeyMaxMeshColliderTris = new ModConfigurationKey<int>("max_mesh_collider_tris", "Maximum total number of triangles of automatically generated from mesh colliders allowed.", () => 50000); // slider 0 250000 0 // The max total number of collision tris loaded from automatically generated static mesh colliders.

    // ENGINE
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> KeyGameTickMs = new ModConfigurationKey<int>("game_tick_ms", "How many Milliseconds should a game tick last. This will directly impact the speed of Mario's behavior.", () => 25); // slider 1, 100, 0

    // Local
    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KeyRenderSlotLocal = new ModConfigurationKey<bool>("render_slot_local", "Whether the Renderer should be Local or not.", () => true);

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<int> KeyMarioScaleFactor = new ModConfigurationKey<int>("mario_scale_factor", "The base scaling factor used to size Mario and his colliders. Lower values make Mario larger; higher values make him smaller.", () => 1000); // slider 1, 100, 0

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<Uri> keyMarioUrl = new ModConfigurationKey<Uri>("mario_url", "The URL for the Non-Modded Renderer for Mario", () => new Uri("resdb:///d85c309f7aa0c909f6b1518c4a74dacc383760c516425bec6617e8ebe8dd50da.brson"));
    
    public static ModConfiguration Config;
    internal static byte[] SuperMario64UsZ64RomBytes;

    // Internal
    internal static bool FilesLoaded;
    public override string Name => "ResoniteMario64";
    public override string Author => "art0007i";
    public override string Version => "1.0.0";

    public override string Link => "https://github.com/art0007i/ResoniteMario64/";

    public override void OnEngineInit()
    {
        Config = GetConfiguration();

        // Extract the native binary to the root Resonite folder
        const string dllName = "sm64.dll";
        try
        {
            Msg($"Copying the sm64.dll to Resonite/{dllName}");
            using var resourceStream = typeof(ResoniteMario64).Assembly.GetManifestResourceStream(dllName);
            using var fileStream = File.Open(dllName, FileMode.Create, FileAccess.Write);
            resourceStream!.CopyTo(fileStream);
        }
        catch (IOException ex)
        {
            Error("Failed to copy native library.");
            Error(ex);
            return;
        }

        // Load the ROM
        try
        {
            string smRomPath = Path.GetFullPath(Path.Combine("rml_libs", SuperMario64UsZ64RomName));
            Msg($"Loading the Super Mario 64 [US] z64 ROM from {smRomPath}...");
            FileInfo smRomFileInfo = new FileInfo(smRomPath);
            if (!smRomFileInfo.Exists)
            {
                Error($"You need to download the Super Mario 64 [US] z64 ROM " +
                      $"(MD5 {SuperMario64UsZ64RomHashHex}), rename the file to {SuperMario64UsZ64RomName} " +
                      $"and save it to the path: {smRomPath}");
                return;
            }

            using MD5 md5 = MD5.Create();
            using FileStream smRomFileSteam = File.OpenRead(smRomPath);
            byte[] smRomFileMd5Hash = md5.ComputeHash(smRomFileSteam);
            string smRomFileMd5HashHex = BitConverter.ToString(smRomFileMd5Hash).Replace("-", "").ToLowerInvariant();

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

    /*[HarmonyPatch(typeof(World), nameof(World.Destroy))]
    private class WorldCleanupPatch
    {
        public static void Prefix(World __instance)
        {
            if (SM64Context.Instance?.World == __instance)
            {
                SM64Context.Instance?.Dispose();
            }
        }
    }*/

    [HarmonyPatch(typeof(UpdateManager), nameof(UpdateManager.RunUpdates))]
    private class WorldUpdatePatch
    {
        public static void Prefix(UpdateManager __instance)
        {
            if (SM64Context.Instance?.World == __instance.World)
            {
                SM64Context.Instance?.OnCommonUpdate();
            }
        }
    }

    [HarmonyPatch(typeof(World), MethodType.Constructor, new Type[] { typeof(WorldManager), typeof(bool), typeof(bool) })]
    private class WorldConstructorPatch
    {
        public static void Postfix(World __instance)
        {
            if (__instance.IsUserspace()) return;

            __instance.RootSlot.ChildAdded += (slot, child) =>
            {
                __instance.RunInUpdates(1, () =>
                {
                    if (child.Tag == MarioTag)
                    {
                        SM64Context.AddMario(child);
                    }
                });
            };
        }
    }
    
    [HarmonyPatch(typeof(Button), "RunPressed")]
    private class RunButtonPressed
    {
        public static bool Prefix(Button __instance)
        {
            if (__instance.Slot.Tag == "SpawnMario")
            {
                Slot mario = __instance.World.AddSlot($"{__instance.LocalUser.UserName}'s Mario", false);
                mario.GlobalPosition = __instance.Slot.GlobalPosition;

                return !SM64Context.TryAddMario(mario);
            }

            if (__instance.Slot.Tag == "KillInstance")
            {
                __instance.RunSynchronously(() =>
                {
                    if (SM64Context.Instance != null)
                    {
                        SM64Context.Instance.Dispose();
                    }
                });
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(UserRoot))]
    private class UserRootPatch
    {
        private const string VariableName = "User/ResoniteMario64.HasInstance";
        [HarmonyPatch("OnStart"), HarmonyPostfix]
        public static void OnStartPatch(UserRoot __instance)
        {
            if (__instance.ActiveUser != __instance.LocalUser) return;
            
            DynamicValueVariable<bool> variable = __instance.Slot.AttachComponent<DynamicValueVariable<bool>>();
            variable.VariableName.Value = VariableName;
        }

        private static bool _previousExist;
        [HarmonyPatch("OnCommonUpdate"), HarmonyPostfix]
        public static void CommonUpdatePatch(UserRoot __instance)
        {
            if (__instance.ActiveUser != __instance.LocalUser) return;
            
            bool value = SM64Context.Instance != null;
            if (value == _previousExist) return;
            
            __instance.Slot.WriteDynamicVariable(VariableName, value);
            _previousExist = value;
        }
    }
    
    [HarmonyPatch(typeof(Slot), nameof(Slot.BuildInspectorUI))]
    private class SlotUiAddon
    {
        public static void Postfix(Slot __instance, UIBuilder ui)
        {
            ui.Button("Spawn Mario").LocalPressed += (b, e) =>
            {
                b.RunSynchronously(() =>
                {
                    Slot mario = __instance.World.AddSlot($"{__instance.LocalUser.UserName}'s Mario", false);
                    mario.GlobalPosition = __instance.GlobalPosition;

                    b.LabelText = SM64Context.TryAddMario(mario) ? "Mario Spawned!" : "Mario Spawn Failed!";

                    b.RunInSeconds(5, () => b.LabelText = "Spawn Mario");
                });
            };
            ui.Button("Rebuild Static Surfaces").LocalPressed += (b, e) =>
            {
                b.RunSynchronously(() =>
                {
                    if (SM64Context.Instance != null)
                    {
                        SM64Context.QueueStaticSurfacesUpdate();
                    }
                });
            };
            ui.Button("Destroy Mario64 Context").LocalPressed += (b, e) =>
            {
                b.RunSynchronously(() =>
                {
                    if (SM64Context.Instance != null)
                    {
                        SM64Context.Instance.Dispose();
                    }
                });
            };

            if (SM64Context.Instance == null) return;
            
            try
            {
                SceneInspector inspector = ui.Root.GetComponentInParents<SceneInspector>();
                if (inspector?.ComponentView?.Target?.Tag == "Mario" && SM64Context.Instance?.Marios.TryGetValue(inspector?.ComponentView?.Target, out SM64Mario mario) is true)
                {
                    if (mario.IsLocal)
                    {
                        ui.Spacer(8);

                        foreach (MarioCapType capType in Enum.GetValues(typeof(MarioCapType)))
                        {
                            ui.Button($"Wear {capType.ToString()}").LocalPressed += (b, e) => { mario.WearCap(capType, capType == MarioCapType.WingCap ? 40f : 15f, true); };
                        }

                        ui.Spacer(8);

                        ui.Button("Heal Mario").LocalPressed += (b, e) => { mario.Heal(1); };

                        ui.Spacer(8);

                        ui.Button("Damage Mario").LocalPressed += (b, e) => { mario.TakeDamage(mario.MarioSlot.GlobalPosition, 1); };
                        ui.Button("Kill Mario").LocalPressed += (b, e) => { mario.SetHealthPoints(0); };
                        ui.Button("Nuke Mario").LocalPressed += (b, e) => { mario.SetMarioAsNuked(true); };

                        ui.Spacer(8);
                    }
                }
                else if (inspector?.ComponentView?.Target?.Tag == $"SM64 {AudioTag}")
                {
                    ui.Button("Play Random Music").LocalPressed += (b, e) => { Interop.PlayRandomMusic(); };
                    ui.Button("Stop Music").LocalPressed += (b, e) => { Interop.StopMusic(); };
                }
            }
            catch (Exception e)
            {
                Error(e);
            }
        }
    }
}