using System.Collections.Generic;
using FrooxEngine;
using ResoniteMario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Components.Context;

public partial class SM64Context
{
    public static Slot TempSlot { get; set; }

    public static Slot GetTempSlot(World world)
    {
        World currentWorld = world ?? Engine.Current.WorldManager.FocusedWorld;
        if (TempSlot is { IsDestroyed: false } && TempSlot.World == currentWorld) return TempSlot;

        if (currentWorld == null)
        {
            Logger.Msg("Current world is null, returning null");
            return null;
        }

        Slot newSlot = currentWorld.RootSlot?.FindChildOrAdd(TempSlotName, false);
        if (newSlot == null)
        {
            Logger.Msg($"Could not find or add temp slot '{TempSlotName}'");
            return null;
        }

        if (TempSlot != null)
        {
            TempSlot.ChildAdded -= HandleSlotAdded;
            Logger.Msg("Removed ChildAdded event from previous tempSlot");
        }

        TempSlot = newSlot;
        TempSlot.ChildAdded += HandleSlotAdded;
        Logger.Msg($"New tempSlot set and ChildAdded event subscribed: {TempSlot.Name} ({TempSlot.ReferenceID})");

        return TempSlot;
    }

    public static bool EnsureInstanceExists(World world, out SM64Context instance)
    {
        // We don't have Instancing for LibSM64, so we can't have multiple instances for separate worlds.
        if (Instance != null && world != Instance.World)
        {
            bool destroy = world.Focus == World.WorldFocus.Focused;
            Logger.Error($"Tried to create instance while one already exists. Replace? - {destroy}");
            if (destroy)
            {
                Logger.Msg("Disposing existing instance");
                Instance.Dispose();
                Instance = null;
            }
            else
            {
                instance = Instance;
                return false;
            }
        }

        if (Instance != null)
        {
            instance = Instance;
            return true;
        }

        Logger.Msg($"Ensuring SM64Context instance exists for world: {world.Name}");
        Instance = new SM64Context(world);
        Logger.Msg("Instance Created!");

        instance = Instance;
        return true;
    }

    public static bool TryAddMario(Slot slot, bool manual = true) => AddMario(slot, manual) != null;

    private static SM64Mario AddMario(Slot slot, bool manual = true)
    {
        bool success = EnsureInstanceExists(slot.World, out SM64Context instance);
        if (!success)
        {
            Logger.Error($"Failed to ensure SM64Context instance for world: {slot.World?.Name}");
            return null;
        }

        bool hasMaxMarios = instance.WorldVariableSpace.TryReadValue("MaxMarios", out int maxMarios);
        if (hasMaxMarios && maxMarios > 0)
        {
            if (instance.MyMarios.Count >= maxMarios)
            {
                Logger.Error("You have too many marios for this world!");
                slot.RunSynchronously(slot.Destroy);
                return null;
            }
        }

        SM64Mario mario = null;
        if (!instance.AllMarios.ContainsKey(slot))
        {
            Logger.Msg($"Adding Mario for Slot: {slot.Name} ({slot.ReferenceID})");

            mario = new SM64Mario(slot, instance);
            instance.AllMarios.Add(slot, mario);
            if (ResoniteMario64.Config.GetValue(ResoniteMario64.KeyPlayRandomMusic))
            {
                Interop.PlayRandomMusic();
            }

            Logger.Msg($"Added mario for Slot: {slot.Name} ({slot.ReferenceID})");
        }

        if (!manual) return mario;

        Slot containerSlot = instance.MarioContainersSlot;
        if (containerSlot != null)
        {
            containerSlot.RunInUpdates(3, () =>
            {
                foreach (Slot child1 in containerSlot.Children.GetTempList())
                {
                    foreach (Slot child2 in child1.Children.GetTempList())
                    {
                        if (child2.Tag != MarioTag) continue;
                        if (instance.AllMarios.ContainsKey(child2)) continue;

                        Logger.Msg($"Adding existing Mario for Slot: {child2.Name} ({child2.ReferenceID})");

                        SM64Mario mario2 = new SM64Mario(child2, instance);
                        instance.AllMarios.Add(child2, mario2);

                        Logger.Msg($"Added existing Mario for Slot: {child2.Name} ({child2.ReferenceID})");
                    }
                }

                instance.ReloadAllColliders(false);
                Logger.Msg("Reloaded all colliders after adding existing Marios");
            });
        }
        else
        {
            Logger.Msg("MarioContainersSlot is null, skipping adding existing Marios");
        }

        return mario;
    }

    public static void RemoveMario(SM64Mario mario)
    {
        Logger.Msg($"Removing Mario with ID {mario.MarioId}");

        mario.Context.AllMarios.Remove(mario.MarioSlot);
        mario.Context.UpdatePlayerMariosState();

        if (mario.Context.AllMarios.Count == 0)
        {
            Logger.Msg("No more Marios left, stopping music");
            Interop.StopMusic();
        }
    }
}