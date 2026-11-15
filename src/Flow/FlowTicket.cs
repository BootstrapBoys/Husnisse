using Husnisse.Claims;
using UnityEngine;

namespace Husnisse.Flow
{
    public enum FlowSource
    {
        Ground,
        Container,
        Pockets,
    }

    /// <summary>One item stack waiting to be moved. The nisse works through these at the configured rate.</summary>
    public class FlowTicket
    {
        public Claim Claim;
        public FlowSource Source;
        public ItemDrop Drop;
        public Container Container;
        public Inventory Inventory;
        public ItemDrop.ItemData Item;
        public int Amount;

        public static FlowTicket FromDrop(Claim claim, ItemDrop drop)
        {
            return new FlowTicket
            {
                Claim = claim,
                Source = FlowSource.Ground,
                Drop = drop,
                Item = drop != null ? drop.m_itemData : null,
                Amount = drop != null && drop.m_itemData != null ? drop.m_itemData.m_stack : 0,
            };
        }

        public static FlowTicket FromContainer(Claim claim, Container container, ItemDrop.ItemData item)
        {
            return new FlowTicket
            {
                Claim = claim,
                Source = FlowSource.Container,
                Container = container,
                Item = item,
                Amount = item != null ? item.m_stack : 0,
            };
        }

        public static FlowTicket FromPockets(Claim claim, Inventory inventory, ItemDrop.ItemData item)
        {
            return new FlowTicket
            {
                Claim = claim,
                Source = FlowSource.Pockets,
                Inventory = inventory,
                Item = item,
                Amount = item != null ? item.m_stack : 0,
            };
        }

        public bool IsValid()
        {
            if (Claim == null || Claim.Bowl == null || Item == null || Item.m_shared == null) return false;
            if (Amount <= 0) return false;

            if (Source == FlowSource.Ground)
            {
                if (Drop == null || Drop.gameObject == null) return false;

                // A claim is a circle on the map, not a cylinder: a stack that has fallen out of the
                // world (off a cliff, into the sea, through a floor on a server with no terrain)
                // still reads as "inside the claim" and would be retried every drain for ever. The
                // nisse does not dredge; a drop this far below the bowl is gone, not work.
                if (Drop.transform.position.y < Claim.Center.y - GoneBelow) return false;
                return true;
            }

            var inventory = Source == FlowSource.Container
                ? (Container != null ? Container.GetInventory() : null)
                : Inventory;

            return inventory != null && inventory.ContainsItem(Item);
        }

        /// <summary>How far below a claim's bowl a drop has to fall before it is out of the world.</summary>
        public const float GoneBelow = 40f;

        public Inventory SourceInventory()
        {
            if (Source == FlowSource.Container) return Container != null ? Container.GetInventory() : null;
            if (Source == FlowSource.Pockets) return Inventory;
            return null;
        }

        public Container ExcludeContainer()
        {
            return Source == FlowSource.Container ? Container : null;
        }
    }
}
