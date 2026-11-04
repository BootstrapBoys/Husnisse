using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Creature;
using Husnisse.Memory;
using Husnisse.Rounds;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Orders
{
    /// <summary>
    /// Orders the player gives the nisse directly: empty my pockets, and tidy this chest. Both are
    /// the nisse's work — they run only if it exists, is fed when feeding is on, and the player is
    /// inside its claim.
    /// </summary>
    public static class OrderService
    {
        public static bool EmptyPockets(Player player, Claim claim)
        {
            NisseActor nisse;
            if (!Ready(claim, player, out nisse)) return true;

            var memory = MemoryOf(nisse);
            var inventory = player.GetInventory();
            if (inventory == null) return true;

            var context = Context(claim, nisse);
            var items = new List<ItemDrop.ItemData>();
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null || item.m_equipped) continue;
                items.Add(item);
            }

            var moved = 0;
            foreach (var item in items)
            {
                if (!context.CanSort()) break;

                var target = StorageRouter.FindTarget(claim, item.m_shared.m_name, memory);
                if (target == null) continue;

                var targetInventory = target.GetInventory();
                if (targetInventory == null || !targetInventory.CanAddItem(item)) continue;
                if (!targetInventory.AddItem(item.Clone())) continue;

                inventory.RemoveItem(item, item.m_stack);
                StorageRouter.Learn(target, item.m_shared.m_name, memory);
                context.SpendSort();
                moved++;
            }

            player.Message(MessageHud.MessageType.Center,
                moved > 0 ? "The nisse takes " + moved + " stack(s)." : "The nisse has nowhere to put that.");
            return true;
        }

        public static bool Tidy(Container source, Claim claim, Player player)
        {
            NisseActor nisse;
            if (!Ready(claim, player, out nisse)) return true;

            var sourceInventory = source != null ? source.GetInventory() : null;
            if (sourceInventory == null) return true;

            var memory = MemoryOf(nisse);
            var context = Context(claim, nisse);

            var items = new List<ItemDrop.ItemData>();
            for (var i = 0; i < sourceInventory.NrOfItems(); i++)
            {
                var item = sourceInventory.GetItem(i);
                if (item != null && item.m_shared != null) items.Add(item);
            }

            var moved = 0;
            foreach (var item in items)
            {
                if (!context.CanSort()) break;

                var target = StorageRouter.FindTarget(claim, item.m_shared.m_name, memory, source);
                if (target == null) continue;

                var targetInventory = target.GetInventory();
                if (targetInventory == null || !targetInventory.CanAddItem(item)) continue;
                if (!targetInventory.AddItem(item.Clone())) continue;

                sourceInventory.RemoveItem(item, item.m_stack);
                StorageRouter.Learn(target, item.m_shared.m_name, memory);
                context.SpendSort();
                moved++;
            }

            player.Message(MessageHud.MessageType.Center,
                moved > 0 ? "The nisse files " + moved + " stack(s)." : "The nisse leaves it as it is.");
            return true;
        }

        private static RoundContext Context(Claim claim, NisseActor nisse)
        {
            return new RoundContext(claim, claim.Bowl, nisse, 0, HusnisseConfig.SortBudget.Value, 0, 0);
        }

        private static ItemMemory MemoryOf(NisseActor nisse)
        {
            var view = nisse != null ? nisse.GetComponent<ZNetView>() : null;
            return view != null && view.GetZDO() != null ? new ItemMemory(view.GetZDO()) : null;
        }

        private static bool Ready(Claim claim, Player player, out NisseActor nisse)
        {
            nisse = null;

            if (!HusnisseConfig.AllowOrders.Value) return false;

            if (claim == null || claim.Bowl == null)
            {
                if (player != null) player.Message(MessageHud.MessageType.Center, "No nisse claims this place.");
                return false;
            }

            if (claim.Suppressed)
            {
                if (player != null) player.Message(MessageHud.MessageType.Center, "This nisse refuses to settle here.");
                return false;
            }

            if (player != null && Vector3.Distance(player.transform.position, claim.Center) > claim.Radius + 5f)
            {
                player.Message(MessageHud.MessageType.Center, "The nisse is not nearby.");
                return false;
            }

            if (!claim.Bowl.IsFed)
            {
                if (player != null) player.Message(MessageHud.MessageType.Center, "The nisse is not working. The bowl is empty.");
                return false;
            }

            nisse = NisseFactory.EnsureNisse(claim.Bowl);
            if (nisse != null) return true;

            if (player != null) player.Message(MessageHud.MessageType.Center, "The nisse is not here.");
            return false;
        }
    }
}
