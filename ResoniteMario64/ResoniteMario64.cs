using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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

    // CONTROLS
    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> ControlsSeparator = new ModConfigurationKey<dummy>("----------- Control Settings -----------", "----------- Control Settings -----------", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyUseGamepad = new ModConfigurationKey<bool>("use_gamepad", "Whether to use gamepads for input or not.", () => false);

    // AUDIO
    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> Spacer1 = new ModConfigurationKey<dummy>(" ", " ", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> AudioSeparator = new ModConfigurationKey<dummy>("----------- Audio Settings -----------", "----------- Audio Settings -----------", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyDisableAudio = new ModConfigurationKey<bool>("disable_audio", "Whether to disable all Super Mario 64 Music/Sounds or not.", () => false);

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyPlayRandomMusic = new ModConfigurationKey<bool>("play_random_music", "Whether to play a random music when a mario joins or not.", () => true);

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyPlayCapMusic = new ModConfigurationKey<bool>("play_cap_music", "Whether to play the Cap music when a mario picks one up or not.", () => true);

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<float> KeyAudioVolume = new ModConfigurationKey<float>("audio_volume", "The audio volume.", () => 0.1f); // slider 0f, 1f, 3 (whatever 3 means in BKTUILib.AddSlider) edit: 3 means probably 3 decimal places

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyLocalAudio = new ModConfigurationKey<bool>("local_audio", "Whether to play the Audio Locally or not.", () => true);

    // PERFORMANCE
    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> Spacer2 = new ModConfigurationKey<dummy>("  ", "  ", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> PerformanceSeparator = new ModConfigurationKey<dummy>("----------- Performance Settings -----------", "----------- Performance Settings -----------", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyDeleteAfterDeath = new ModConfigurationKey<bool>("delete_after_death", "Whether to automatically delete our marios after 15 seconds of being dead or not.", () => true);

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<float> KeyMarioCullDistance = new ModConfigurationKey<float>("mario_cull_distance", "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios. -- UNUSED", () => 5f); // slider 0f, 50f, 2 // The max distance that we're going to calculate the mario animations for other people.

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<int> KeyMaxMariosPerPerson = new ModConfigurationKey<int>("max_marios_per_person", "Max number of Marios per player that will be animated using the Super Mario 64 Engine. -- UNUSED", () => 5); // slider 0, 20, 0 (still dk what the last arg means)

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<int> KeyMaxMeshColliderTris = new ModConfigurationKey<int>("max_mesh_collider_tris", "Maximum total number of triangles of automatically generated from mesh colliders allowed.", () => 50000); // slider 0 250000 0 // The max total number of collision tris loaded from automatically generated static mesh colliders.

    // ENGINE
    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> Spacer3 = new ModConfigurationKey<dummy>("   ", "   ", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> EngineSeparator = new ModConfigurationKey<dummy>("----------- Engine Settings -----------", "----------- Engine Settings -----------", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<int> KeyGameTickMs = new ModConfigurationKey<int>("game_tick_ms", "How many Milliseconds should a game tick last. This will directly impact the speed of Mario's behavior.", () => 25); // slider 1, 100, 0

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<int> KeyMarioScaleFactor = new ModConfigurationKey<int>("mario_scale_factor", "The base scaling factor used to size Mario and his colliders. Lower values make Mario larger; higher values make him smaller.", () => 1000); // slider 1, 100, 0

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<Uri> KeyMarioUrl = new ModConfigurationKey<Uri>("mario_url", "The URL for the Non-Modded Renderer for Mario - Null = Default Mario", () => null);

#if DEBUG
    // DEBUG
    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> Spacer4 = new ModConfigurationKey<dummy>("    ", "    ", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> DebugSeparator = new ModConfigurationKey<dummy>("----------- Debug Settings -----------", "----------- Debug Settings -----------", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyRenderSlotLocal = new ModConfigurationKey<bool>("render_slot_local", "Whether the Renderer should be Local or not.", () => true);

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyLogColliderChanges = new ModConfigurationKey<bool>("log_collider_changes", "Whether to Log Collider changes or not.", () => false);
#endif

    public static ModConfiguration Config;
    internal static byte[] SuperMario64UsZ64RomBytes;

    // Internal
    internal static bool FilesLoaded;
    public override string Name => "ResoniteMario64";
    public override string Author => "art0007i, NepuShiro";
    public override string Version => "1.0.0";
    public override string Link => "https://github.com/art0007i/ResoniteMario64/";

    public override void OnEngineInit()
    {
        Config = GetConfiguration();

        // Extract the native binary to the correct Resonite folder
#if IsNet9
        const string dllName = "sm64.dll";
#else
        const string dllName = @"Resonite_Data\Plugins\x86_64\sm64.dll";
#endif
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

    [HarmonyPatch(typeof(World), "StartRunning")]
    public class WorldStartRunningPatch
    {
        public static void Postfix(World __instance)
        {
            if (__instance.IsUserspace()) return;

            __instance.RunInUpdates(1, () =>
            {
                Slot contextSlot = SM64Context.GetTempSlot(__instance).FindChild(x => x.Tag == ContextTag);
                if (contextSlot == null) return;

                if (SM64Context.EnsureInstanceExists(__instance, out SM64Context context))
                {
                    context.MarioContainersSlot.ForeachChild(slot =>
                    {
                        if (slot.Tag == MarioTag)
                        {
                            SM64Context.TryAddMario(slot);
                        }
                    });
                }
            });
        }
    }

    // TODO: Add more buttons here, and figure out a way to sync some of them
    [HarmonyPatch(typeof(Button), "RunPressed")]
    private class RunButtonPressed
    {
        public static bool Prefix(Button __instance)
        {
            if (__instance.Slot.Tag == "SpawnMario")
            {
                __instance.RunSynchronously(() =>
                {
                    Slot root = __instance.World.RootSlot.FindChild(x => x.Name == TempSlotName) ?? __instance.World.RootSlot.AddSlot(TempSlotName, false);

                    Slot mario = root.AddSlot($"{__instance.LocalUser.UserName}'s Mario", false);
                    mario.GlobalPosition = __instance.Slot.GlobalPosition;

                    SM64Context.TryAddMario(mario);
                });

                return false;
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

                return false;
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
            __instance.RunInUpdates(3, () =>
            {
                if (__instance.ActiveUser != __instance.LocalUser) return;

                DynamicValueVariable<bool> variable = __instance.Slot.AttachComponent<DynamicValueVariable<bool>>();
                variable.VariableName.Value = VariableName;
            });
        }

        [HarmonyPatch("OnCommonUpdate"), HarmonyPostfix]
        public static void CommonUpdatePatch(UserRoot __instance)
        {
            if (__instance.ActiveUser != __instance.LocalUser) return;

            __instance.Slot.WriteDynamicVariable(VariableName, SM64Context.Instance != null);
        }
    }

    // TODO: Either add a config to make these debug only, or remove them entirely for physical buttons
    [HarmonyPatch(typeof(Slot), nameof(Slot.BuildInspectorUI))]
    private class SlotUiAddon
    {
        public static void Postfix(Slot __instance, UIBuilder ui)
        {
            ui.Button("Spawn Mario").LocalPressed += (b, e) =>
            {
                b.RunSynchronously(() =>
                {
                    Slot root = __instance.World.RootSlot.FindChild(x => x.Name == TempSlotName) ?? __instance.World.RootSlot.AddSlot(TempSlotName, false);

                    Slot mario = root.AddSlot($"{__instance.LocalUser.UserName}'s Mario", false);
                    mario.GlobalPosition = __instance.GlobalPosition;

                    b.LabelText = SM64Context.TryAddMario(mario) ? "Mario Spawned!" : "Mario Spawn Failed!";

                    b.RunInSeconds(5, () => b.LabelText = "Spawn Mario");
                });
            };
            ui.Button("Reload All Colliders").LocalPressed += (b, e) =>
            {
                b.RunSynchronously(() =>
                {
                    if (SM64Context.Instance != null)
                    {
                        SM64Context.Instance.ReloadAllColliders();
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
                if (inspector?.ComponentView?.Target?.Tag == MarioTag && SM64Context.Instance?.AllMarios.TryGetValue(inspector?.ComponentView?.Target, out SM64Mario mario) is true)
                {
                    if (mario.IsLocal)
                    {
                        ui.Spacer(8);

                        foreach (MarioCapType capType in Enum.GetValues(typeof(MarioCapType)))
                        {
                            ui.Button($"Wear {capType.ToString()}").LocalPressed += (b, e) => { mario.WearCap(capType, capType == MarioCapType.WingCap ? 40f : 15f, !Config.GetValue(KeyDisableAudio)); };
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
                else if (inspector?.ComponentView?.Target?.Tag == AudioTag)
                {
                    ui.Spacer(8);

                    ui.Button("Play Random Music").LocalPressed += (b, e) => { Interop.PlayRandomMusic(); };
                    ui.Button("Stop Music").LocalPressed += (b, e) => { Interop.StopMusic(); };

                    ui.Spacer(8);
                }
            }
            catch (Exception e)
            {
                Error(e);
            }
        }
    }

#if IsNet9
    // This is just here because fuck these logs for now, until Pre-release actually Releases
    [HarmonyPatch(typeof(UniLog))]
    public class UniLogPatch
    {
        private static readonly Regex SuppressRegex = new Regex(@"((?:Uploading|Unloading).*Texture\w*|Failed\s+gather|Failed\s+Load|State:\s*Failed|Received\s+status\s+update\s+that's\s+already\s+expired:)", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

        private static bool ShouldLog(string message) => !SuppressRegex.IsMatch(message);

        [HarmonyPatch(nameof(UniLog.Log), new Type[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        public static bool LogPatch(string message, bool stackTrace) => ShouldLog(message);

        [HarmonyPatch(nameof(UniLog.Warning), new Type[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        public static bool WarningPatch(string message, bool stackTrace) => ShouldLog(message);

        [HarmonyPatch(nameof(UniLog.Error), new Type[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        public static bool ErrorPatch(string message, bool stackTrace) => ShouldLog(message);
    }
#endif
}