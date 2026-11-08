namespace EmpireVOE;

/// <summary>EmpireVOE logging, prefixed <c>[EmpireVOE]</c>; debug messages follow the mod's debug setting.</summary>
public static class VOELog
{
    public readonly struct DebugGate : ISubmodLogGate
    {
        public static string Slug => "[EmpireVOE]";
        public static bool Enabled => EmpireVOESettings.debugLogging;
    }

    public static void Message(string message) => SubmodLog<DebugGate>.Message(message);
    public static void Message(ref DebugLogHandler<DebugGate> message) => SubmodLog<DebugGate>.Message(ref message);
    public static void MessageForce(string message) => SubmodLog<DebugGate>.MessageForce(message);
    public static void Warning(string message) => SubmodLog<DebugGate>.Warning(message);
    public static void Error(string message) => SubmodLog<DebugGate>.Error(message);
}
