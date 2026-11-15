using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Flow;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Commands
{
    /// <summary>
    /// `husnisse_bench` — what the network costs at the size the roadmap promises to survive: two
    /// hundred containers in one claim. It times the four pieces a reconcile tick actually runs, in
    /// the shop the tick runs them in:
    ///
    ///   index    — gathering the claim's containers (one scene scan, the biggest fixed cost)
    ///   network  — filtering that list to the ones a stack may be filed into
    ///   decide   — one routing answer per item, against a list gathered once
    ///   refill   — the whole pass: drops inside the claim, ground and deposit, queue filled
    ///
    /// The numbers are for one call, averaged over <c>runs</c> passes; they are what the gate records,
    /// not a micro-benchmark of a helper. The claim's queue is emptied before and after, so the bench
    /// leaves no work behind and does not make the nisse walk two hundred trips afterwards.
    /// </summary>
    public static class BenchCommand
    {
        private const int SampleCap = 200;

        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_bench",
                "Times the container network on the first claim: index, network, decide and one full refill pass. " +
                "husnisse_bench [runs] (default 10).",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var runs = 10;
            if (args.Args.Length > 1) int.TryParse(args.Args[1], out runs);
            if (runs < 1) runs = 1;
            if (runs > 100) runs = 100;

            foreach (var line in Lines(FirstClaim(), runs))
            {
                args.Context?.AddString(line);
                DiagnosticsFile.Line(line);
            }
        }

        /// <summary>
        /// The measurement itself, as lines. The console command prints them and the rig harness
        /// reads the same list, so the numbers a gate asserts on are the ones a player sees — there
        /// is no second implementation to drift.
        /// </summary>
        public static List<string> Lines(Claim claim, int runs)
        {
            var lines = new List<string>();
            if (runs < 1) runs = 1;
            if (runs > 100) runs = 100;

            if (claim == null || claim.Bowl == null)
            {
                lines.Add("husnisse bench no-claim");
                return lines;
            }

            var containers = ContainerIndex.InClaim(claim);
            var samples = Samples(claim, containers);
            var network = StorageRouter.NetworkCandidates(claim, containers);

            lines.Add("husnisse bench scene claim=" + claim.BowlId +
                      " containers=" + containers.Count +
                      " network=" + network.Count +
                      " samples=" + samples.Count +
                      " runs=" + runs);

            Measure(lines, "index", runs, containers.Count, delegate { ContainerIndex.InClaim(claim); });
            Measure(lines, "network", runs, containers.Count, delegate { StorageRouter.NetworkCandidates(claim, containers); });

            Measure(lines, "decide", runs, samples.Count, delegate
            {
                foreach (var item in samples) StorageRouter.Decide(claim, network, item, null, null);
            });

            // Refill is the pass a reconcile tick runs; the queue is cleared first so every run
            // measures a refill from empty rather than the cheap "already queued" path.
            Measure(lines, "refill", runs, samples.Count, delegate
            {
                FlowQueue.Forget(claim.BowlId);
                FlowScheduler.Refill(claim);
            });

            var queued = FlowQueue.Count(claim);
            FlowQueue.Forget(claim.BowlId);

            lines.Add("husnisse bench done queued_after_refill=" + queued + " (queue cleared)");
            return lines;
        }

        private static Claim FirstClaim()
        {
            foreach (var claim in ClaimRegistry.All())
            {
                if (claim != null && claim.Bowl != null) return claim;
            }
            return null;
        }

        /// <summary>
        /// Real stacks to route: what the chests already hold plus what lies on the ground inside the
        /// claim. A routing answer needs a real ItemData, and a synthetic one would not be the cost
        /// the tick pays (shared-name lookup, type rules and a clone per candidate).
        /// </summary>
        private static List<ItemDrop.ItemData> Samples(Claim claim, List<Container> containers)
        {
            var samples = new List<ItemDrop.ItemData>();

            foreach (var container in containers)
            {
                var inventory = container.GetInventory();
                if (inventory == null) continue;
                for (var i = 0; i < inventory.NrOfItems() && samples.Count < SampleCap; i++)
                {
                    var item = inventory.GetItem(i);
                    if (item != null && item.m_shared != null) samples.Add(item);
                }
                if (samples.Count >= SampleCap) break;
            }

            foreach (var drop in UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (samples.Count >= SampleCap) break;
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                if (!claim.Contains(drop.transform.position)) continue;
                samples.Add(drop.m_itemData);
            }

            return samples;
        }

        private static void Measure(List<string> lines, string metric, int runs, int units, Action body)
        {
            var watch = new Stopwatch();
            var total = 0d;
            var worst = 0d;

            // One warm-up call outside the measurement: the first call JITs the path and would
            // otherwise be reported as the cost of the work.
            body();

            for (var i = 0; i < runs; i++)
            {
                watch.Reset();
                watch.Start();
                body();
                watch.Stop();

                var ms = watch.Elapsed.TotalMilliseconds;
                total += ms;
                if (ms > worst) worst = ms;
            }

            lines.Add("husnisse bench metric=" + metric +
                      " units=" + units +
                      " runs=" + runs +
                      " avg_ms=" + Millis(total / runs) +
                      " max_ms=" + Millis(worst));
        }

        private static string Millis(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
