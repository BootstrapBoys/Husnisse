using System;
using System.Collections.Generic;
using System.Text;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// Editable presets: the named sets behind a category can be replaced from the rules file, and
    /// the replacement is world state like every other routing decision. The server reads the file
    /// on world load and publishes one global key with a value; every peer applies the same table,
    /// so a client's suggestion and the server's routing cannot disagree about what "Metals" means.
    ///
    /// Global keys lower-case their value (the game parses "name value" from one string), and every
    /// Valheim shared name is lower-case already, so the round trip is lossless for what a preset
    /// stores. An unknown category token in the value is ignored rather than fatal: a newer build's
    /// preset must not wipe the others.
    /// </summary>
    public static class PresetStore
    {
        public const string Key = "husnisse_presets";
        public const string Version = "v1";

        private static int _appliedFrame = -1;
        private static string _appliedRaw;

        /// <summary>
        /// Keeps the live table in step with the world's key. Cheap: the parse runs once per frame
        /// at most, and a routing pass asks for a category many times inside one frame.
        /// </summary>
        public static void Ensure()
        {
            if (_appliedFrame == Time.frameCount) return;
            _appliedFrame = Time.frameCount;

            var zone = ZoneSystem.instance;
            if (zone == null) return;

            string raw;
            if (!zone.GetGlobalKey(Key, out raw)) raw = string.Empty;
            if (string.Equals(raw, _appliedRaw, StringComparison.Ordinal)) return;

            _appliedRaw = raw;
            CategoryMap.SetOverrides(Decode(raw));
        }

        /// <summary>Server only: make the file's <c>presets:</c> section the world's table.</summary>
        public static void PublishFromFile()
        {
            Apply(LoadFilePresets("world load"));
        }

        /// <summary>Server only: publish a table (import, or a live edit).</summary>
        public static bool Apply(Dictionary<ItemCategory, List<string>> presets)
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return false;

            var encoded = Encode(presets);
            zone.RemoveGlobalKey(Key);
            if (encoded.Length > 0) zone.SetGlobalKey(Key + " " + encoded);

            _appliedRaw = null;
            Ensure();

            Log.Info("presets published: " + (encoded.Length == 0 ? "built-in table" : encoded));
            return true;
        }

        private static Dictionary<ItemCategory, List<string>> LoadFilePresets(string why)
        {
            try
            {
                var presets = RulesFile.ReadPresets();
                Log.Info("presets read from " + RulesFile.Path + ": " + (presets == null ? 0 : presets.Count) +
                         " categor(ies) at " + why);
                return presets;
            }
            catch (Exception ex)
            {
                Log.Warn("presets not readable (" + why + "): " + ex.Message);
                return null;
            }
        }

        public static string Encode(Dictionary<ItemCategory, List<string>> presets)
        {
            if (presets == null || presets.Count == 0) return string.Empty;

            var text = new StringBuilder(Version);
            foreach (var category in ContainerRule.Presets)
            {
                List<string> names;
                if (!presets.TryGetValue(category, out names) || names == null || names.Count == 0) continue;

                text.Append(';').Append(ContainerRule.CategoryToken(category)).Append('=');
                var first = true;
                foreach (var raw in names)
                {
                    var name = (raw ?? string.Empty).Trim();
                    if (name.Length == 0) continue;
                    if (name.IndexOf(';') >= 0 || name.IndexOf('=') >= 0 || name.IndexOf(',') >= 0)
                    {
                        Log.Warn("preset name skipped (reserved character): " + name);
                        continue;
                    }
                    if (!first) text.Append(',');
                    text.Append(name);
                    first = false;
                }
            }
            return text.ToString();
        }

        /// <summary>Null when the value is absent or unreadable — the built-in table stays in force.</summary>
        public static Dictionary<ItemCategory, List<string>> Decode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;

            var parts = raw.Split(';');
            if (parts.Length < 2 || !string.Equals(parts[0], Version, StringComparison.Ordinal))
            {
                Log.Debug("Ledger", "presets value not understood: " + raw);
                return null;
            }

            var presets = new Dictionary<ItemCategory, List<string>>();
            for (var i = 1; i < parts.Length; i++)
            {
                var split = parts[i].IndexOf('=');
                if (split <= 0) continue;

                var category = ContainerRule.CategoryFromToken(parts[i].Substring(0, split));
                if (category == ItemCategory.None) continue;

                var names = new List<string>();
                foreach (var name in parts[i].Substring(split + 1).Split(','))
                {
                    var trimmed = name.Trim();
                    if (trimmed.Length > 0) names.Add(trimmed);
                }
                if (names.Count > 0) presets[category] = names;
            }

            return presets.Count > 0 ? presets : null;
        }

        /// <summary>One line for diagnostics and the ledger: which categories are overridden.</summary>
        public static string Describe()
        {
            var overrides = CategoryMap.Overrides;
            if (overrides == null || overrides.Count == 0) return "builtin";

            var text = new StringBuilder();
            foreach (var category in ContainerRule.Presets)
            {
                if (!overrides.ContainsKey(category)) continue;
                if (text.Length > 0) text.Append(',');
                text.Append(ContainerRule.CategoryToken(category));
            }
            return text.Length > 0 ? "file:" + text : "builtin";
        }
    }
}
