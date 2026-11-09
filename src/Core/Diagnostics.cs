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
                      " require_ward_ownership=" + HusnisseConfig.RequireWardOwnership.Value +
                      " quartermaster=" + (PluginGuards.QuartermasterInstalled() ? "installed" : "absent"));

            lines.Add("schema=" + SaveSchema.Read() +
                      " feeding_required=" + WorldPolicy.FeedingRequired +
                      " claims=" + ClaimRegistry.Count +
                      " nisses=" + Object.FindObjectsByType<NisseActor>(FindObjectsSortMode.None).Length +
                      " bowl_piece=" + BowlPieceState());

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
                          " owner=" + claim.OwnerId +
                          " r=" + claim.Radius.ToString("0") +
                          " suppressed=" + claim.Suppressed +
                          " satiety=" + (bowl != null ? bowl.Satiety.ToString("0") : "-") +
                          " mood=" + (bowl != null ? bowl.Mood.ToString("0") : "-") +
                          " sulk=" + (bowl != null ? bowl.SulkLevel.ToString() : "-") +
                          " bowl_items=" + (bowl != null && bowl.GetComponent<Container>() != null &&
                                             bowl.GetComponent<Container>().GetInventory() != null
                              ? bowl.GetComponent<Container>().GetInventory().NrOfItems().ToString()
                              : "-") +
                          " pantry=" + (bowl != null ? bowl.PantrySize() : "-") +
                          " queued=" + FlowQueue.Count(claim) +
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
