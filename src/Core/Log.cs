using BepInEx.Logging;
using Husnisse.Config;

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
            var enabled = HusnisseConfig.DebugLogging != null
                ? HusnisseConfig.DebugLogging.Value
                : DebugEnabled;
            if (enabled) _source?.LogInfo("[debug] " + message);
        }

        /// <summary>Tagged debug line: every module logs under its own name so a filter can isolate it.</summary>
        public static void Debug(string module, string message)
        {
            var enabled = HusnisseConfig.DebugLogging != null
                ? HusnisseConfig.DebugLogging.Value
                : DebugEnabled;
            if (enabled) _source?.LogInfo("[debug][" + module + "] " + message);
        }
    }
}
