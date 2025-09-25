using System;
using System.Collections.Generic;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ResoniteMario64.Mario64.Components;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Mario64;

public class Patches
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
                    Slot contextSlot = SM64Context.GetTempSlot(instance).FindChild(x => x.Tag == ContextTag);
                    if (contextSlot == null)
                    {
                        return;
                    }

                    if (SM64Context.EnsureInstanceExists(instance, out SM64Context context))
                    {
                        context.World.RunInUpdates(3, () =>
                        {
                            context.MarioContainersSlot?.ForeachChild(slot =>
                            {
                                if (slot.Tag != MarioTag) return;
                                SM64Context.TryAddMario(slot, false);
                            });
                        });
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

                    foreach (SM64Constants.MarioCapType capType in Enum.GetValues(typeof(SM64Constants.MarioCapType)))
                    {
                        ui.Button($"Wear {capType.ToString()}").LocalPressed += (_, _) => mario.WearCap(capType, capType == SM64Constants.MarioCapType.WingCap ? 40f : 15f, !Config.DisableAudio.Value);
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
                Logger.Error(e);
            }
        }
    }
}