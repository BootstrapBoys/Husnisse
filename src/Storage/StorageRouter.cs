using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Memory;

namespace Husnisse.Storage
{
    /// <summary>
    /// Decides which chest a stack belongs in: the one the nisse remembers, then a chest that already
    /// holds that item, then any chest with room. Learning happens only after a successful deposit.
    /// </summary>
    public static class StorageRouter
    {
        public static Container FindTarget(Claim claim, string itemName, ItemMemory memory)
        {
            if (memory != null)
            {
                ZDOID remembered;
                if (memory.TryRecall(itemName, out remembered))
                {
                    var known = ContainerIndex.ById(claim, remembered);
                    if (known != null && ContainerIndex.HasRoom(known)) return known;
                }
            }

            Container fallback = null;
            foreach (var container in ContainerIndex.InClaim(claim))
            {
                var inventory = container.GetInventory();
                if (inventory == null) continue;

                if (inventory.ContainsItemByName(itemName))
                {
                    return container;
                }

                if (fallback == null && ContainerIndex.HasRoom(container)) fallback = container;
            }

            return fallback;
        }

        public static void Learn(Container container, string itemName, ItemMemory memory)
        {
            if (container == null || memory == null) return;
            var view = container.GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null) return;
            memory.Remember(itemName, view.GetZDO().m_uid);
            Log.Debug("Memory", "learned " + itemName + " -> " + view.GetZDO().m_uid);
        }
    }
}
