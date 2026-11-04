using BepInEx.Logging;

namespace Husnisse.Core
{
    /// <summary>Single logging seam so every component reports under the mod's name.</summary>
    public static class Log
    {
        private static ManualLogSource _source;

        public static bool DebugEnabled { get; set; }

        public static void Init(ManualLogSource source)
        {
            _source = source;
        }

        public static void Info(string message)
        {
            _source?.LogInfo(message);
        }

        public static void Warn(string message)
        {
            _source?.LogWarning(message);
        }

        public static void Error(string message)
        {
            _source?.LogError(message);
        }

        public static void Debug(string message)
        {
            if (DebugEnabled) _source?.LogInfo("[debug] " + message);
        }
    }
}
