using System.Collections.Generic;
using Husnisse.Core;
using Husnisse.Migration;
using Husnisse.Storage;
using Husnisse.Util;

namespace Husnisse.Commands
{
    /// <summary>
    /// `husnisse migrate` — the read-only Quartermaster migration.
    ///
    /// Quartermaster stores its per-container settings as a JSON string in each container's ZDO, so
    /// they outlive its removal: the operator removes Quartermaster from a copy of the world, boots
    /// Husnisse, and this command reads those records and offers to translate them. It never writes a
    /// Quartermaster key, never needs Quartermaster installed, and never moves an item — a scan is
    /// pure, and an apply writes one Husnisse rule per container and then reads it back.
    ///
    /// It works on the **save** (ZDOs), not on the loaded scene: a server that nobody is standing on
    /// has no chest GameObjects at all, and a migration that only saw loaded pieces would skip most of
    /// a base without saying so.
    ///
    /// `scan` is the safe default: it says exactly what would change, per container, plus what has no
    /// equivalent (Quartermaster's base groups and its watch-and-learn model) and what could not be
    /// translated (an item from a mod this world does not have).
    /// </summary>
    public static class MigrateCommand
    {
        private const string Usage =
            "usage: husnisse migrate scan | apply | status — read Quartermaster's per-container records " +
            "and translate them into Husnisse rules. `scan` writes nothing.";

        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_migrate",
                "Quartermaster migration: scan (report only) | apply (write Husnisse rules) | status.",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var sub = args.Args.Length > 1 ? args.Args[1].ToLowerInvariant() : "scan";

            if (sub == "status") { Status(args.Context); return; }
            if (sub == "scan") { Scan(args.Context); return; }
            if (sub == "apply") { Apply(args.Context); return; }

            Say(args.Context, Usage);
        }

        private static void Status(Terminal context)
        {
            var containers = QmScan.Containers();
            int withItems;
            var orphans = QmScan.Orphans(out withItems);

            var migrated = 0;
            var live = 0;
            foreach (var found in containers)
            {
                // "Migrated" is the presence of a Husnisse rule, not a role: a chest whose only
                // Quartermaster setting was a supply restriction carries `k=none` and is still done.
                if (!string.IsNullOrEmpty(found.Existing)) migrated++;
                if (found.Live) live++;
            }

            Say(context, "[Migrate] status records=" + containers.Count +
                          " migrated=" + migrated +
                          " pending=" + (containers.Count - migrated) +
                          " live=" + live +
                          " orphans=" + orphans +
                          " orphan_items=" + withItems);
        }

        private static void Scan(Terminal context)
        {
            var containers = QmScan.Containers();
            int withItems;
            var orphans = QmScan.Orphans(out withItems);

            Say(context, "[Migrate] found records=" + containers.Count +
                          " orphans=" + orphans + " orphan_items=" + withItems +
                          " contents=" + Fingerprint(containers));

            var wouldApply = 0;
            var already = 0;
            var unmapped = 0;

            for (var i = 0; i < containers.Count; i++)
            {
                var plan = QmPlan.For(containers[i]);
                Say(context, "[Migrate] " + plan.Describe(i));

                if (plan.Unmapped.Count > 0) unmapped += plan.Unmapped.Count;

                if (!containers[i].ExistingRule.IsNone) already++;
                else if (plan.HasRule) wouldApply++;

                foreach (var note in plan.Notes) Say(context, "[Migrate] note " + i + " " + note);
            }

            Say(context, "[Migrate] scan complete would_apply=" + wouldApply +
                          " already=" + already +
                          " unmapped=" + unmapped +
                          " orphans=" + orphans);

            if (orphans > 0)
            {
                Say(context, "[Migrate] " + orphans + " record(s) belong to a prefab this world does not have " +
                             "(Quartermaster's own chests): their contents stay in the save untouched and need " +
                             "Quartermaster back, or a manual move, before they can be used again");
            }
        }

        private static void Apply(Terminal context)
        {
            if (!ConfirmNoPlayers(context)) return;

            var containers = QmScan.Containers();
            int withItems;
            var orphans = QmScan.Orphans(out withItems);

            var before = Fingerprint(containers);

            var applied = 0;
            var skipped = 0;
            var failed = 0;
            var preserved = 0;

            for (var i = 0; i < containers.Count; i++)
            {
                var found = containers[i];
                var plan = QmPlan.For(found);

                if (!string.IsNullOrEmpty(found.Existing))
                {
                    skipped++;
                    Say(context, "[Migrate] skipped " + i + " " + found.Prefab +
                                 " at=" + Where(found) + " already='" + found.Existing + "'");
                    continue;
                }

                if (!plan.HasRule)
                {
                    // Nothing to carry over: an unconfigured chest. It does not join the network by
                    // itself — that is a decision the owner makes in the container window.
                    skipped++;
                    Say(context, "[Migrate] skipped " + i + " " + found.Prefab + " (no Quartermaster rule to carry)");
                    continue;
                }

                var owner = found.Zdo.GetOwner();
                if (owner != 0L && owner != ZDOMan.GetSessionID())
                {
                    failed++;
                    Say(context, "[Migrate] failed " + i + " " + found.Prefab +
                                 " (its ZDO belongs to " + owner + "; the write would not persist)");
                    continue;
                }

                var rule = plan.Rule;
                rule.Sanitise();
                var encoded = rule.Encode();

                found.Zdo.Set(ZdoKeys.ContainerNetwork, true);
                found.Zdo.Set(ZdoKeys.ContainerRule, encoded);

                // Read back from the ZDO, not from the object just written: the rule every peer will
                // read is the one in the ZDO.
                var stored = found.Zdo.GetString(ZdoKeys.ContainerRule, string.Empty);
                var after = Fingerprint(found);
                if (after == Fnv(found.Items)) preserved++;

                if (stored != encoded)
                {
                    failed++;
                    Say(context, "[Migrate] failed " + i + " " + found.Prefab + " wrote='" + stored + "'");
                    continue;
                }

                applied++;
                Say(context, "[Migrate] applied " + i + " " + found.Prefab +
                             " at=" + Where(found) +
                             " rule=" + rule.Describe() + rule.SupplyDescribe() +
                             " net=on contents=" + (after == Fnv(found.Items) ? "same" : "CHANGED"));
            }

            var afterAll = Fingerprint(containers);

            Say(context, "[Migrate] apply complete applied=" + applied + " skipped=" + skipped +
                          " failed=" + failed + " contents_preserved=" + preserved +
                          " orphans=" + orphans + " orphan_items=" + withItems +
                          " contents_before=" + before + " contents_after=" + afterAll +
                          " contents=" + (before == afterAll ? "same" : "CHANGED"));

            DiagnosticsFile.Line("[Migrate] apply applied=" + applied + " skipped=" + skipped +
                                 " failed=" + failed + " preserved=" + preserved +
                                 " orphans=" + orphans + " orphan_items=" + withItems +
                                 " contents_same=" + (before == afterAll));
        }

        /// <summary>
        /// Applying a rule changes what the nisse may do with every chest in the world, so it stays a
        /// deliberate act: on a server with people on it, the operator waits.
        /// </summary>
        private static bool ConfirmNoPlayers(Terminal context)
        {
            if (ZNet.instance == null) return true;
            var peers = ZNet.instance.GetPeers();
            if (peers == null || peers.Count == 0) return true;

            Say(context, "[Migrate] refused: " + peers.Count +
                         " player(s) connected. Stop the server, or run this on the copied world with nobody on it.");
            return false;
        }

        /// <summary>One number for every recorded container's packed inventory, so "nothing moved" is a check.</summary>
        private static ulong Fingerprint(List<QmContainer> containers)
        {
            ulong total = 1469598103934665603UL;
            foreach (var container in containers)
            {
                total ^= Fnv(container.Items);
                total *= 1099511628211UL;
            }
            return total;
        }

        private static ulong Fingerprint(QmContainer container)
        {
            return Fnv(container.Zdo.GetByteArray("items"));
        }

        private static ulong Fnv(byte[] bytes)
        {
            ulong hash = 1469598103934665603UL;
            if (bytes == null) return hash;
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        private static string Where(QmContainer container)
        {
            var at = container.Position;
            return "(" + at.x.ToString("0.#") + "," + at.z.ToString("0.#") + ")";
        }

        private static void Say(Terminal context, string line)
        {
            context?.AddString(line);
            DiagnosticsFile.Line(line);
        }
    }
}
