using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace ResoniteMario64;

public static class Logger
{
    private static ManualLogSource Log => Plugin.Log;

    private static string Format(object message, string caller, int line)
    {
        return $"[{caller}|{line}] {message?.ToString() ?? "null"}";
    }

    public static void Info(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        Log.LogInfo(Format(message, caller, line));
    }

    public static void Msg(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        Log.LogMessage(Format(message, caller, line));
    }

    public static void Warn(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        Log.LogWarning(Format(message, caller, line));
    }

    public static void Error(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        Log.LogError(Format(message, caller, line));
    }

    public static void Fatal(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        Log.LogFatal(Format(message, caller, line));
    }

    public static void Debug(object message, [CallerMemberName] string caller = "", [CallerLineNumber] int line = 0)
    {
        Log.LogDebug(Format(message, caller, line));
    }
}