using System.Diagnostics.CodeAnalysis;
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
        if (!Config.UnlockMovementKeyToggle.Value)
        {
            _movementBlocked = !inp.GetKey(Config.UnlockMovementKey.Value);
        }
        else if (inp.GetKeyUp(Config.UnlockMovementKey.Value))
        {
            _movementBlocked = !_movementBlocked;
        }

        bool shouldRun = !World.LocalUser.HasActiveFocus() && _movementBlocked || inp.VR_Active;
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
        return length > 1.0f ? new float2(input.x / length, input.y / length) : input;
    }

    private static bool ShouldBlockInputs(InteractionHandler c, Chirality hand) => ShouldBlockInit() && c.Side.Value == hand;
    private static bool ShouldBlockInputs() => ShouldBlockInit() && Config.BlockDashWithMarios.Value;
    private static bool ShouldBlockInit() => Instance?.World != null && Instance.AnyControlledMarios && Instance.World.InputInterface.VR_Active && !Instance.World.LocalUser.HasActiveFocus();

    [HarmonyPatch(typeof(UserspaceRadiantDash), nameof(UserspaceRadiantDash.Open), MethodType.Setter)]
    public class DashInputBlocker
    {
        public static void Prefix(ref bool value)
        {
            if (ShouldBlockInputs() && !(Config.UseGamepad.Value && Engine.Current.InputInterface.GetDevices<StandardGamepad>().Count != 0)) value = false;
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
        private static bool? _lastBlocked;
        private static Slot _cachedLocomotionModules;

        public static void Postfix(InteractionHandler __instance)
        {
            if (__instance.Slot.ActiveUser != __instance.LocalUser) return;

            bool isIndex = __instance.Controller is IndexController;
            if (isIndex && _cachedLocomotionModules?.FilterWorldElement() == null)
            {
                LocomotionController locomotionController = __instance.LocalUser.Root.GetRegisteredComponent<LocomotionController>();
                _cachedLocomotionModules = locomotionController?.ActiveModule?.Slot?.Parent;
            }

            bool blocked = ShouldBlockInputs();

            if (_lastBlocked.HasValue && blocked == _lastBlocked) return;

            _lastBlocked = blocked;

            if (isIndex)
            {
                __instance.RunSynchronously(() => _cachedLocomotionModules?.ActiveSelf = !blocked, true);
            }
            else
            {
                __instance.Inputs.Axis.RegisterBlocks = blocked;
            }

            if (!blocked && !__instance.InputInterface.VR_Active && !(_cachedLocomotionModules?.ActiveSelf ?? false))
            {
                __instance.RunSynchronously(() => _cachedLocomotionModules?.ActiveSelf = true, true);
            }
        }
    }

    [HarmonyPatch(typeof(StandardGamepad), nameof(StandardGamepad.Bind))]
    public class GamepadInputBlocker
    {
        public static bool Prefix()
        {
            if (!Config.UseGamepad.Value) return true;

            Logger.Warn("Blocking StandardGamepad binding because SM64 is using gamepad input.");
            return false;

        }
    }

    [HarmonyPatch(typeof(VR_Manager), "UpdateHaptics")]
    public static class UpdateHapticsPatch
    {
        internal volatile static bool PendingHaptics;

        internal static int MarioId = -1;
        internal static short Level;
        internal static double Time;

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public static void Postfix(Chirality side, ref VR_ControllerOutputState state)
        {
            if (!PendingHaptics) return;

            bool shouldRunMario = PendingHaptics && state.hapticState is { force: 0, pain: 0, temperature: 0, vibration: 0 } || state.hapticState.force == Level && state.hapticState.pain == Level && state.hapticState.temperature == Level && state.hapticState.vibration == Level;
            if (shouldRunMario)
            {
                state.vibrateTime = Time;

                state.hapticState.force = Level;
                state.hapticState.temperature = Level;
                state.hapticState.pain = Level;
                state.hapticState.vibration = Level;

                if (side == Chirality.Right)
                {
                    PendingHaptics = false;
                    MarioId = -1;
                    Level = 0;
                    Time = 0;
                }
            }
        }
    }
    
    public static void VibrateCallback(int marioId, short level, short time)
    {
        if (marioId == -1 || level <= 0 || time <= 0) return;

        SM64Context instance = Instance;
        if (instance == null) return;

        if (!instance.World.InputInterface.ControllerVibrationEnabled) return;
        if (instance.MyMarios.All(x => x.MarioId != marioId)) return;

        float durationSeconds = time * 2 / 1000f;
        if (durationSeconds <= 0) return;

        UpdateHapticsPatch.PendingHaptics = true;
        UpdateHapticsPatch.MarioId = marioId;
        UpdateHapticsPatch.Level = level;
        UpdateHapticsPatch.Time = durationSeconds;
        
        if (Config.DebugEnabled.Value) Plugin.Log.LogDebug($"Got Vibrate Callback: marioId: {marioId}, level: {level}, time: {UpdateHapticsPatch.Time}");
    }
}