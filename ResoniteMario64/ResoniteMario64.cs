using System;
using System.Collections.Generic;
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
#if IsNet9
using System.Text.RegularExpressions;
#endif

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

    [AutoRegisterConfigKey /*, Range(0, 1, "0.000")*/]
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

    [AutoRegisterConfigKey /*, Range(0, 50)*/]
    public static readonly ModConfigurationKey<float> KeyMarioCullDistance = new ModConfigurationKey<float>("mario_cull_distance", "The distance where it should stop using the Super Mario 64 Engine to handle other players Marios.", () => 15f); // slider 0f, 50f, 2 // The max distance that we're going to calculate the mario animations for other people.

    [AutoRegisterConfigKey /*, Range(0, 50)*/]
    public static readonly ModConfigurationKey<int> KeyMaxMariosPerPerson = new ModConfigurationKey<int>("max_marios_per_person", "Max number of Marios per player that will be animated using the Super Mario 64 Engine.", () => 5); // slider 0, 20, 0 (still dk what the last arg means)

    [AutoRegisterConfigKey /*, Range(0, 250000)*/]
    public static readonly ModConfigurationKey<int> KeyMaxMeshColliderTris = new ModConfigurationKey<int>("max_mesh_collider_tris", "Maximum total number of triangles of automatically generated from mesh colliders allowed.", () => 50000); // slider 0 250000 0 // The max total number of collision tris loaded from automatically generated static mesh colliders.

    // ENGINE
    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> Spacer3 = new ModConfigurationKey<dummy>("   ", "   ", () => new dummy());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> EngineSeparator = new ModConfigurationKey<dummy>("----------- Engine Settings -----------", "----------- Engine Settings -----------", () => new dummy());

    [AutoRegisterConfigKey /*, Range(1, 100)*/]
    public static readonly ModConfigurationKey<int> KeyGameTickMs = new ModConfigurationKey<int>("game_tick_ms", "How many Milliseconds should a game tick last. This will directly impact the speed of Mario's behavior.", () => 25); // slider 1, 100, 0

    [AutoRegisterConfigKey /*, Range(1, 1000)*/]
    public static readonly ModConfigurationKey<int> KeyMarioScaleFactor = new ModConfigurationKey<int>("mario_scale_factor", "The base scaling factor used to size Mario and his colliders. Lower values make Mario larger; higher values make him smaller.", () => 200); // slider 1, 100, 0

    [AutoRegisterConfigKey /*, Range(3, 50)*/]
    public static readonly ModConfigurationKey<int> KeyMarioCollisionChecks = new ModConfigurationKey<int>("mario_collision_checks", "The number of evenly spaced points to check along Mario's body for collisions. Higher values increase accuracy but cost more performance.", () => 10); // slider 1, 100, 0

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<Uri> KeyMarioUrl = new ModConfigurationKey<Uri>("mario_url", "The URL for the Non-Modded Renderer for Mario - Null = Default Mario", () => null);

    // DEBUG
    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> Spacer4 = new ModConfigurationKey<dummy>("    ", "    ", () => new dummy(), Utils.CheckDebug());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<dummy> DebugSeparator = new ModConfigurationKey<dummy>("----------- Debug Settings -----------", "----------- Debug Settings -----------", () => new dummy(), Utils.CheckDebug());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyRenderSlotLocal = new ModConfigurationKey<bool>("render_slot_local", "Whether the Renderer should be Local or not.", () => true, Utils.CheckDebug());

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> KeyLogColliderChanges = new ModConfigurationKey<bool>("log_collider_changes", "Whether to Log Collider changes or not.", () => false, Utils.CheckDebug());

    public static ModConfiguration Config;
    internal static byte[] SuperMario64UsZ64RomBytes;
    private static string AssemblyMD5Hash = "";

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
        string dllName = Path.GetFullPath(Path.Combine("sm64.dll"));
#else
        string dllName = Path.GetFullPath(Path.Combine("Resonite_Data", "Plugins", "x86_64", "sm64.dll"));
#endif
        try
        {
            string dllDirectory = Path.GetDirectoryName(dllName);
            if (string.IsNullOrEmpty(dllDirectory) || !Directory.Exists(dllDirectory))
            {
                Error($"Directory not found: {dllDirectory}");
                return;
            }

            if (File.Exists(dllName))
            {
                Logger.Msg("sm64.dll already exists, overwriting.");
            }

            Logger.Msg($"Copying the sm64.dll to {dllName}");

            using var resourceStream = typeof(ResoniteMario64).Assembly.GetManifestResourceStream("sm64.dll");
            if (resourceStream == null)
            {
                Error("Embedded resource sm64.dll not found in assembly.");
                return;
            }

            using var fileStream = File.Open(dllName, FileMode.Create, FileAccess.Write);
            resourceStream.CopyTo(fileStream);
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
            Logger.Msg($"Loading the Super Mario 64 [US] z64 ROM from {smRomPath}...");
            if (!File.Exists(smRomPath))
            {
                Error($"You need to download the Super Mario 64 [US] z64 ROM " +
                      $"(MD5 {SuperMario64UsZ64RomHashHex}), rename the file to {SuperMario64UsZ64RomName} " +
                      $"and save it to the path: {smRomPath}");
                return;
            }

            using MD5 md5 = MD5.Create();
            using FileStream smRomFileStream = File.OpenRead(smRomPath);
            byte[] smRomFileMd5Hash = md5.ComputeHash(smRomFileStream);
            string smRomFileMd5HashHex = BitConverter.ToString(smRomFileMd5Hash).Replace("-", "").ToLowerInvariant();
            // string smRomFileMd5HashHex = Convert.ToHexStringLower(smRomFileMd5Hash);

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

        try
        {
            using MD5 md5 = MD5.Create();
            using FileStream assemblyHash = File.OpenRead(Assembly.GetExecutingAssembly().Location);
            byte[] assemblyMD5Hash = md5.ComputeHash(assemblyHash);
            AssemblyMD5Hash = BitConverter.ToString(assemblyMD5Hash).Replace("-", "").ToLowerInvariant();

            Logger.Msg($"Our Assembly MD5Hash - {AssemblyMD5Hash}");
        }
        catch (Exception ex)
        {
            Error("Failed to compute assembly hash.");
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
    public class WorldStartRunningPatch
    {
        private static readonly Dictionary<World, Action<World>> Subscriptions = new Dictionary<World, Action<World>>();

        public static void Postfix(World __instance)
        {
            Logger.Msg($"World constructor called with AssemblyHash: {AssemblyMD5Hash}");

            if (__instance.IsUserspace()) return;
            if (Engine.Current?.WorldManager == null) return;

            if (Subscriptions.ContainsKey(__instance)) return;

            Action<World> handler = world => WorldSubMethod(world, __instance);
            Subscriptions[__instance] = handler;

            Engine.Current.WorldManager.WorldFocused += handler;
            Logger.Msg($"Subscribed to WorldFocused event for world: {__instance.Name}");

            __instance.RootSlot.ChildAdded += (_, child) =>
            {
                if (child.Name != TempSlotName) return;

                SM64Context.TempSlot = child;
                Logger.Msg("Found Existing TempSlot");
            };

            __instance.RootSlot.ChildRemoved += (slot, child) =>
            {
                if (child.Name != TempSlotName) return;

                slot.RunInUpdates(slot.LocalUser.AllocationID, () =>
                {
                    SM64Context.TempSlot = slot.FindChildOrAdd(TempSlotName, false);
                    Logger.Msg("Remade TempSlot");
                });
            };
        }

        private static void WorldSubMethod(World world, World instance)
        {
            Logger.Msg($"WorldFocused event triggered for world: world ({world.Name} - {world.Focus}), instance ({instance.Name} - {instance.Focus})");

            if (world != instance)
            {
                UnsubscribeWorldFocused(instance);
                return;
            }

            if (world.Focus != World.WorldFocus.Focused) return;

            world.RunInUpdates(3, () =>
            {
                try
                {
                    Logger.Msg("Trying to find TempSlot with ContextSlot");
                    Slot contextSlot = SM64Context.GetTempSlot(instance).FindChild(x => x.Tag == ContextTag);
                    if (contextSlot == null)
                    {
                        Logger.Msg("ContextSlot not found in TempSlot");
                        return;
                    }

                    if (SM64Context.EnsureInstanceExists(instance, out SM64Context context))
                    {
                        Logger.Msg("Ensured SM64Context instance exists");
                        context.World.RunInUpdates(3, () =>
                        {
                            context.MarioContainersSlot?.ForeachChild(slot =>
                            {
                                if (slot.Tag != MarioTag) return;
                                
                                Logger.Msg($"Trying to add Mario slot: {slot.Name} ({slot.ReferenceID})");
                                SM64Context.TryAddMario(slot, false);
                            });
                        });
                    }
                    else
                    {
                        Logger.Msg("Failed to ensure SM64Context instance");
                    }
                }
                finally
                {
                    UnsubscribeWorldFocused(instance);
                }
            });
        }

        private static void UnsubscribeWorldFocused(World world)
        {
            if (Subscriptions.TryGetValue(world, out Action<World> handler))
            {
                Engine.Current.WorldManager.WorldFocused -= handler;
                Subscriptions.Remove(world);
                Logger.Msg($"Unsubscribed from WorldFocused event for world: {world.Name}");
            }
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

    // TODO: Add more buttons here, and figure out a way to sync some of them
    [HarmonyPatch(typeof(Button))]
    private class ButtonPatches
    {
        private static bool _spawnRunning;

        [HarmonyPatch("RunPressed"), HarmonyPrefix]
        public static bool RunPressed(Button __instance)
        {
            string oldText = __instance.LabelText;
            switch (__instance.Slot.Tag)
            {
                case "SpawnMario":
                    __instance.RunSynchronously(() =>
                    {
                        Slot root = __instance.World.RootSlot.FindChild(x => x.Name == TempSlotName) ?? __instance.World.RootSlot.AddSlot(TempSlotName, false);

                        Slot mario = root.AddSlot($"{__instance.LocalUser.UserName}'s Mario", false);
                        mario.GlobalPosition = __instance.Slot.GlobalPosition;

                        __instance.LabelTextField.OverrideForUser(__instance.LocalUser, SM64Context.TryAddMario(mario) ? "Mario Spawned!" : "Mario Spawn Failed!");

                        if (_spawnRunning) return;

                        _spawnRunning = true;
                        __instance.RunInSeconds(5, () =>
                        {
                            __instance.LabelTextField.OverrideForUser(__instance.LocalUser, oldText);
                            _spawnRunning = false;
                        });
                    });

                    return false;
                case "KillInstance":
                {
                    if (SM64Context.Instance != null)
                    {
                        __instance.RunSynchronously(() => { SM64Context.Instance?.Dispose(); });
                    }

                    return false;
                }
                default:
                    return true;
            }
        }
    }

    // TODO: Either add a config to make these debug only, or remove them entirely for physical buttons
    [HarmonyPatch(typeof(Slot), nameof(Slot.BuildInspectorUI))]
    private class SlotUiAddon
    {
        public static void Postfix(Slot __instance, UIBuilder ui)
        {
            SceneInspector inspector = ui.Root.GetComponentInParents<SceneInspector>();
            Slot compView = inspector?.ComponentView?.Target;
            if (compView?.Tag == ContextTag || compView?.FindParent(x => x.Tag == ContextTag) == null) return;

            // ui.Button("Button Label").LocalPressed += (b, _) => { b.RunSynchronously(() => { /* Do things here */ }) };

            ui.Button("Spawn Mario").LocalPressed += (b, _) =>
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
            if (Interop.IsGlobalInit) ui.Button("Reload All Colliders").LocalPressed += (b, _) => b.RunSynchronously(() => SM64Context.Instance?.ReloadAllColliders());
            ui.Button("Destroy Mario64 Context").LocalPressed += (b, _) => b.RunSynchronously(() => SM64Context.Instance?.Dispose());

            if (SM64Context.Instance == null || !Interop.IsGlobalInit) return;

            try
            {
                if ((compView.Tag == MarioTag || compView.FindParent(x => x.Tag == MarioTag) != null) && SM64Context.Instance.AllMarios.TryGetValue(compView, out SM64Mario mario) && mario.IsLocal)
                {
                    ui.Spacer(8);

                    foreach (MarioCapType capType in Enum.GetValues(typeof(MarioCapType)))
                    {
                        ui.Button($"Wear {capType.ToString()}").LocalPressed += (_, _) => mario.WearCap(capType, capType == MarioCapType.WingCap ? 40f : 15f, !Config.GetValue(KeyDisableAudio));
                    }

                    ui.Spacer(8);

                    ui.Button("Heal Mario").LocalPressed += (_, _) => mario.Heal(1);

                    ui.Spacer(8);

                    ui.Button("Damage Mario").LocalPressed += (_, _) => mario.TakeDamage(mario.MarioSlot.GlobalPosition, 1);
                    ui.Button("Kill Mario").LocalPressed += (_, _) => mario.SetHealthPoints(0);
                    ui.Button("Nuke Mario").LocalPressed += (_, _) => mario.SetMarioAsNuked(true);

                    ui.Spacer(8);
                }
                else if (compView.Tag == AudioTag)
                {
                    ui.Spacer(8);

                    ui.Button("Play Random Music").LocalPressed += (_, _) => Interop.PlayRandomMusic();
                    ui.Button("Stop Music").LocalPressed += (_, _) => Interop.StopMusic();

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