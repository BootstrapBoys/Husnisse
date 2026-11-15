using System.Collections.Generic;
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

    /// <summary>How an order ended. Wire values: a client and a server of the same build agree on them.</summary>
    public enum OrderResult : byte
    {
        Queued = 0,
        NoClaim = 1,
        Refuses = 2,
        NotYours = 3,
        TooFar = 4,
        NotWorking = 5,
        Nothing = 6,

        /// <summary>The server accepted the order and is waiting for the caller to hand the items over.</summary>
        Handover = 7,
    }

    /// <summary>
    /// Who gave an order. The server builds this from the connection that sent the request — never
    /// from the identity the package claims — so a client cannot order work in another player's name.
    ///
    /// A dedicated server simulates no player characters of its own, so a remote caller has no
    /// <see cref="Player"/>: its identity comes from the character ZDO the peer is driving and its
    /// position from the peer's reference position. The host and a single-player game have a real
    /// player object and take the ordinary path.
    /// </summary>
    public struct OrderCaller
    {
        public long PlayerId;
        public Player Player;
        public long PeerUid;
        public Vector3 Position;

        public bool Valid
        {
            get { return Player != null || PeerUid != 0L; }
        }

        public static readonly OrderCaller Invalid = default(OrderCaller);

        /// <summary>The player who is asking on this machine (host, single-player, or the client's own screen).</summary>
        public static OrderCaller Local(Player player)
        {
            return new OrderCaller
            {
                PlayerId = player != null ? player.GetPlayerID() : 0L,
                Player = player,
                PeerUid = 0L,
                Position = player != null ? player.transform.position : Vector3.zero,
            };
        }
    }

    /// <summary>The server's answer to an order, carried back to whoever asked.</summary>
    public struct OrderVerdict
    {
        public OrderResult Result;
        public int Count;
        public bool Feeding;

        public static OrderVerdict Of(OrderResult result)
        {
            return new OrderVerdict { Result = result };
        }
    }

    /// <summary>
    /// Player requests become queue tickets. The client only asks; the server validates and enqueues,
    /// so no item is moved on a client's word.
    ///
    /// One exception is built into the game: a player's inventory lives on the player's own machine
    /// and a dedicated server has no copy of it. "Empty Pockets" therefore happens in two steps —
    /// the server accepts the order and asks the caller to hand the stacks over, the caller removes
    /// them and sends the item data, and the server puts them on the ground by its bowl as real drops
    /// and lets the ordinary trip carry them to storage. Nothing else in the flow moves an item on a
    /// client's word: from the handover on, the server owns the drops.
    /// </summary>
    public static class OrderService
    {
        /// <summary>A handover is bounded like everything else: ten stacks is a trip's load.</summary>
        private const int MaxHandover = 40;

        public static bool RequestEmptyPockets(Player player, Claim claim)
        {
            if (player == null) return false;
            if (!HusnisseConfig.AllowOrders.Value) return true;

            var caller = OrderCaller.Local(player);
            var refusal = Validate(claim, caller, false);
            if (refusal != OrderResult.Queued)
            {
                Report(player, OrderKind.EmptyPockets, OrderVerdict.Of(refusal));
                return true;
            }

            if (PocketItems(player).Count == 0)
            {
                Report(player, OrderKind.EmptyPockets, OrderVerdict.Of(OrderResult.Nothing));
                return true;
            }

            var answer = OrderRpc.Send(OrderKind.EmptyPockets, claim.BowlId, ZDOID.None, caller.PlayerId);
            if (answer.HasValue) Report(player, OrderKind.EmptyPockets, answer.Value);
            return true;
        }

        public static bool RequestTidy(Player player, Claim claim, Container source)
        {
            if (player == null) return false;
            if (!HusnisseConfig.AllowOrders.Value) return true;

            var caller = OrderCaller.Local(player);
            var refusal = Validate(claim, caller, false);
            if (refusal != OrderResult.Queued)
            {
                Report(player, OrderKind.Tidy, OrderVerdict.Of(refusal));
                return true;
            }

            var view = source != null ? source.GetComponent<ZNetView>() : null;
            var id = view != null && view.GetZDO() != null ? view.GetZDO().m_uid : ZDOID.None;
            if (id == ZDOID.None)
            {
                Report(player, OrderKind.Tidy, OrderVerdict.Of(OrderResult.Nothing));
                return true;
            }

            var answer = OrderRpc.Send(OrderKind.Tidy, claim.BowlId, id, caller.PlayerId);
            if (answer.HasValue) Report(player, OrderKind.Tidy, answer.Value);
            return true;
        }

        /// <summary>Server entry point, called from the RPC handler (or directly on a host).</summary>
        public static OrderVerdict ExecuteOnServer(OrderCaller caller, ZDOID bowlId, OrderKind kind, ZDOID containerId)
        {
            if (!caller.Valid) return OrderVerdict.Of(OrderResult.NotYours);

            var claim = ClaimRegistry.Find(bowlId);
            if (claim == null || claim.Bowl == null) return OrderVerdict.Of(OrderResult.NoClaim);

            Log.Debug("Orders", "order kind=" + kind + " from=" + caller.PlayerId + " peer=" + caller.PeerUid +
                                " claim=" + bowlId + " live_player=" + (caller.Player != null));

            if (kind == OrderKind.ToggleFeeding)
            {
                if (WorldPolicy.FeedingRequired) return OrderVerdict.Of(OrderResult.Refuses);
                if (!claim.Allows(caller.PlayerId)) return OrderVerdict.Of(OrderResult.NotYours);

                return new OrderVerdict { Result = OrderResult.Queued, Feeding = claim.Bowl.ServerToggleFeeding(caller.PlayerId) };
            }

            var refusal = Validate(claim, caller, true);
            if (refusal != OrderResult.Queued) return OrderVerdict.Of(refusal);

            if (kind == OrderKind.EmptyPockets)
            {
                // Any caller that came over the wire owns its inventory somewhere else — on a
                // dedicated server no player object exists at all, and on a listen server the host's
                // copy of a client's inventory is not authoritative. Only this machine's own player
                // (host, single-player) is moved directly.
                if (caller.PeerUid != 0L) return OrderVerdict.Of(OrderResult.Handover);
                if (caller.Player == null) return OrderVerdict.Of(OrderResult.NotWorking);

                return new OrderVerdict { Result = OrderResult.Queued, Count = EnqueuePockets(caller.Player, claim) };
            }

            var source = ContainerIndex.ById(claim, containerId);
            if (source == null) return OrderVerdict.Of(OrderResult.Nothing);

            return new OrderVerdict { Result = OrderResult.Queued, Count = EnqueueContainer(source, claim) };
        }

        // ------------------------------------------------------------------ client side

        /// <summary>
        /// The stacks a nisse may file from this player: everything but the hotbar, worn equipment and
        /// quest items. Shared by the client's pre-check and by the handover, so both agree on the set.
        /// </summary>
        public static List<ItemDrop.ItemData> PocketItems(Player player)
        {
            var items = new List<ItemDrop.ItemData>();
            var inventory = player != null ? player.GetInventory() : null;
            if (inventory == null) return items;

            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;
                if (item.m_equipped || item.m_shared.m_questItem) continue;
                if (IsHotbar(item)) continue;
                items.Add(item);
            }

            return items;
        }

        /// <summary>The HUD line an order earns. Called on the machine that asked, never on the server's behalf.</summary>
        public static void Report(OrderKind kind, OrderVerdict verdict)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            Report(player, kind, verdict);
        }

        public static void Report(Player player, OrderKind kind, OrderVerdict verdict)
        {
            if (player == null) return;

            switch (verdict.Result)
            {
                case OrderResult.Queued:
                    if (kind == OrderKind.ToggleFeeding)
                    {
                        player.Message(MessageHud.MessageType.Center, Loc.T(verdict.Feeding ? Loc.BowlOfferOn : Loc.BowlOfferOff));
                    }
                    else if (kind == OrderKind.EmptyPockets)
                    {
                        player.Message(MessageHud.MessageType.Center,
                            verdict.Count > 0 ? string.Format(Loc.T(Loc.OrderTakes), verdict.Count) : Loc.T(Loc.OrderLeaves));
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.Center,
                            verdict.Count > 0 ? string.Format(Loc.T(Loc.OrderFiles), verdict.Count) : Loc.T(Loc.OrderLeaves));
                    }
                    break;

                case OrderResult.NoClaim: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNoClaim)); break;
                case OrderResult.Refuses: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderRefuses)); break;
                case OrderResult.NotYours: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.BowlNotYours)); break;
                case OrderResult.TooFar: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderTooFar)); break;
                case OrderResult.NotWorking: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNotWorking)); break;
                case OrderResult.Nothing: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderLeaves)); break;
                default: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderQueued)); break;
            }
        }

        // ------------------------------------------------------------------ server side

        /// <summary>
        /// Rebuilds the stacks a client handed over and puts them on the ground by the bowl as ordinary
        /// drops, queued for the next trip. The package holds one item blob per stack, written by the
        /// client's own <see cref="ItemDrop.ItemData.Save"/>.
        /// </summary>
        public static int MaterialisePockets(Claim claim, ZPackage package, int count)
        {
            if (claim == null || claim.Bowl == null || package == null) return 0;

            var origin = claim.Bowl.transform.position;
            var nisse = NisseFactory.Find(claim.Bowl);
            if (nisse != null) origin = nisse.transform.position;

            var queued = 0;
            var total = Mathf.Clamp(count, 0, MaxHandover);

            for (var i = 0; i < total; i++)
            {
                byte[] blob;
                try
                {
                    blob = package.ReadByteArray();
                }
                catch (System.Exception ex)
                {
                    Log.Warn("order: handover package ended after " + i + " of " + total + " stack(s): " + ex.Message);
                    break;
                }

                ItemDrop.ItemData item;
                try
                {
                    item = ReadItem(blob);
                }
                catch (System.Exception ex)
                {
                    // A blob a client wrote is not trusted input: one bad stack must not cost the rest.
                    Log.Warn("order: handed-over stack " + i + " (" + (blob != null ? blob.Length : 0) +
                             " bytes) could not be rebuilt: " + ex.Message);
                    continue;
                }

                if (item == null)
                {
                    Log.Warn("order: a handed-over stack could not be rebuilt and was dropped from the handover");
                    continue;
                }

                // Spread the pile so the drops do not spawn inside each other and roll away together.
                var angle = i * 0.9f;
                var offset = new Vector3(Mathf.Cos(angle) * 0.55f, 0.2f, Mathf.Sin(angle) * 0.55f);
                var drop = ItemDrop.DropItem(item, item.m_stack, origin + offset, Quaternion.identity);
                if (drop == null)
                {
                    Log.Warn("order: no drop prefab for " + item.m_shared.m_name + "; the stack stays in the handover");
                    continue;
                }

                FlowQueue.EnqueueDrop(claim, drop);
                queued++;
            }

            Note("empty pockets queued=" + queued + " claim=" + claim.BowlId);
            if (queued > 0) TripRunner.PokeNext(claim);
            return queued;
        }

        /// <summary>A stack as the client wrote it: the save version byte, then the item data.</summary>
        public static byte[] SerialiseItem(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null || item.m_dropPrefab == null) return null;

            var package = new ZPackage();
            package.Write((byte)Version.Item.NewCheats);
            item.Save(package);
            return package.GetArray();
        }

        private static ItemDrop.ItemData ReadItem(byte[] blob)
        {
            if (blob == null || blob.Length == 0) return null;

            var package = new ZPackage(blob);
            var version = (Version.Item)package.ReadByte();
            var data = new ItemDrop.ItemData();
            var hash = ItemDrop.ItemData.Load(package, data, version);
            if (hash == 0) return null;

            var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(hash) : null;
            if (prefab == null && ZNetScene.instance != null) prefab = ZNetScene.instance.GetPrefab(hash);
            if (prefab == null) return null;

            var source = prefab.GetComponent<ItemDrop>();
            if (source == null || source.m_itemData == null || source.m_itemData.m_shared == null) return null;

            data.m_dropPrefab = prefab;
            data.m_shared = source.m_itemData.m_shared;
            return data;
        }

        private static int EnqueuePockets(Player player, Claim claim)
        {
            var inventory = player != null ? player.GetInventory() : null;
            if (inventory == null) return 0;

            var items = PocketItems(player);
            var queued = 0;
            foreach (var item in items)
            {
                FlowQueue.EnqueuePockets(claim, inventory, item);
                queued++;
            }

            Note("empty pockets queued=" + queued + " claim=" + claim.BowlId);
            return queued;
        }

        private static int EnqueueContainer(Container source, Claim claim)
        {
            var inventory = source.GetInventory();
            if (inventory == null) return 0;

            var queued = 0;
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;

                FlowQueue.EnqueueContainer(claim, source, item);
                queued++;
            }

            Note("tidy queued=" + queued + " claim=" + claim.BowlId + " source=" + source.name);
            return queued;
        }

        /// <summary>
        /// An order line two readers need: the debug log, for anyone watching the server output, and
        /// the diagnostics file, because that file is rewritten every boot — a scenario can assert on
        /// it without matching a line from an earlier run (LogOutput.log accumulates).
        /// </summary>
        private static void Note(string line)
        {
            Log.Debug("Orders", line);
            DiagnosticsFile.Line("[Orders] " + line);
        }

        private static bool IsHotbar(ItemDrop.ItemData item)
        {
            // The first inventory row is the hotbar; those stacks belong to the player's hands.
            return item.m_gridPos.y == 0;
        }

        /// <summary>
        /// The order rules, in one place, for both the caller's quick check and the server's decision.
        /// The server runs the same list with the identity it resolved from the connection.
        /// </summary>
        private static OrderResult Validate(Claim claim, OrderCaller caller, bool server)
        {
            if (!HusnisseConfig.AllowOrders.Value) return OrderResult.Nothing;

            if (claim == null || claim.Bowl == null) return OrderResult.NoClaim;
            if (claim.Suppressed) return OrderResult.Refuses;

            if (caller.Valid && !claim.Allows(caller.PlayerId)) return OrderResult.NotYours;

            if (caller.Valid && Vector3.Distance(caller.Position, claim.Center) > claim.Radius + 5f)
            {
                return OrderResult.TooFar;
            }

            if (!claim.Bowl.IsFed) return OrderResult.NotWorking;

            if (server && NisseFactory.EnsureNisse(claim.Bowl) == null) return OrderResult.NotWorking;

            return OrderResult.Queued;
        }
    }
}
