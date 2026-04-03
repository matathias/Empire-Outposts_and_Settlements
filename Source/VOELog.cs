using Verse;

namespace EmpireVOE
{
    public static class VOELog
    {
        private const string Prefix = "[EmpireVOE]";

        public static void Message(string message)
        {
            if (EmpireVOESettings.debugLogging)
            {
                Log.Message($"{Prefix} {message}");
            }
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
}
