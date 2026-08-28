using System.Diagnostics;
using System.Runtime.CompilerServices;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteMario64.Mario64.Components;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Mario64;

public static class Patches
{
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
        private static readonly Stopwatch updateTimer = new Stopwatch();
        private static World lastWorld;
        private static bool initialized;

        public static void Prefix(UpdateManager __instance)
        {
            try
            {
                World world = __instance.World;

                SM64Context instance = SM64Context.Instance;
                if (instance != null && instance.World == world)
                {
                    instance.OnCommonUpdate();
                }

                if (world.Focus != World.WorldFocus.Focused)
                    return;

                if (!initialized || lastWorld != world)
                {
                    updateTimer.Reset();
                    updateTimer.Start();
                    initialized = true;
                    lastWorld = world;
                }

                if (updateTimer.Elapsed.TotalSeconds >= 5.0)
                {
                    SM64Context.CheckForInstance(world);
                    updateTimer.Restart();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }

    [HarmonyPatch(typeof(World), MethodType.Constructor, new Type[] { typeof(WorldManager), typeof(bool), typeof(bool) })]
    public class WorldStartRunningPatch
    {
        private static readonly Dictionary<World, Action<World>> Subscriptions = new Dictionary<World, Action<World>>();

        public static void Postfix(World __instance)
        {
            if (__instance.IsUserspace()) return;
            if (Engine.Current?.WorldManager == null) return;

            if (Subscriptions.ContainsKey(__instance)) return;

            Action<World> handler = world => WorldSubMethod(world, __instance);
            Subscriptions[__instance] = handler;

            Engine.Current.WorldManager.WorldFocused += handler;

            __instance.RootSlot.ChildAdded += (_, child) =>
            {
                if (child.Name != TempSlotName) return;

                SM64Context.TempSlot = child;
            };

            __instance.RootSlot.ChildRemoved += (slot, child) =>
            {
                if (child.Name != TempSlotName) return;

                slot.RunInUpdates(slot.LocalUser.AllocationID, () => { SM64Context.TempSlot = slot.FindChildOrAdd(TempSlotName, false); });
            };
        }

        private static void WorldSubMethod(World world, World instance)
        {
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
                    SM64Context.CheckForInstance(instance);
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
            switch (__instance.Slot.Tag)
            {
                case "SpawnMario":
                    __instance.RunSynchronously(() =>
                    {
                        string oldText = __instance.LabelText;
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
                        __instance.RunSynchronously(() => SM64Context.Instance.Dispose());
                    }

                    return false;
                }
                default:
                    return true;
            }
        }
    }

    [HarmonyPatch(typeof(WorkerInspector), nameof(WorkerInspector.BuildInspectorUI))]
    private class SlotUIEnumAddon
    {
        private static ConditionalWeakTable<Worker, Dictionary<string, Text>> _conditionalWeakTable = new ConditionalWeakTable<Worker, Dictionary<string, Text>>();

        public static void Postfix(Worker worker, UIBuilder ui)
        {
            if (worker is Slot slot && slot.GetComponent<Collider>() is { } col)
            {
                if (!_conditionalWeakTable.TryGetValue(worker, out Dictionary<string, Text> texts) || texts == null)
                {
                    texts = new Dictionary<string, Text>(3);
                    _conditionalWeakTable.Add(worker, texts);
                }

                texts.Clear();

                string[] tagParts = slot.Tag?.Split(',');
                Utils.ParseTagParts(tagParts, out SurfaceType surfaceType, out TerrainType terrainType, out InteractableType interactableType, out _, out _);
                ColliderCategory category = Utils.GetColliderCategory(col);

                BuildEnumEditor(ui, slot, "ColliderCategory", category, texts);
                BuildEnumEditor(ui, slot, "SurfaceType", surfaceType, texts);
                BuildEnumEditor(ui, slot, "TerrainType", terrainType, texts);
                BuildEnumEditor(ui, slot, "InteractableType", interactableType, texts);
            }
        }

        private static void BuildEnumEditor<T>(UIBuilder ui, Slot slot, string name, T enumValue, Dictionary<string, Text> texts) where T : Enum
        {
            ui.HorizontalLayout(4f);
            ui.Style.FlexibleWidth = -1f;
            ui.Style.MinWidth = 24f;
            ui.Button((LocaleString)"<<").LocalPressed += (_, _) =>
            {
                if (texts.TryGetValue(name, out var text))
                {
                    DecrementEnum(ref enumValue);
                    text.Content.Value = enumValue.ToString();
                    if (typeof(T) == typeof(ColliderCategory))
                    {
                        SetColliderCategory((ColliderCategory)(object)enumValue, slot);
                        return;
                    }
                    SetSlotTag(enumValue, slot);
                }
            };
            ui.Style.FlexibleWidth = 100f;
            ui.Style.MinWidth = -1f;
            Button button = ui.Button();
            var content = button.Slot.GetComponentInChildren<Text>();
            content.Content.Value = enumValue.ToString();
            texts.Add(name, content);
            ui.Style.FlexibleWidth = -1f;
            ui.Style.MinWidth = 24f;
            ui.Button((LocaleString)">>").LocalPressed += (_, _) =>
            {
                if (texts.TryGetValue(name, out var text))
                {
                    IncrementEnum(ref enumValue);
                    text.Content.Value = enumValue.ToString();
                    if (typeof(T) == typeof(ColliderCategory))
                    {
                        SetColliderCategory((ColliderCategory)(object)enumValue, slot);
                        return;
                    }
                    SetSlotTag(enumValue, slot);
                }
            };
            ui.Style.FlexibleWidth = -1f;
            ui.Style.MinWidth = 24f;
            ui.Button("∅").LocalPressed += (_, _) =>
            {
                if (typeof(T) == typeof(ColliderCategory))
                {
                    RemoveAllColliderCategories(slot);
                    return;
                }
                RemoveSlotTagType<T>(slot);
            };
            ui.NestOut();
        }

        private static void SetSlotTag<T>(T enumValue, Slot slot) where T : Enum
        {
            RemoveSlotTagType<T>(slot);
            AddSlotTag(enumValue, slot);
        }

        private static void AddSlotTag<T>(T enumValue, Slot slot) where T : Enum
        {
            string newTag = $"{typeof(T).Name.Replace("SM64", "")}_{enumValue}";

            List<string> tags = GetSlotTags(slot);

            if (!tags.Contains(newTag, StringComparer.Ordinal))
            {
                tags.Add(newTag);
            }

            slot.Tag = string.Join(",", tags);
        }

        private static void RemoveSlotTagType<T>(Slot slot) where T : Enum
        {
            string prefix = $"{typeof(T).Name.Replace("SM64", "")}_";

            List<string> tags = GetSlotTags(slot);

            tags.RemoveAll(x => x.StartsWith(prefix, StringComparison.Ordinal));

            slot.Tag = string.Join(",", tags);
        }

        private static readonly string[] AllColliderTags =
        {
            "SM64 StaticCollider",
            "SM64 Collider",
            "SM64 DynamicCollider",
            "SM64 Interactable",
            "SM64 WaterBox",
            "SM64 Teleporter"
        };

        private static void SetColliderCategory(ColliderCategory enumValue, Slot slot)
        {
            RemoveAllColliderCategories(slot);
            AddColliderCategory(enumValue, slot);
        }

        private static void AddColliderCategory(ColliderCategory category, Slot slot)
        {
            if (category == ColliderCategory.None)
            {
                return;
            }

            List<string> tags = GetSlotTags(slot);

            string colliderTag = GetColliderTag(category);

            if (!tags.Contains(colliderTag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(colliderTag);
            }

            slot.Tag = string.Join(",", tags);
        }

        private static void RemoveAllColliderCategories(Slot slot)
        {
            List<string> tags = GetSlotTags(slot);

            foreach (string colliderTag in AllColliderTags)
            {
                tags.RemoveAll(x => string.Equals(x, colliderTag, StringComparison.OrdinalIgnoreCase));
            }

            slot.Tag = string.Join(",", tags);
        }

        private static string GetColliderTag(ColliderCategory category)
        {
            return category switch
            {
                ColliderCategory.Static       => "SM64 StaticCollider",
                ColliderCategory.Dynamic      => "SM64 DynamicCollider",
                ColliderCategory.Interactable => "SM64 Interactable",
                ColliderCategory.WaterBox     => "SM64 WaterBox",
                ColliderCategory.Teleporter   => "SM64 Teleporter",
                _                             => string.Empty
            };
        }

        private static List<string> GetSlotTags(Slot slot)
        {
            return string.IsNullOrWhiteSpace(slot.Tag) ? new List<string>() : slot.Tag.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(static x => x.Trim()).ToList();
        }

        private static void DecrementEnum<T>(ref T value) where T : Enum
        {
            value = ShiftEnum(value, -1);
        }

        private static void IncrementEnum<T>(ref T value) where T : Enum
        {
            value = ShiftEnum(value, 1);
        }

        private static T ShiftEnum<T>(T value, int delta) where T : Enum
        {
            return value.ShiftEnum(delta);
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
            if (compView == null) return;

            bool isUnderContext = compView.Tag == ContextTag || compView.FindParent(x => x.Tag == ContextTag) != null;
            if (!isUnderContext) return;

            // ui.Button("Button Label").LocalPressed += (b, _) => { b.RunSynchronously(() => { /* Do things here */ }); };

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
            if (SM64Interop.IsGlobalInit) ui.Button("Reload All Colliders").LocalPressed += (b, _) => b.RunSynchronously(() => SM64Context.Instance?.ReloadAllColliders());
            ui.Button("Destroy Mario64 Context").LocalPressed += (b, _) => b.RunSynchronously(() => SM64Context.Instance?.Dispose());

            if (SM64Context.Instance == null || !SM64Interop.IsGlobalInit) return;

            try
            {
                SM64Context.Instance.AllMarios.TryGetValue(compView, out SM64Mario mario);
                if (mario == null)
                {
                    SM64Context.Instance.AllMarios.TryGetValue(compView.FindParent(x => x.Tag == MarioTag), out mario);
                }

                if (mario != null && mario.IsLocal)
                {
                    ui.Spacer(8);

                    ui.Button("Goto Mario").LocalPressed += (b, _) => { b.RunSynchronously(() => { __instance.LocalUser.Root.Slot.GlobalPosition = mario.MarioSlot.GlobalPosition; }); };

                    ui.Button("Bring Mario").LocalPressed += (b, _) =>
                    {
                        b.RunSynchronously(() =>
                        {
                            __instance.LocalUser.GetPointInFrontOfUser(out float3 point, out floatQ _, float3.Forward, distance: 2f);
                            mario.SetPosition(point);
                        });
                    };

                    ui.Spacer(8);

                    foreach (MarioCapType capType in Enum.GetValues(typeof(MarioCapType)))
                    {
                        ui.Button($"Wear {capType.ToString()}").LocalPressed += (_, _) => mario.WearCap(capType, capType == MarioCapType.WingCap ? 40f : 15f, !Config.DisableAudio.Value);
                    }

                    ui.Spacer(8);

                    ui.Button("Heal Mario").LocalPressed += (_, _) => mario.Heal(1);
                    ui.Button("Add Life").LocalPressed += (_, _) => mario.SyncedLives++;
                    ui.Button("Remove Life").LocalPressed += (_, _) => mario.SyncedLives--;

                    ui.Spacer(8);

                    ui.Button("999 Lives").LocalPressed += (_, _) => mario.SyncedLives = 999;
                    ui.Button("0 Lives").LocalPressed += (_, _) => mario.SyncedLives = 0;

                    ui.Spacer(8);

                    ui.Button("Damage Mario").LocalPressed += (_, _) => mario.TakeDamage(mario.MarioSlot.GlobalPosition, 1);
                    ui.Button("Kill Mario").LocalPressed += (_, _) => mario.SetHealthPoints(0);
                    ui.Button("Nuke Mario").LocalPressed += (_, _) => mario.SetMarioAsNuked(true);

                    ui.Spacer(8);
                }
                else if (compView.Tag == AudioTag)
                {
                    ui.Spacer(8);

                    ui.Button("Play Random Music").LocalPressed += (_, _) => SM64Interop.PlayRandomMusic();
                    ui.Button("Stop Music").LocalPressed += (_, _) => SM64Interop.StopMusic();

                    ui.Spacer(8);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}