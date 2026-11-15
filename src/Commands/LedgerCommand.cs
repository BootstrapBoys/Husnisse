using System;
using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Ledger;
using Husnisse.Storage;
using Husnisse.UI;
using UnityEngine;

namespace Husnisse.Commands
{
    /// <summary>
    /// `husnisse_ledger` — the ledger without a mouse. It prints the same model the panel draws and
    /// its writes go through <see cref="LedgerActions"/>, the layer every ledger button calls, so a
    /// headless scenario proves the edit path rather than a copy of it.
    ///
    /// No local player (a dedicated server console) means the server itself is writing: the same
    /// rule `husnisse_rule` has always had.
    /// </summary>
    public static class LedgerCommand
    {
        private const string Usage =
            "usage: husnisse_ledger [list|unknown] | net <prefab> <on|off> [index] | " +
            "rule <prefab> <rule> [index] | classify <prefab> <category> [index] | " +
            "blacklist <prefab> <on|off> [index] | priority <prefab> <n> [index] | " +
            "pickup <category|all> <on|off> | tidy <prefab> [index] | range | teach <on|off> | open";

        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_ledger",
                "The claim ledger: list current rules, classify an unknown chest, manage the blacklist " +
                "and the pickup filter. With no arguments it prints the ledger and opens the panel.",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var parts = args.Args;
            var sub = parts.Length > 1 ? parts[1].ToLowerInvariant() : "list";

            try
            {
                switch (sub)
                {
                    case "list":
                        List(args, false);
                        OpenPanel();
                        return;
                    case "unknown":
                        List(args, true);
                        return;
                    case "open":
                        OpenPanel();
                        return;
                    case "range":
                        Range(args);
                        return;
                    case "pickup":
                        Pickup(args, parts);
                        return;
                    case "teach":
                        TeachMode(args, parts);
                        return;
                    case "net":
                    case "rule":
                    case "classify":
                    case "blacklist":
                    case "priority":
                    case "tidy":
                        Write(args, parts, sub);
                        return;
                    default:
                        args.Context?.AddString(Usage);
                        return;
                }
            }
            catch (Exception ex)
            {
                var line = "husnisse_ledger: " + ex.Message;
                args.Context?.AddString(line);
                Core.DiagnosticsFile.Line(line);
            }
        }

        private static void OpenPanel()
        {
            if (Player.m_localPlayer == null) return;
            if (!InventoryGui.IsVisible()) InventoryGui.instance.Show(null);
            LedgerUi.Toggle(InventoryGui.instance);
        }

        private static void List(Terminal.ConsoleEventArgs args, bool unknownOnly)
        {
            var claims = ClaimRegistry.All();
            if (claims.Count == 0)
            {
                Emit(args, "ledger claims=0");
                return;
            }

            var rows = 0;
            var unknown = 0;
            foreach (var claim in claims)
            {
                if (claim == null || claim.Bowl == null) continue;

                var model = LedgerModel.Rows(claim);
                Emit(args, LedgerModel.Describe(claim));

                foreach (var row in model)
                {
                    rows++;
                    if (row.Unknown) unknown++;
                    if (unknownOnly && !row.Unknown) continue;
                    Emit(args, "ledger row " + row.Index + " " + row.Describe());
                }
            }

            Emit(args, "ledger done claims=" + claims.Count + " rows=" + rows + " unknown=" + unknown);
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
                Emit(args, "ledger: no container " + prefab + "[" + index + "]");
                return;
            }

            var claim = ClaimRegistry.FindFor(container.transform.position);
            if (claim == null)
            {
                Emit(args, "ledger: " + prefab + "[" + index + "] is outside every claim");
                return;
            }

            var row = LedgerModel.RowFor(claim, container);
            if (row == null)
            {
                Emit(args, "ledger: " + prefab + "[" + index + "] is not a container the claim can use");
                return;
            }

            switch (sub)
            {
                case "net":
                    LedgerActions.SetNetwork(claim, row, On(value));
                    break;

                case "blacklist":
                    LedgerActions.SetBlacklist(claim, row, On(value));
                    break;

                case "classify":
                {
                    var category = ContainerRule.CategoryFromToken(value.ToLowerInvariant());
                    if (category == ItemCategory.None)
                    {
                        Emit(args, "ledger: category not understood: " + value);
                        return;
                    }
                    LedgerActions.Classify(claim, row, category);
                    break;
                }

                case "priority":
                {
                    int priority;
                    if (!int.TryParse(value, out priority))
                    {
                        Emit(args, "ledger: priority not a number: " + value);
                        return;
                    }
                    LedgerActions.SetPriority(claim, row, priority);
                    break;
                }

                case "rule":
                {
                    var rule = ContainerRule.ParseShorthand(value);
                    if (rule == null)
                    {
                        Emit(args, "ledger: rule not understood: " + value);
                        return;
                    }
                    LedgerActions.SetRule(claim, row, rule);
                    break;
                }

                case "tidy":
                    if (!LedgerActions.Tidy(claim, row)) Emit(args, "ledger: tidy needs a local player");
                    break;
            }

            Emit(args, "ledger wrote " + prefab + "[" + index + "] " + ContainerNetwork.Describe(container));
            Emit(args, LedgerModel.Describe(claim));
        }

        private static void Pickup(Terminal.ConsoleEventArgs args, string[] parts)
        {
            if (parts.Length < 4)
            {
                args.Context?.AddString(Usage);
                return;
            }

            var token = parts[2].ToLowerInvariant();
            var on = On(parts[3]);
            var claim = ClaimAtPlayer();
            if (claim == null)
            {
                Emit(args, "ledger: no claim here");
                return;
            }

            if (token == "all")
            {
                LedgerActions.SetPickup(claim, on ? PickupFilter.All() : PickupFilter.None());
            }
            else
            {
                var category = ContainerRule.CategoryFromToken(token);
                if (category == ItemCategory.None)
                {
                    Emit(args, "ledger: category not understood: " + token);
                    return;
                }
                LedgerActions.SetPickup(claim, category, on);
            }

            Emit(args, LedgerModel.Describe(claim));
        }

        private static void TeachMode(Terminal.ConsoleEventArgs args, string[] parts)
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                Emit(args, "ledger: teaching needs a local player");
                return;
            }

            var on = parts.Length > 2 ? On(parts[2]) : true;
            var claim = ClaimAtPlayer();
            if (claim == null)
            {
                Emit(args, "ledger: no claim here");
                return;
            }

            if (on) Teaching.Teach.Begin(player, claim);
            else Teaching.Teach.Cancel();

            Emit(args, "ledger teach watching=" + Teaching.Teach.Active);
        }

        private static void Range(Terminal.ConsoleEventArgs args)
        {
            var claim = ClaimAtPlayer();
            if (claim == null)
            {
                Emit(args, "ledger: no claim here");
                return;
            }

            var player = Player.m_localPlayer;
            if (player != null && ZNet.instance != null && !ZNet.instance.IsDedicated())
            {
                ClaimRing.ShowFor(claim.Center, claim.Radius, new Color(0.55f, 0.95f, 0.85f, 0.85f), 8f);
            }

            Emit(args, "ledger range claim=" + claim.BowlId + " r=" + claim.Radius.ToString("0") +
                       " center=" + claim.Center.x.ToString("0.#") + "," + claim.Center.z.ToString("0.#"));
        }

        private static Claim ClaimAtPlayer()
        {
            var player = Player.m_localPlayer;
            if (player != null)
            {
                var claim = ClaimRegistry.FindFor(player.transform.position);
                if (claim != null) return claim;
            }

            var all = ClaimRegistry.All();
            return all.Count > 0 ? all[0] : null;
        }

        private static Container Find(string prefab, int index)
        {
            var matches = LedgerModel.Containers(prefab);
            return index >= 0 && index < matches.Count ? matches[index] : null;
        }

        private static bool On(string value)
        {
            return string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   value == "1";
        }

        private static void Emit(Terminal.ConsoleEventArgs args, string line)
        {
            args.Context?.AddString(line);
            Core.DiagnosticsFile.Line(line);
        }
    }
}
