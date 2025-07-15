using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using static ResoniteMario64.Constants;
#if IsNet9
using Renderite.Shared;
#endif

namespace ResoniteMario64.Components.Context;

public sealed partial class SM64Context
{
    public Comment InputBlock;

    public float2 Joystick;
    public bool Jump;
    public bool Kick;
    public bool Stomp;

    public bool MovementBlocked = true;

    private void HandleInputs()
    {
        InputInterface inp = World.InputInterface;
        if (inp.GetKeyUp(Key.Period))
        {
            MovementBlocked = !MovementBlocked;
        }

        bool shouldRun = !World.LocalUser.HasActiveFocus() && MovementBlocked;
        if (inp.VR_Active && shouldRun)
        {
            InteractionHandler main = World.LocalUser.GetInteractionHandler(World.LocalUser.Primaryhand);
            InteractionHandler off = main.OtherTool;

            Joystick = off.Inputs.Axis.CurrentValue;
            Jump = main.SharesUserspaceToggleAndMenus ? main.Inputs.Menu.Held : main.Inputs.UserspaceToggle.Held;
            Stomp = main.Inputs.Grab.Held;
            Kick = main.Inputs.Interact.Held;
        }
        else if (shouldRun)
        {
            bool w = inp.GetKey(Key.W);
            bool s = inp.GetKey(Key.S);
            bool d = inp.GetKey(Key.D);
            bool a = inp.GetKey(Key.A);

            Joystick = GetDesktopJoystick(w, s, d, a);
            Jump = inp.GetKey(Key.Space);
            Stomp = inp.GetKey(Key.Shift);
            Kick = inp.Mouse.LeftButton.Held;
        }

        if (InputBlock == null || InputBlock.IsRemoved)
        {
            Comment block = World.LocalUser.Root.Slot.GetComponentOrAttach<Comment>(c => c.Text.Value == $"SM64 {InputBlockTag}");
            block.Text.Value = $"SM64 {InputBlockTag}";
            InputBlock = block;
        }

        LocomotionController loco = World.LocalUser?.Root?.GetRegisteredComponent<LocomotionController>();
        if (loco != null)
        {
            if (AnyControlledMarios && !inp.VR_Active && MovementBlocked)
            {
                Comment currentBlock = loco.SupressSources.OfType<Comment>().FirstOrDefault(c => c.Text.Value == $"SM64 {InputBlockTag}");
                if (currentBlock == null)
                {
                    loco.SupressSources.Add(InputBlock);
                }
            }
            else
            {
                loco.SupressSources.RemoveAll(InputBlock);
            }
        }
    }

    private float2 GetDesktopJoystick(bool up, bool down, bool left, bool right)
    {
        float vert = up ? 1 : down ? -1 : 0;
        float hori = left ? 1 : right ? -1 : 0;

        float2 input = new float2(hori, vert);

        float length = MathX.Sqrt(input.x * input.x + input.y * input.y);
        return length > 1.0f
                ? new float2(input.x / length, input.y / length)
                : input;
    }

    private static bool ShouldblockInputs(InteractionHandler c, Chirality hand)
        => Instance?.World == c.World &&
           (Instance?.AnyControlledMarios ?? false) &&
           c.InputInterface.VR_Active &&
           c.Side.Value == hand &&
           (Instance?.World?.LocalUser.HasActiveFocus() ?? false) &&
           (Instance?.MovementBlocked ?? false);

    [HarmonyPatch(typeof(InteractionHandler), "OnInputUpdate")]
    public class JumpInputBlocker
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            FieldInfo lookFor = AccessTools.Field(typeof(InteractionHandler), "_blockUserspaceOpen");
            foreach (CodeInstruction code in codes)
            {
                yield return code;
                if (code.LoadsField(lookFor))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, typeof(JumpInputBlocker).GetMethod(nameof(Injection)));
                }
            }
        }

        public static bool Injection(bool b, InteractionHandler c) => ShouldblockInputs(c, c.LocalUser.Primaryhand) || b;
    }

    [HarmonyPatch(typeof(InteractionHandler), nameof(InteractionHandler.BeforeInputUpdate))]
    public class MarioInputBlocker
    {
        public static void Postfix(InteractionHandler __instance)
        {
            if (ShouldblockInputs(__instance, __instance.LocalUser.Primaryhand.GetOther()))
            {
                __instance.Inputs.Axis.RegisterBlocks = true;
            }
            /*if (ShouldblockInputs(__instance, __instance.LocalUser.Primaryhand))
            {
                __instance.Inputs.UserspaceToggle.RegisterBlocks = true;
                __instance.Inputs.Interact.RegisterBlocks = true;
                __instance.Inputs.Grab.RegisterBlocks = true;
            }*/
        }
    }
}