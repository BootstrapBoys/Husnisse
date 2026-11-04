using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Husnisse.Config;

namespace Husnisse.Core
{
    /// <summary>
    /// The BSBLogs diagnostic file, in the same style as the other Bootstrap Boys mods: one file per
    /// plugin under BepInEx/BSBLogs, rewritten at startup so it cannot grow across sessions, with a
    /// header describing the session and a state dump at startup, on demand and at shutdown.
    /// </summary>
    public static class DiagnosticsFile
    {
        private static string _path;
        private static bool _ready;

        public static string Path
        {
            get { return _path; }
        }

        public static void Init(string pluginName, string version)
        {
            try
            {
                var directory = System.IO.Path.Combine(Paths.BepInExRootPath, "BSBLogs");
                Directory.CreateDirectory(directory);

                _path = System.IO.Path.Combine(directory, SafeName(pluginName) + ".log");

                File.WriteAllText(_path,
                    pluginName + " " + version + Environment.NewLine +
                    "started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                    Environment.NewLine);

                _ready = true;
            }
            catch (Exception ex)
            {
                Log.Warn("diagnostics file unavailable: " + ex.Message);
            }
        }

        public static void Header(string text)
        {
            Append(text);
        }

        public static void Dump(string reason)
        {
            if (!HusnisseConfig.WriteDiagnosticsFile.Value) return;

            var lines = new List<string>
            {
                "",
                "== " + reason + " @ " + DateTime.Now.ToString("HH:mm:ss") + " =="
            };
            lines.AddRange(Diagnostics.Lines());
            Append(string.Join(Environment.NewLine, lines.ToArray()));
        }

        public static void Line(string text)
        {
            Append(text);
        }

        private static void Append(string text)
        {
            if (!_ready || string.IsNullOrEmpty(text)) return;

            try
            {
                File.AppendAllText(_path, text + Environment.NewLine);
            }
            catch
            {
                // A diagnostic that throws is worse than a diagnostic that is missing.
                _ready = false;
            }
        }

        private static string SafeName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
