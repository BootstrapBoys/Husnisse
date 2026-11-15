using System;
using System.Collections.Generic;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Commands
{
    /// <summary>
    /// `husnisse_rule` — the server-side way to inspect and write container rules without a client.
    /// The UI writes through the same ContainerNetwork calls; this exists so a harness (and an admin
    /// at the console) can prove storage at scale and repair a world the UI cannot reach.
    /// </summary>
    public static class RuleCommand
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_rule",
                "Container network: list | net <prefab> <on|off> [index] | set <prefab> <rule> [index].",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var parts = args.Args;
            var sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : "list";

            if (sub == "list")
            {
                List(args.Context);
                return;
            }

            if (sub != "net" && sub != "set")
            {
                args.Context?.AddString("usage: husnisse_rule list | net <prefab> <on|off> [index] | set <prefab> <rule> [index]");
                return;
            }

            if (parts.Length < 4)
            {
                args.Context?.AddString("usage: husnisse_rule " + sub + " <prefab> <value> [index]");
                return;
            }

            var prefab = parts[2];
            var value = parts[3];
            var index = 0;
            if (parts.Length > 4) int.TryParse(parts[4], out index);

            var container = Find(prefab, index);
            if (container == null)
            {
                args.Context?.AddString("husnisse_rule: no container " + prefab + "[" + index + "]");
                return;
            }

            if (sub == "net")
            {
                var on = value == "on" || value == "true" || value == "1";
                ContainerNetwork.WriteNetwork(container, on, "console");
            }
            else
            {
                var rule = ContainerRule.ParseShorthand(value);
                if (rule == null)
                {
                    args.Context?.AddString("husnisse_rule: rule not understood: " + value);
                    return;
                }
                ContainerNetwork.WriteRule(container, rule, "console");
            }

            var line = "husnisse rule " + prefab + "[" + index + "] " + ContainerNetwork.Describe(container);
            args.Context?.AddString(line);
            Core.DiagnosticsFile.Line(line);
        }

        private static void List(Terminal context)
        {
            var count = 0;
            foreach (var container in ByPrefab(null))
            {
                var text = "husnisse rule " + Utils.GetPrefabName(container.gameObject) +
                           "[" + count + "] " + ContainerNetwork.Describe(container);
                Core.DiagnosticsFile.Line(text);
                context?.AddString(text);
                count++;
            }

            var line = "husnisse rules listed=" + count;
            Core.DiagnosticsFile.Line(line);
            context?.AddString(line);
        }

        private static Container Find(string prefab, int index)
        {
            var matches = ByPrefab(prefab);
            return index >= 0 && index < matches.Count ? matches[index] : null;
        }

        /// <summary>
        /// Deterministic order by position (x, then z, then y) — the same order the rig harness uses
        /// for `zdo`/`destroy`, so "chest 3" means the same chest in a scenario and at the console.
        /// </summary>
        private static List<Container> ByPrefab(string prefab)
        {
            var matches = new List<Container>();
            foreach (var container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (container == null) continue;
                if (container.GetComponent<Bowl.BowlPiece>() != null) continue;
                if (prefab != null && Utils.GetPrefabName(container.gameObject) != prefab) continue;

                var view = container.GetComponent<ZNetView>();
                if (view == null || view.GetZDO() == null) continue;
                matches.Add(container);
            }

            matches.Sort(delegate(Container a, Container b)
            {
                var first = a.transform.position;
                var second = b.transform.position;
                if (Math.Abs(first.x - second.x) > 0.01f) return first.x < second.x ? -1 : 1;
                if (Math.Abs(first.z - second.z) > 0.01f) return first.z < second.z ? -1 : 1;
                return first.y.CompareTo(second.y);
            });
            return matches;
        }
    }
}
