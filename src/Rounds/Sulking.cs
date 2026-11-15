using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Localization;
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

            // Both chests are moved on the server, so neither ZDO is necessarily this machine's:
            // take each one before the write and put the packed inventory back afterwards, or the
            // sulk looks like it happened and the next owner write undoes it.
            var claimed = Storage.ContainerWrite.EnsureOwned(from, "sulk source");
            claimed &= Storage.ContainerWrite.EnsureOwned(target, "sulk target");

            if (!target.GetInventory().AddItem(item.Clone())) return;
            fromInventory.RemoveItem(item, item.m_stack);

            var persisted = Storage.ContainerWrite.Persist(target, "sulk target");
            persisted &= Storage.ContainerWrite.Persist(from, "sulk source");
            if (!claimed || !persisted)
            {
                Log.Warn("sulk: the move from " + from.name + " to " + target.name +
                         " may not have reached the ZDO (claimed=" + claimed + " persisted=" + persisted + ")");
            }

            bowl.RegisterSulk();
            Creature.NisseVoice.Say(claim, Loc.T(Loc.VoiceSulk), Creature.NisseVoice.Kind.Event);
            Log.Info("sulk: misfiled " + item.m_shared.m_name + " in claim " + claim.BowlId);
        }
    }
}
