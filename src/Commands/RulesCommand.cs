using System;
using Husnisse.Storage;

namespace Husnisse.Commands
{
    /// <summary>
    /// `husnisse_rules` — the rules file. Export writes every rule in the world; plan validates a
    /// file against the live world and prints the mapping; map re-points one entry at another
    /// container; apply writes the validated plan (all of it or, on any validation error, nothing).
    /// Server-side only: a client's copy of the world is the part it has loaded.
    /// </summary>
    public static class RulesCommand
    {
        private const string Usage =
            "usage: husnisse_rules export | plan | map <n> <prefab> [index] | apply | presets | " +
            "preset <category> <names|builtin> | show";

        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_rules",
                "Rule file: export | plan | map <n> <prefab> [index] | apply | presets.",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var parts = args.Args;
            var sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : "plan";

            switch (sub)
            {
                case "export":
                    Export(args);
                    return;
                case "plan":
                    Plan(args);
                    return;
                case "map":
                    Map(args, parts);
                    return;
                case "apply":
                    Apply(args);
                    return;
                case "presets":
                    Presets(args);
                    return;
                case "preset":
                    SetPreset(args, parts);
                    return;
                case "show":
                    Show(args);
                    return;
                default:
                    args.Context?.AddString(Usage);
                    return;
            }
        }

        private static void Export(Terminal.ConsoleEventArgs args)
        {
            var result = RulesFile.Export();
            if (result.Error != null)
            {
                Emit(args, "rules export failed: " + result.Error);
                return;
            }

            Emit(args, "rules export claims=" + result.Claims + " containers=" + result.Containers +
                       " path=" + result.Path);
        }

        private static void Plan(Terminal.ConsoleEventArgs args)
        {
            var plan = RulesFile.Load();
            Emit(args, RulesFile.Describe(plan));

            foreach (var row in plan.Rows) Emit(args, row.Describe());
            foreach (var error in plan.Errors) Emit(args, "rules error " + error);

            Emit(args, "rules plan ok=" + plan.Ok + " unmapped=" + plan.Unmapped);
        }

        private static void Map(Terminal.ConsoleEventArgs args, string[] parts)
        {
            if (parts.Length < 4)
            {
                args.Context?.AddString(Usage);
                return;
            }

            var plan = RulesFile.Pending;
            if (plan == null)
            {
                // A map without a plan is a plan load, then the map.
                plan = RulesFile.Load();
            }

            int index;
            if (!int.TryParse(parts[2], out index))
            {
                Emit(args, "rules map: row number not a number: " + parts[2]);
                return;
            }

            var prefab = parts[3];
            var containerIndex = 0;
            if (parts.Length > 4) int.TryParse(parts[4], out containerIndex);

            string message;
            var ok = RulesFile.Map(plan, index, prefab, containerIndex, out message);
            Emit(args, (ok ? "rules map " : "rules map failed ") + message);
            Emit(args, "rules plan ok=" + plan.Ok + " unmapped=" + plan.Unmapped);
        }

        private static void Apply(Terminal.ConsoleEventArgs args)
        {
            var plan = RulesFile.Pending;
            if (plan == null) plan = RulesFile.Load();

            int applied;
            string error;
            if (!RulesFile.Apply(plan, out applied, out error))
            {
                Emit(args, "rules apply refused: " + error);
                return;
            }

            Emit(args, "rules apply applied=" + applied + " claims=" + plan.Claims.Count +
                       " pickup=" + plan.Pickups.Count);
            RulesFile.ClearPending();
        }

        private static void Presets(Terminal.ConsoleEventArgs args)
        {
            Emit(args, "rules presets " + PresetStore.Describe());
            foreach (var category in ContainerRule.Presets)
            {
                var names = new System.Collections.Generic.List<string>(CategoryMap.NamesOf(category));
                Emit(args, "rules preset " + ContainerRule.CategoryToken(category) +
                           " type=" + (names.Count > 0 ? "named" : "type") +
                           " names=" + names.Count);
            }
        }

        private static void Show(Terminal.ConsoleEventArgs args)
        {
            Emit(args, "rules file " + RulesFile.Path + " exists=" + System.IO.File.Exists(RulesFile.Path));
        }

        /// <summary>
        /// One category's named set, live: the admin's edit without hand-editing the file. `builtin`
        /// removes the override for that category again. Published as world state like every other
        /// preset change, so every peer's routing and suggestions follow it.
        /// </summary>
        private static void SetPreset(Terminal.ConsoleEventArgs args, string[] parts)
        {
            if (parts.Length < 4)
            {
                args.Context?.AddString(Usage);
                return;
            }

            var category = ContainerRule.CategoryFromToken(parts[2].ToLowerInvariant());
            if (category == ItemCategory.None)
            {
                Emit(args, "rules preset: category not understood: " + parts[2]);
                return;
            }

            var presets = new System.Collections.Generic.Dictionary<ItemCategory, System.Collections.Generic.List<string>>();
            var overrides = CategoryMap.Overrides;
            if (overrides != null)
            {
                foreach (var pair in overrides)
                {
                    presets[pair.Key] = new System.Collections.Generic.List<string>(pair.Value);
                }
            }

            if (string.Equals(parts[3], "builtin", StringComparison.OrdinalIgnoreCase))
            {
                presets.Remove(category);
            }
            else
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var name in parts[3].Split(','))
                {
                    var trimmed = name.Trim();
                    if (trimmed.Length > 0) names.Add(trimmed);
                }
                if (names.Count == 0)
                {
                    Emit(args, "rules preset: no names given");
                    return;
                }
                presets[category] = names;
            }

            PresetStore.Apply(presets);
            Emit(args, "rules preset " + ContainerRule.CategoryToken(category) + " " + parts[3] +
                       " presets=" + PresetStore.Describe());
        }

        private static void Emit(Terminal.ConsoleEventArgs args, string line)
        {
            args.Context?.AddString(line);
            Core.DiagnosticsFile.Line(line);
        }
    }
}
