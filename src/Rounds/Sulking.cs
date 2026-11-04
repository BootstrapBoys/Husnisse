using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Tasks;
using UnityEngine;

namespace Husnisse.Rounds
{
    /// <summary>
    /// What an unfed nisse does about it: it moves one stack into the wrong chest, at most a few times,
    /// and it complains where the players can hear it. Nothing is ever deleted — only misfiled.
    /// </summary>
    public static class Sulking
    {
        public static void Step(Claim claim)
        {
            var bowl = claim != null ? claim.Bowl : null;
            if (bowl == null || !bowl.RequiresFeeding) return;
            if (bowl.IsFed) return;
            if (bowl.Mood >= 30f) return;
            if (bowl.SulkLevel >= HusnisseConfig.SulkMisfileCap.Value) return;
            if (!bowl.SulkDue(HusnisseConfig.SulkIntervalMinutes.Value)) return;

            var containers = Storage.ContainerIndex.InClaim(claim);
            var sources = new List<Container>();
            foreach (var container in containers)
            {
                var inventory = container.GetInventory();
                if (inventory != null && inventory.NrOfItems() > 0) sources.Add(container);
            }
            if (sources.Count < 2) return;

            var from = sources[Random.Range(0, sources.Count)];
            var fromInventory = from.GetInventory();
            var item = fromInventory.GetItem(Random.Range(0, fromInventory.NrOfItems()));
            if (item == null) return;

            Container target = null;
            foreach (var candidate in containers)
            {
                if (candidate == from) continue;
                var inventory = candidate.GetInventory();
                if (inventory != null && inventory.CanAddItem(item))
                {
                    target = candidate;
                    break;
                }
            }
            if (target == null) return;

            if (!target.GetInventory().AddItem(item.Clone())) return;
            fromInventory.RemoveItem(item, item.m_stack);

            bowl.RegisterSulk();
            Creature.NisseVoice.Say(claim, "Something clatters into the wrong chest.", true);
            Log.Info("sulk: misfiled " + item.m_shared.m_name + " in claim " + claim.BowlId);
        }
    }
}
