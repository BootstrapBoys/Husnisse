using System;
using System.Collections.Generic;
using Husnisse.Claims;
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
        private const string Usage =
            "usage: husnisse_rule list | net <prefab> <on|off> [index] | netall <prefab> <on|off> | " +
            "blacklist <prefab> <on|off> [index] | set <prefab> <rule> [index] | " +
            "prio <prefab> <n> [index] | suggest <prefab> [index] | copy <prefab> <index> [radius]";

        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_rule",
                "Container network: list | net <prefab> <on|off> [index] | netall <prefab> <on|off> | " +
                "blacklist <prefab> <on|off> [index] | set <prefab> <rule> [index] | " +
                "prio <prefab> <n> [index] | suggest <prefab> [index] | copy <prefab> <index> [radius].",
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

            if (sub == "set" || sub == "net" || sub == "prio" || sub == "blacklist")
            {
                Write(args, parts, sub);
                return;
            }

            if (sub == "netall")
            {
                NetAll(args, parts);
                return;
            }

            if (sub == "suggest")
            {
                Suggest(args, parts);
                return;
            }

            if (sub == "copy")
            {
                Copy(args, parts);
                return;
            }

            args.Context?.AddString(Usage);
        }

        /// <summary>
        /// Joins (or leaves) every container of one prefab in one call. A base with two hundred chests
        /// is otherwise two hundred console commands, which is what the 200-container benchmark and
        /// an admin repairing a world both need.
        /// </summary>
        private static void NetAll(Terminal.ConsoleEventArgs args, string[] parts)
        {
            if (parts.Length < 4)
            {
                args.Context?.AddString(Usage);
                return;
            }

            var prefab = parts[2];
            var on = parts[3] == "on" || parts[3] == "true" || parts[3] == "1";

            var written = 0;
            foreach (var container in ByPrefab(prefab))
            {
                if (ContainerNetwork.WriteNetworkAndSuggest(container, on, "console")) written++;
            }

            var line = "husnisse netall " + prefab + " " + (on ? "on" : "off") + " written=" + written;
            args.Context?.AddString(line);
            Core.DiagnosticsFile.Line(line);
        }

        private static void Write(Terminal.ConsoleEventArgs args, string[] parts, string sub)
        {
            if (parts.Length < 4)
            {
                args.Context?.AddString(Usage);
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

            if (sub == "blacklist")
            {
                var blacklisted = value == "on" || value == "true" || value == "1";
                ContainerNetwork.WriteBlacklist(container, blacklisted, "console");
            }
            else if (sub == "net")
            {
                // The console path is a first toggle like any other, so it offers the same
                // suggestion the button does: one behaviour, whichever way the container joins.
                var on = value == "on" || value == "true" || value == "1";
                ContainerNetwork.WriteNetworkAndSuggest(container, on, "console");
            }
            else if (sub == "prio")
            {
                int priority;
                if (!int.TryParse(value, out priority))
                {
                    args.Context?.AddString("husnisse_rule: priority not a number: " + value);
                    return;
                }

                var rule = ContainerNetwork.RuleOf(container);
                if (rule.IsNone)
                {
                    args.Context?.AddString("husnisse_rule: " + prefab + "[" + index + "] has no rule to prioritise");
                    return;
                }

                rule.Priority = priority;
                ContainerNetwork.WriteRule(container, rule, "console");
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

            // Releasing a container whose class still ships blacklisted changes nothing: say so
            // rather than leave a flag that reads as if it worked.
            if (sub == "blacklist" && ContainerNetwork.IsBlacklisted(container))
            {
                var hint = "husnisse rule " + prefab + "[" + index + "] still blacklisted: its prefab " +
                           "class is skipped by 4. Storage / Skip Carts and Ships or Skip Display Piles";
                args.Context?.AddString(hint);
                Core.DiagnosticsFile.Line(hint);
            }
        }

        /// <summary>
        /// What the first toggle would write, and why: the preset most of the contents belong to, per
        /// stack, with the count. Read-only — a suggestion the player has not asked for is not written.
        /// </summary>
        private static void Suggest(Terminal.ConsoleEventArgs args, string[] parts)
        {
            if (parts.Length < 3)
            {
                args.Context?.AddString(Usage);
                return;
            }

            var prefab = parts[2];
            var index = 0;
            if (parts.Length > 3) int.TryParse(parts[3], out index);

            var container = Find(prefab, index);
            if (container == null)
            {
                args.Context?.AddString("husnisse_rule: no container " + prefab + "[" + index + "]");
                return;
            }

            var suggestion = ContainerNetwork.Suggestion(container);
            var counts = Counts(container);
            var line = "husnisse suggest " + prefab + "[" + index + "] " +
                       (suggestion.IsNone ? "none" : suggestion.Describe()) + " " + counts;
            args.Context?.AddString(line);
            Core.DiagnosticsFile.Line(line);
        }

        /// <summary>Stacks per preset in one container, so a suggestion that seems wrong can be read.</summary>
        private static string Counts(Container container)
        {
            var counts = new Dictionary<ItemCategory, int>();
            var inventory = container.GetInventory();
            if (inventory != null)
            {
                for (var i = 0; i < inventory.NrOfItems(); i++)
                {
                    var item = inventory.GetItem(i);
                    var category = CategoryMap.Of(item);
                    if (category == ItemCategory.None) continue;
                    int count;
                    counts.TryGetValue(category, out count);
                    counts[category] = count + 1;
                }
            }

            var text = "stacks=" + (inventory != null ? inventory.NrOfItems() : 0);
            foreach (var pair in counts) text += " " + ContainerRule.CategoryToken(pair.Key) + "=" + pair.Value;
            return text;
        }

        private static void Copy(Terminal.ConsoleEventArgs args, string[] parts)
        {
            if (parts.Length < 4)
            {
                args.Context?.AddString(Usage);
                return;
            }

            var prefab = parts[2];
            var index = 0;
            int.TryParse(parts[3], out index);
            var radius = RuleCopy.DefaultRadius;
            if (parts.Length > 4) float.TryParse(parts[4], out radius);

            var container = Find(prefab, index);
            if (container == null)
            {
                args.Context?.AddString("husnisse_rule: no container " + prefab + "[" + index + "]");
                return;
            }

            var claim = ClaimRegistry.FindFor(container.transform.position);
            if (claim == null)
            {
                args.Context?.AddString("husnisse_rule: " + prefab + "[" + index + "] is outside every claim");
                return;
            }

            var targets = RuleCopy.Targets(claim, container, radius).Count;
            var changed = RuleCopy.Copy(claim, container, radius, "console");
            var line = "husnisse copy " + prefab + "[" + index + "] targets=" + targets + " written=" + changed;
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
