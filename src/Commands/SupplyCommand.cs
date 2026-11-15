using System;
using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Contract;
using Husnisse.Core;
using Husnisse.Flow;
using Husnisse.Net;
using Husnisse.Orders;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Commands
{
    /// <summary>
    /// `husnisse_supply` — drive a supply errand without a crafting window.
    ///
    /// Two jobs. With no argument it prints what the nisse is carrying and for whom, which is how a
    /// gate reads the errand between its phases. With an item and an amount it asks for exactly what
    /// a craft press asks for, through the same validation and the same visible trip: on a client
    /// the load comes back to the caller's inventory, and on a headless server there is nobody to
    /// take it, so the load is set down at the bench and the claim's ground pass picks it up. That
    /// second case is the gate's proof that the delivery is a real transfer, not a bookkeeping entry.
    /// </summary>
    public static class SupplyCommand
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_supply",
                "Ask the nisse for materials, or show the errands in flight: husnisse_supply [item] [amount] | status.",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var sub = args.Args.Length > 1 ? args.Args[1] : "status";

            if (string.Equals(sub, "status", StringComparison.OrdinalIgnoreCase))
            {
                Emit(args, "supply errands=" + FetchRunner.Count + " " + FetchRunner.Describe());
                foreach (var claim in ClaimRegistry.All())
                {
                    var fetch = FetchRunner.Get(claim);
                    if (fetch == null) continue;
                    Emit(args, "delivery claim=" + claim.BowlId + " token=" + fetch.Token + " phase=" + fetch.PhaseName +
                         " carried=" + fetch.Carried + " planned=" + fetch.Planned + " player=" + fetch.PlayerId);
                }
                return;
            }

            var shared = StorageContract.SharedName(sub);
            var amount = 1;
            if (args.Args.Length > 2) int.TryParse(args.Args[2], out amount);
            if (amount <= 0) amount = 1;

            if (string.IsNullOrEmpty(shared))
            {
                Emit(args, "supply: name a material, e.g. husnisse_supply Wood 10");
                return;
            }

            var player = Player.m_localPlayer;
            var home = player != null ? ClaimRegistry.FindFor(player.transform.position) : FirstClaim();
            if (home == null || home.Bowl == null)
            {
                Emit(args, "supply refused: no claim here");
                return;
            }

            var caller = player != null
                ? OrderCaller.Local(player)
                : new OrderCaller { PlayerId = 0L, PeerUid = 0L, Position = home.Center };

            var items = new List<KeyValuePair<string, int>>();
            items.Add(new KeyValuePair<string, int>(shared, amount));

            var token = Environment.TickCount;
            var verdict = SupplyService.Begin(caller, home.BowlPosition, token, caller.Position, items);

            Emit(args, "supply request item=" + shared + " amount=" + amount + " claim=" + home.BowlId +
                       " caller=" + caller.PlayerId + " peer=" + caller.PeerUid +
                       " caller_pos=" + caller.Position.ToString("F1") +
                       " claim_center=" + home.Center.ToString("F1") +
                       " dist=" + Vector3.Distance(caller.Position, home.Center).ToString("0.0") +
                       " -> " + verdict +
                       " note=\"" + (SupplyService.LastNote ?? string.Empty) + "\"");
            FetchRpc.Register();
        }

        private static Claim FirstClaim()
        {
            foreach (var claim in ClaimRegistry.All())
            {
                if (claim != null && claim.Bowl != null) return claim;
            }
            return null;
        }

        /// <summary>A line for the console, the BepInEx log and the diagnostics file at once.</summary>
        private static void Emit(Terminal.ConsoleEventArgs args, string line)
        {
            Log.Info(line);
            args?.Context?.AddString(line);
            DiagnosticsFile.Line(line);
        }
    }
}
