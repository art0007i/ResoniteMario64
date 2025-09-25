using System;
using FrooxEngine;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Mario64.Components.Context;

public sealed partial class SM64Context
{
    public static Slot TempSlot { get; set; }

    public static Slot GetTempSlot(World world)
    {
        World currentWorld = world ?? Engine.Current.WorldManager?.FocusedWorld;
        if (TempSlot is { IsDestroyed: false } && TempSlot.World == currentWorld) return TempSlot;

        if (currentWorld == null)
        {
            return null;
        }

        Slot newSlot = currentWorld.RootSlot?.FindChildOrAdd(TempSlotName, false);
        if (newSlot == null)
        {
            return null;
        }

        if (TempSlot != null)
        {
            TempSlot.ChildAdded -= HandleSlotAdded;
        }

        TempSlot = newSlot;
        TempSlot.ChildAdded += HandleSlotAdded;

        return TempSlot;
    }

    public static bool EnsureInstanceExists(World world, out SM64Context instance)
    {
        // We don't have Instancing for LibSM64, so we can't have multiple instances for separate worlds.
        if (Instance != null && world != Instance.World)
        {
            bool destroy = world.Focus == World.WorldFocus.Focused;
            Logger.Info($"Tried to create instance while one already exists. Replace? - {destroy}");
            if (destroy)
            {
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

        instance = Instance;
        return true;
    }

    public static bool TryAddMario(Slot slot, bool manual = true) => AddMario(slot, manual) != null;

    private static SM64Mario AddMario(Slot slot, bool manual = true)
    {
        bool success = EnsureInstanceExists(slot.World, out SM64Context context);
        if (!success)
        {
            Logger.Error($"Failed to ensure SM64Context instance for world: {slot.World?.Name}");
            return null;
        }

        bool hasMaxMarios = context.WorldVariableSpace.TryReadValue("MaxMarios", out int maxMarios);
        if (hasMaxMarios && maxMarios > 0)
        {
            if (context.MyMarios.Count >= maxMarios)
            {
                Logger.Msg("Tried to create mario, but we are at the configured limit!");
                slot.RunSynchronously(slot.Destroy);
                return null;
            }
        }

        SM64Mario mario = null;
        if (!context.AllMarios.ContainsKey(slot))
        {
            mario = new SM64Mario(slot, context);
            context.AllMarios.Add(slot, mario);
            if (Config.PlayRandomMusic.Value)
            {
                Interop.PlayRandomMusic();
            }
            
            if (context.WorldVariableSpace.TryReadValue("SM64Music", out string value) && Enum.TryParse(value, out SM64Constants.MusicSequence music) && !Interop.IsMusicPlaying(music))
            {
                Interop.PlayMusic(music);
            }
        }

        if (!manual) return mario;

        Slot containerSlot = context.MarioContainersSlot;
        if (containerSlot != null)
        {
            containerSlot.RunInUpdates(3, () =>
            {
                foreach (Slot child1 in containerSlot.Children.GetTempList())
                {
                    foreach (Slot child2 in child1.Children.GetTempList())
                    {
                        if (child2.Tag != MarioTag) continue;
                        if (context.AllMarios.ContainsKey(child2)) continue;

                        SM64Mario mario2 = new SM64Mario(child2, context);
                        context.AllMarios.Add(child2, mario2);
                    }
                }

                context.ReloadAllColliders(false);
            });
        }

        return mario;
    }

    public static void RemoveMario(SM64Mario mario)
    {
        mario.Context.AllMarios.Remove(mario.MarioSlot);
        mario.Context.UpdatePlayerMariosState();

        if (mario.Context.AllMarios.Count == 0)
        {
            Interop.StopMusic();
        }
    }
    
    private static void HandleSlotAdded(Slot slot, Slot child)
    {
        if (child.Tag != ContextTag) return;

        child.RunSynchronously(() =>
        {
            if (EnsureInstanceExists(child.World, out SM64Context context))
            {
                context.MarioContainersSlot.ForeachChild(marioSlot =>
                {
                    if (marioSlot.Tag == MarioTag)
                    {
                        TryAddMario(marioSlot, false);
                    }
                });
            }
        });
    }
}