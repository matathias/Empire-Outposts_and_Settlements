namespace EmpireVOE;

public static class VOELog
{
    private const string Prefix = "[EmpireVOE]";

    /// <summary>Gates <see cref="Message(ref DebugLogHandler{DebugGate})"/> on this mod's debug setting.</summary>
    public readonly struct DebugGate : IDebugLogGate
    {
        public static bool Enabled => EmpireVOESettings.debugLogging;
    }

    public static void Message(string message)
    {
        if (EmpireVOESettings.debugLogging)
        {
            Log.Message($"{Prefix} {message}");
        }
    }

    /// <summary>
    /// Debug message from an interpolated string. The compiler picks this overload for every
    /// <c>Message($"...")</c> call; when debug logging is off, the string's holes are never evaluated
    /// and nothing is allocated.
    /// </summary>
    public static void Message(ref DebugLogHandler<DebugGate> message)
    {
        if (!message.Enabled) return;
        Log.Message($"{Prefix} {message.ToStringAndClear()}");
    }

    public static void MessageForce(string message)
    {
        Log.Message($"{Prefix} {message}");
    }

    public static void Warning(string message)
    {
        Log.Warning($"{Prefix} {message}");
    }

    public static void Error(string message)
    {
        Log.Error($"{Prefix} {message}");
    }
}
