using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using Renderite.Shared;
using static ResoniteMario64.Constants;

namespace ResoniteMario64.Mario64.Components.Context;

public sealed partial class SM64Context
{
    private Comment _inputBlock;

    public float2 Joystick;
    public bool Jump;
    public bool Kick;
    public bool Stomp;

    private bool _movementBlocked = true;

    private void HandleInputs()
    {
        InputInterface inp = World.InputInterface;
        if(!Config.UnlockMovementKeyToggle.Value)
        {
            _movementBlocked = !inp.GetKey(Config.UnlockMovementKey.Value);
        }
        else if (inp.GetKeyUp(Config.UnlockMovementKey.Value))
        {
            _movementBlocked = !_movementBlocked;
        }

        bool shouldRun = !World.LocalUser.HasActiveFocus() && _movementBlocked;
        bool shouldGamepad = Config.UseGamepad.Value && inp.GetDevices<StandardGamepad>().Count != 0;
        if (!shouldGamepad && inp.VR_Active && shouldRun)
        {
            InteractionHandler main = World.LocalUser.GetInteractionHandler(World.LocalUser.Primaryhand);
            InteractionHandler off = main.OtherTool;

            Joystick = off.Controller is IndexController controller ? controller.Joystick.Value : off.Inputs.Axis.CurrentValue;
            Jump = main.SharesUserspaceToggleAndMenus ? main.Inputs.Menu.Held : main.Inputs.UserspaceToggle.Held;
            Stomp = main.Inputs.Grab.Held;
            Kick = main.Inputs.Interact.Held;
        }
        else if (!shouldGamepad && shouldRun)
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
        else if (shouldGamepad)
        {
            float2 accum = float2.Zero;
            bool jump = false;
            bool stomp = false;
            bool kick = false;

            inp.ForEachDevice<StandardGamepad>(d =>
            {
                accum += d.LeftThumbstick.Value;
                jump |= d.A.Held;
                stomp |= d.LeftTrigger.Value > 0.1f;
                kick |= d.X.Held;
            });

            Joystick = MathX.Clamp(accum, -float2.One, float2.One);
            Jump = jump;
            Stomp = stomp;
            Kick = kick;
        }
        else
        {
            Joystick = float2.Zero;
            Jump = false;
            Stomp = false;
            Kick = false;
        }

        if (_inputBlock == null || _inputBlock.IsRemoved)
        {
            Comment block = World.LocalUser.Root?.Slot?.GetComponentOrAttach<Comment>(c => c.Text.Value == InputBlockTag);
            if (block != null)
            {
                block.Text.Value = InputBlockTag;
                _inputBlock = block;
            }
        }

        LocomotionController loco = World.LocalUser.Root?.GetRegisteredComponent<LocomotionController>();
        if (loco == null) return;

        if (AnyControlledMarios && !inp.VR_Active && _movementBlocked && !shouldGamepad)
        {
            Comment currentBlock = loco.SupressSources.OfType<Comment>().FirstOrDefault(c => c.Text.Value == InputBlockTag);
            if (currentBlock == null)
            {
                loco.SupressSources.Add(_inputBlock);
            }
        }
        else
        {
            loco.SupressSources.RemoveAll(_inputBlock);
        }
    }

    private static float2 GetDesktopJoystick(bool up, bool down, bool left, bool right)
    {
        float2 input = float2.Zero;

        if (up) input += new float2(0, 1);
        if (down) input += new float2(0, -1);
        if (left) input += new float2(1);
        if (right) input += new float2(-1);

        float length = MathX.Sqrt(input.x * input.x + input.y * input.y);
        return length > 1.0f
                ? new float2(input.x / length, input.y / length)
                : input;
    }

    private static bool ShouldBlockInputs(InteractionHandler c, Chirality hand) => ShouldBlockInit() && c.Side.Value == hand;
    private static bool ShouldBlockInputs() => ShouldBlockInit() && Config.BlockDashWithMarios.Value;
    private static bool ShouldBlockInit() => Instance?.World != null && Instance.AnyControlledMarios && Instance.World.InputInterface.VR_Active && !Instance.World.LocalUser.HasActiveFocus();
    
    [HarmonyPatch(typeof(UserspaceRadiantDash), nameof(UserspaceRadiantDash.Open), MethodType.Setter)]
    public class DashInputBlocker
    {
        public static void Prefix(ref bool value)
        {
            if (ShouldBlockInputs()) value = false;
        }
    }

    // [HarmonyPatch(typeof(InteractionHandler), "OnInputUpdate")]
    // public class JumpInputBlocker
    // {
    //     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    //     {
    //         List<CodeInstruction> list = new List<CodeInstruction>(instructions);
    //         MethodInfo invoke = typeof(Action<InteractionHandler>).GetMethod("Invoke", new Type[] { typeof(InteractionHandler) });
    //         bool done = false;
    //
    //         for (int i = 0; i < list.Count; i++)
    //         {
    //             if (!done && i >= 3)
    //             {
    //                 if (list[i].opcode == OpCodes.Callvirt && Equals(list[i].operand, invoke))
    //                 {
    //                     if (list[i - 1].opcode == OpCodes.Ldarg_0 &&
    //                         list[i - 2].opcode == OpCodes.Ldfld &&
    //                         list[i - 3].opcode == OpCodes.Ldarg_0)
    //                     {
    //                         Label skip = generator.DefineLabel();
    //                         int after = i + 1;
    //                         if (after < list.Count)
    //                         {
    //                             list[after].labels.Add(skip);
    //                         }
    //
    //                         List<CodeInstruction> inject = new List<CodeInstruction>()
    //                         {
    //                             new CodeInstruction(OpCodes.Ldarg_0),
    //                             new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JumpInputBlocker), nameof(Injection))),
    //                             new CodeInstruction(OpCodes.Brfalse_S, skip)
    //                         };
    //
    //                         list.InsertRange(i - 3, inject);
    //                         i += inject.Count;
    //                         done = true;
    //                     }
    //                 }
    //             }
    //         }
    //
    //         return list;
    //     }
    //
    //     public static bool Injection(InteractionHandler handler)
    //     {
    //         return !ShouldBlockInputs(handler, handler.LocalUser.Primaryhand);
    //     }
    // }

    [HarmonyPatch(typeof(InteractionHandler), nameof(InteractionHandler.BeforeInputUpdate))]
    public class MarioInputBlocker
    {
        public static void Postfix(InteractionHandler __instance)
        {
            if (__instance.Slot.ActiveUser != __instance.LocalUser) return;
            
            bool isIndex = __instance.Controller is IndexController;
            if (ShouldBlockInputs(__instance, __instance.LocalUser.Primaryhand.GetOther()))
            {
                if (isIndex)
                {
                    var module = __instance.LocalUser.Root.GetRegisteredComponent<LocomotionController>()?.ActiveModule;
                    switch (module)
                    {
                        case PhysicalLocomotion phys:
                            if (phys.CharacterController.Gravity == float3.Zero)
                            {
                                phys.CharacterController.AirSpeed.Value = 0f;
                            }
                            else
                            {
                                phys.CharacterController.Speed.Value = 0f;
                            }

                            break;
                        case NoclipLocomotion noclip:
                            noclip.MaxSpeed.Value = 0f;
                            break;
                    }
                }
                else
                {
                    __instance.Inputs.Axis.RegisterBlocks = true;
                }
            }
            else
            {
                if (isIndex)
                {
                    var module = __instance.LocalUser.Root.GetRegisteredComponent<LocomotionController>()?.ActiveModule;
                    var builder = __instance.World.RootSlot.GetComponentInChildren<CommonAvatarBuilder>();
                    switch (module)
                    {
                        case PhysicalLocomotion phys:

                            if (phys.CharacterController.Gravity == float3.Zero && phys.CharacterController.AirSpeed.Value == 0f)
                            {
                                var fly = builder?.LocomotionModules.Target?.GetComponentInChildren<PhysicalLocomotion>(x => x.CharacterController.Gravity == float3.Zero);
                                phys.CharacterController.AirSpeed.Value = fly?.CharacterController.AirSpeed.Value ?? 10f;
                            }
                            else if (phys.CharacterController.Speed.Value == 0f)
                            {
                                var walk = builder?.LocomotionModules.Target?.GetComponentInChildren<PhysicalLocomotion>(x => x.CharacterController.Gravity != float3.Zero);
                                phys.CharacterController.Speed.Value = walk?.CharacterController.Speed.Value ?? 4f;
                            }

                            break;
                        case NoclipLocomotion noclip:
                            if (noclip.MaxSpeed.Value == 0f)
                            {
                                var noclip2 = builder?.LocomotionModules.Target?.GetComponentInChildren<NoclipLocomotion>();
                                noclip.MaxSpeed.Value = noclip2?.MaxSpeed.Value ?? 15f;
                            }
                            break;
                    }
                }
                else
                {
                    __instance.Inputs.Axis.RegisterBlocks = true;
                }
            }
            /*if (ShouldBlockInputs(__instance, __instance.LocalUser.Primaryhand))
            {
                __instance.Inputs.UserspaceToggle.RegisterBlocks = true;
                __instance.Inputs.Interact.RegisterBlocks = true;
                __instance.Inputs.Grab.RegisterBlocks = true;
            }*/
        }
    }

    [HarmonyPatch(typeof(StandardGamepad), nameof(StandardGamepad.Bind))]
    public class GamepadInputBlocker
    {
        public static bool Prefix() => !Config.UseGamepad.Value;
    }
}