using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Storage;
using Husnisse.Compat;
using Husnisse.Creature;
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
                      " interval=" + HusnisseConfig.RoundInterval.Value +
                      " radius=" + HusnisseConfig.ClaimRadius.Value +
                      " forced_feeding=" + WorldPolicy.FeedingForced);

            lines.Add("[compat] wardislove=" + (WardIsLoveCompat.Installed ? "installed" : "absent") +
                      " require_ward_ownership=" + HusnisseConfig.RequireWardOwnership.Value +
                      " quartermaster=" + (PluginGuards.QuartermasterInstalled() ? "installed" : "absent"));

            lines.Add("schema=" + SaveSchema.Read() +
                      " feeding_forced=" + WorldPolicy.FeedingForced +
                      " claims=" + ClaimRegistry.Count);

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
                foreach (var actor in Object.FindObjectsOfType<NisseActor>())
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
                          " containers=" + containers.Count +
                          " stacks=" + stacks +
                          " nisse=" + nisse);
            }

            return lines;
        }
    }
}
