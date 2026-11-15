using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Bowl;
using Husnisse.Storage;
using Husnisse.Compat;
using Husnisse.Creature;
using Husnisse.Flow;
using UnityEngine;

namespace Husnisse.Core
{
    /// <summary>What `husnisse_status` prints. Enough to debug a base without a client GUI.</summary>
    public static class Diagnostics
    {
        public static List<string> Lines()
        {
            // The status command is a probe, not a snapshot: resolve first so a ward that arrived or
            // left since the last timer tick is already reflected in the numbers below.
            ClaimRegistry.RevalidateAll();

            var lines = new List<string>();
            lines.Add("[config] enabled=" + HusnisseConfig.Enabled.Value +
                      " debug=" + HusnisseConfig.DebugLogging.Value +
                      " diagnostics_file=" + HusnisseConfig.WriteDiagnosticsFile.Value +
                      " rate=" + HusnisseConfig.ItemsPerSecond.Value +
                      " reconcile=" + HusnisseConfig.ReconcileInterval.Value +
                      " bowl_drain=" + HusnisseConfig.BowlDrainMinutes.Value +
                      " radius=" + HusnisseConfig.ClaimRadius.Value +
                      " feeding_required=" + WorldPolicy.FeedingRequired);

            lines.Add("[compat] wardislove=" + (WardIsLoveCompat.Installed ? "installed" : "absent") +
                      " wards=" + WardIsLoveCompat.Count +
                      " ward_api=" + (WardIsLoveCompat.Bound ? "bound" : "unbound") +
                      " require_ward_ownership=" + HusnisseConfig.RequireWardOwnership.Value +
                      " quartermaster=" + (PluginGuards.QuartermasterInstalled() ? "installed" : "absent"));

            lines.Add("schema=" + SaveSchema.Read() +
                      " feeding_required=" + WorldPolicy.FeedingRequired +
                      " claims=" + ClaimRegistry.Count +
                      " trips=" + TripRunner.Count +
                      " nisses=" + Object.FindObjectsByType<NisseActor>(FindObjectsSortMode.None).Length +
                      " bowl_piece=" + BowlPieceState());

            var network = 0;
            var ruled = 0;
            var deposits = 0;
            foreach (var claim in ClaimRegistry.All())
            {
                foreach (var container in ContainerIndex.InClaim(claim))
                {
                    if (!ContainerNetwork.IsOnNetwork(container) && ContainerNetwork.RuleOf(container).IsNone) continue;
                    network++;
                    if (!ContainerNetwork.RuleOf(container).IsNone) ruled++;
                    if (ContainerNetwork.RuleOf(container).Kind == RuleKind.Deposit &&
                        ContainerNetwork.IsOnNetwork(container)) deposits++;
                    lines.Add("rule " + claim.BowlId + " " + ContainerNetwork.Describe(container));
                }
            }
            lines.Add("[rules] containers=" + network + " ruled=" + ruled + " deposits=" + deposits);

            foreach (var claim in ClaimRegistry.All())
            {
                var bowl = claim.Bowl;
                var containers = ContainerIndex.InClaim(claim);
                var stacks = 0;
                foreach (var container in containers)
                {
                    var inventory = container.GetInventory();
                    if (inventory != null) stacks += inventory.NrOfItems();
                }

                var nisse = "none";
                foreach (var actor in Object.FindObjectsByType<NisseActor>(FindObjectsSortMode.None))
                {
                    if (actor != null && actor.BowlId == claim.BowlId) nisse = "yes";
                }

                lines.Add("claim " + claim.BowlId +
                          " source=" + claim.SourceName +
                          " ward=" + claim.WardId +
                          " owner=" + claim.OwnerId +
                          " r=" + claim.Radius.ToString("0") +
                          " center=" + claim.Center.x.ToString("0.#") + "," + claim.Center.z.ToString("0.#") +
                          " native_r=" + claim.NativeRadius.ToString("0") +
                          " native_owner=" + claim.NativeOwnerId +
                          " suppressed=" + claim.Suppressed +
                          " refusal_code=" + claim.RefusalCode +
                          (claim.Refusal != null ? " refusal=\"" + claim.Refusal + "\"" : string.Empty) +
                          " satiety=" + (bowl != null ? bowl.Satiety.ToString("0") : "-") +
                          " mood=" + (bowl != null ? bowl.Mood.ToString("0") : "-") +
                          " sulk=" + (bowl != null ? bowl.SulkLevel.ToString() : "-") +
                          " bowl_items=" + (bowl != null && bowl.GetComponent<Container>() != null &&
                                             bowl.GetComponent<Container>().GetInventory() != null
                              ? bowl.GetComponent<Container>().GetInventory().NrOfItems().ToString()
                              : "-") +
                          " pantry=" + (bowl != null ? bowl.PantrySize() : "-") +
                          " queued=" + FlowQueue.Count(claim) +
                          " trip=" + (TripRunner.Get(claim) != null ? TripRunner.Get(claim).ShortState() : "idle") +
                          " containers=" + containers.Count +
                          " stacks=" + stacks +
                          " nisse=" + nisse);
            }

            return lines;
        }

        private static string BowlPieceState()
        {
            var database = ObjectDB.instance;
            if (database == null) return "no-objectdb";

            var pieces = database.GetAllBuildPieces(true);
            if (pieces == null) return "no-list";

            foreach (var piece in pieces)
            {
                if (piece != null && piece.name == BowlPrefab.PrefabName) return "present";
            }

            return "missing";
        }
    }
}
