using System.Runtime.CompilerServices;
using ResoniteModLoader;

namespace ResoniteMario64;

public static class Logger
{
    public static void Msg(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string output = $"[{caller}|{line}] {message?.ToString() ?? "null"}";
        ResoniteMod.Msg(output);
    }

    public static void Warn(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string output = $"[{caller}|{line}] {message?.ToString() ?? "null"}";
        ResoniteMod.Warn(output);
    }

    public static void Error(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        string output = $"[{caller}|{line}] {message?.ToString() ?? "null"}";
        ResoniteMod.Error(output);
    }

    public static void Debug(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        if (!Utils.CheckDebug()) return;

        string output = $"[{caller}|{line}] {message?.ToString() ?? "null"}";
        ResoniteMod.Debug(output);
    }
}