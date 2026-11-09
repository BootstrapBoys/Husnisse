using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Flow;
using Husnisse.Localization;
using Husnisse.Net;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Orders
{
    public enum OrderKind : byte
    {
        EmptyPockets = 1,
        Tidy = 2,
        ToggleFeeding = 3,
    }

    /// <summary>
    /// Player requests become queue tickets. The client only asks; the server validates and enqueues,
    /// so no item is moved by a client.
    /// </summary>
    public static class OrderService
    {
        public static bool RequestEmptyPockets(Player player, Claim claim)
        {
            if (!Ready(claim, player, false)) return true;
            OrderRpc.Send(OrderKind.EmptyPockets, claim.BowlId, ZDOID.None, player.GetPlayerID());
            player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderQueued));
            return true;
        }

        public static bool RequestTidy(Player player, Claim claim, Container source)
        {
            if (!Ready(claim, player, false)) return true;

            var view = source != null ? source.GetComponent<ZNetView>() : null;
            var id = view != null && view.GetZDO() != null ? view.GetZDO().m_uid : ZDOID.None;
            if (id == ZDOID.None) return true;

            OrderRpc.Send(OrderKind.Tidy, claim.BowlId, id, player.GetPlayerID());
            player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderQueued));
            return true;
        }

        /// <summary>Server entry point, called from the RPC handler (or directly on a host).</summary>
        public static void ExecuteOnServer(long playerId, ZDOID bowlId, OrderKind kind, ZDOID containerId)
        {
            var player = Player.GetPlayer(playerId);
            var claim = ClaimRegistry.Find(bowlId);
            if (player == null || claim == null) return;

            if (kind == OrderKind.ToggleFeeding)
            {
                claim.Bowl.ServerToggleFeeding(playerId);
                return;
            }

            if (!Ready(claim, player, true)) return;

            if (kind == OrderKind.EmptyPockets)
            {
                EnqueuePockets(player, claim);
                return;
            }

            var source = ContainerIndex.ById(claim, containerId);
            if (source == null) return;
            EnqueueContainer(source, claim);
        }

        private static void EnqueuePockets(Player player, Claim claim)
        {
            var inventory = player.GetInventory();
            if (inventory == null) return;

            var queued = 0;
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;
                if (item.m_equipped || IsHotbar(item)) continue;

                FlowQueue.EnqueuePockets(claim, inventory, item);
                queued++;
            }

            Log.Debug("Orders", "empty pockets queued=" + queued + " claim=" + claim.BowlId);
        }

        private static void EnqueueContainer(Container source, Claim claim)
        {
            var inventory = source.GetInventory();
            if (inventory == null) return;

            var queued = 0;
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;

                FlowQueue.EnqueueContainer(claim, source, item);
                queued++;
            }

            Log.Debug("Orders", "tidy queued=" + queued + " claim=" + claim.BowlId + " source=" + source.name);
        }

        private static bool IsHotbar(ItemDrop.ItemData item)
        {
            // The first inventory row is the hotbar; those stacks belong to the player's hands.
            return item.m_gridPos.y == 0;
        }

        private static bool Ready(Claim claim, Player player, bool server)
        {
            if (!HusnisseConfig.AllowOrders.Value) return false;

            if (claim == null || claim.Bowl == null)
            {
                if (!server && player != null) player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNoClaim));
                return false;
            }

            if (claim.Suppressed)
            {
                if (!server && player != null) player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderRefuses));
                return false;
            }

            if (player != null && claim.OwnerId != 0L && player.GetPlayerID() != claim.OwnerId)
            {
                if (!server) player.Message(MessageHud.MessageType.Center, Loc.T(Loc.BowlNotYours));
                return false;
            }

            if (player != null && Vector3.Distance(player.transform.position, claim.Center) > claim.Radius + 5f)
            {
                if (!server) player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderTooFar));
                return false;
            }

            if (!claim.Bowl.IsFed)
            {
                if (!server) player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNotWorking));
                return false;
            }

            if (server)
            {
                if (NisseFactory.EnsureNisse(claim.Bowl) == null) return false;
            }

            return true;
        }
    }
}
