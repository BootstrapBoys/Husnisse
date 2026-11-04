using Husnisse.Core;
using Husnisse.Memory;
using Husnisse.Rounds;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Tasks
{
    /// <summary>
    /// Picks up ground items inside the claim and files them. This is the 0.1 deposit pile: what the
    /// player drops is what the nisse tidies.
    /// </summary>
    public class CollectDropsTask : IRoundTask
    {
        public string Name
        {
            get { return "collect"; }
        }

        public bool Run(RoundContext context)
        {
            var memory = MemoryOf(context);
            var worked = false;

            foreach (var drop in Object.FindObjectsOfType<ItemDrop>())
            {
                if (drop == null) continue;
                if (!context.Claim.Contains(drop.transform.position)) continue;
                if (!context.CanCollect()) break;

                var item = drop.m_itemData;
                if (item == null || item.m_shared == null) continue;

                var target = StorageRouter.FindTarget(context.Claim, item.m_shared.m_name, memory);
                if (target == null) continue;

                var inventory = target.GetInventory();
                if (inventory == null || !inventory.CanAddItem(item)) continue;
                if (!inventory.AddItem(item)) continue;

                if (context.Nisse != null) context.Nisse.WarpTo(drop.transform.position);
                ZNetScene.instance.Destroy(drop.gameObject);

                StorageRouter.Learn(target, item.m_shared.m_name, memory);
                context.SpendCollect();
                worked = true;
            }

            if (worked) Log.Debug("Tasks", Name + ": filed " + context.Collected + " item(s) in " + context.Claim.BowlId);
            return worked;
        }

        private static ItemMemory MemoryOf(RoundContext context)
        {
            var actor = context.Nisse;
            if (actor == null) return null;
            var view = actor.GetComponent<ZNetView>();
            return view != null && view.GetZDO() != null ? new ItemMemory(view.GetZDO()) : null;
        }
    }
}
