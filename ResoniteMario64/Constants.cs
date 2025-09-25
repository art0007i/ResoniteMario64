namespace ResoniteMario64;

public static class Constants
{
    // Slot Names
    public const string TempSlotName = "__TEMP";
    public const string ContextSlotName = "ResoniteMario64 Instance";
    public const string ConfigSlotName = "Config";
    public const string AudioSlotName = "Audio";
    public const string MarioContainersSlotName = "Mario Containers";

    // DynamicVariableSpaceTags
    public const string ContextSpaceName = "SM64Context";
    public const string MarioSpaceName = "Mario";

    // DynamicVariable Val/Ref Names
    // Tags for SM64 Context
    public const string HostVarName = "Host";
    public const string ScaleVarName = "Scale";
    public const string WaterVarName = "WaterLevel";
    public const string GasVarName = "GasLevel";

    // Tags for Mario Instances
    public const string JoystickVarName = "Joystick";
    public const string JumpVarName = "Jump";
    public const string PunchVarName = "Punch";
    public const string CrouchVarName = "Crouch";
    public const string IsShownVarName = "IsShown";
    public const string HealthPointsVarName = "HealthPoints";
    public const string ActionFlagsVarName = "ActionFlags";
    public const string StateFlagsVarName = "StateFlags";

    // Actual Tags
    public const string ContextTag = "SM64 Context";
    public const string MarioTag = "SM64 Mario";
    public const string MarioContainersTag = "SM64 Mario Containers";
    public const string MarioContainerTag = "SM64 Mario Container";
    public const string MarioNonMRendererTag = "SM64 Non-Modded Renderer";
    public const string AudioTag = "SM64 Audio";
    public const string ConfigTag = "SM64 Config";
    public const string InputBlockTag = "SM64 InputBlock";
}