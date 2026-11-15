using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Orders;
using UnityEngine;

namespace Husnisse.Net
{
    /// <summary>
    /// Client-to-server order channel. The client never moves an item: it sends a request, the
    /// server validates it and puts the work on the claim's queue.
    ///
    /// Two things this channel has to get right on a dedicated server:
    ///
    /// - <b>Identity.</b> <c>ZNetPeer.m_playerID</c> is never populated on this game build — the
    ///   game registers <c>RPC_PlayerID</c> and nothing invokes it — so a guard on it drops every
    ///   client request with no log line at all. The identity the server can trust is the character
    ///   ZDO the peer is driving: <c>peer.m_characterID</c> is populated by the game's own handshake,
    ///   and that ZDO carries the same <c>playerID</c> the client computes locally.
    /// - <b>Acknowledgement.</b> The caller is told the verdict, not before it. The old code printed
    ///   "The nisse will see to it." the moment the request left, so a dropped request still looked
    ///   like work in progress.
    /// </summary>
    public static class OrderRpc
    {
        private const string Method = "Husnisse_Order";
        private const string AckMethod = "Husnisse_OrderAck";
        private const string HandoverMethod = "Husnisse_Handover";
        private const string HandoverResultMethod = "Husnisse_HandoverResult";

        /// <summary>More stacks than a trip could ever carry; a handover beyond this is refused.</summary>
        private const int MaxHandover = 40;

        private static bool _registered;

        public static void Register()
        {
            if (_registered || ZRoutedRpc.instance == null) return;

            ZRoutedRpc.instance.Register<ZPackage>(Method, HandleOrder);
            ZRoutedRpc.instance.Register<ZPackage>(AckMethod, HandleAck);
            ZRoutedRpc.instance.Register<ZPackage>(HandoverMethod, HandleHandover);
            ZRoutedRpc.instance.Register<ZPackage>(HandoverResultMethod, HandleHandoverResult);

            _registered = true;
            Log.Info("order RPC registered");
        }

        /// <summary>
        /// Asks the server for an order. Returns the verdict when the caller is the server itself
        /// (host or single-player) and null when the request went over the wire, where the answer
        /// arrives later as an acknowledgment.
        /// </summary>
        public static OrderVerdict? Send(OrderKind kind, ZDOID bowlId, ZDOID containerId, long playerId)
        {
            if (ZNet.instance == null) return OrderVerdict.Of(OrderResult.NoClaim);

            Register();

            var package = new ZPackage();
            package.Write((byte)kind);
            package.Write(bowlId);
            package.Write(containerId);
            package.Write(playerId);

            if (ZNet.instance.IsServer())
            {
                // The package was just written; BinaryWriter still holds the tail in its buffer, so a
                // plain rewind reads a stream that is shorter than the package and throws "Unable to
                // read beyond the end of the stream" — which is how a host's own order used to die.
                package.Flush();
                package.SetPos(0);
                return Execute(0L, package);
            }

            if (ZRoutedRpc.instance == null) return OrderVerdict.Of(OrderResult.NoClaim);
            ZRoutedRpc.instance.InvokeRoutedRPC(Method, package);
            return null;
        }

        // ------------------------------------------------------------------ identity

        /// <summary>
        /// The player id behind a connected peer, read from the character ZDO the peer drives. The
        /// package's own claim is only ever compared against this, never trusted.
        /// </summary>
        public static long PlayerIdOf(ZNetPeer peer)
        {
            if (peer == null) return 0L;
            if (peer.m_playerID != 0L) return peer.m_playerID;
            if (ZDOMan.instance == null || peer.m_characterID.IsNone()) return 0L;

            var character = ZDOMan.instance.GetZDO(peer.m_characterID);
            return character != null ? character.GetLong(ZDOVars.s_playerID, 0L) : 0L;
        }

        /// <summary>
        /// Who is asking. <paramref name="senderUid"/> 0 is this machine — the host or a single-player
        /// game. Anything else is a peer, and its identity must be readable from its character ZDO; a
        /// package that names a different player is refused rather than obeyed.
        /// </summary>
        public static OrderCaller ResolveCaller(long senderUid, long claimedPlayerId)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return OrderCaller.Invalid;

            if (senderUid == 0L)
            {
                var local = Player.m_localPlayer;
                if (local == null) return OrderCaller.Invalid;

                var caller = OrderCaller.Local(local);
                var id = local.GetPlayerID();
                if (id == 0L) caller.PlayerId = claimedPlayerId;
                return caller;
            }

            return CallerFor(ZNet.instance.GetPeer(senderUid), claimedPlayerId);
        }

        /// <summary>
        /// The identity policy on its own, so it can be exercised without a second machine: a peer is
        /// named by the character ZDO it drives, and the id in the package may only agree with it.
        /// </summary>
        public static OrderCaller CallerFor(ZNetPeer peer, long claimedPlayerId)
        {
            if (peer == null) return OrderCaller.Invalid;

            var playerId = PlayerIdOf(peer);
            if (playerId == 0L) return OrderCaller.Invalid;
            if (claimedPlayerId != 0L && claimedPlayerId != playerId) return OrderCaller.Invalid;

            var live = Player.GetPlayer(playerId);
            return new OrderCaller
            {
                PlayerId = playerId,
                Player = live,
                PeerUid = peer.m_uid,
                Position = live != null ? live.transform.position : peer.GetRefPos(),
            };
        }

        // ------------------------------------------------------------------ order

        private static void HandleOrder(long sender, ZPackage package)
        {
            Execute(sender, package);
        }

        private static OrderVerdict Execute(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer() || package == null)
            {
                return OrderVerdict.Of(OrderResult.NoClaim);
            }

            OrderKind kind;
            ZDOID bowlId;
            ZDOID containerId;
            long claimed;

            try
            {
                kind = (OrderKind)package.ReadByte();
                bowlId = package.ReadZDOID();
                containerId = package.ReadZDOID();
                claimed = package.ReadLong();
            }
            catch (System.Exception ex)
            {
                Log.Warn("order RPC unreadable: " + ex.Message);
                return OrderVerdict.Of(OrderResult.Refuses);
            }

            var caller = ResolveCaller(sender, claimed);
            if (!caller.Valid)
            {
                Log.Warn("order RPC dropped: sender=" + sender + " claimed=" + claimed +
                         " (the connection has no readable player identity)");
                return OrderVerdict.Of(OrderResult.NotYours);
            }

            var verdict = OrderService.ExecuteOnServer(caller, bowlId, kind, containerId);

            if (sender != 0L)
            {
                if (verdict.Result == OrderResult.Handover) AskHandover(sender, bowlId, caller.PlayerId);
                else Ack(sender, kind, verdict);
            }

            return verdict;
        }

        private static void Ack(long uid, OrderKind kind, OrderVerdict verdict)
        {
            if (uid == 0L || ZRoutedRpc.instance == null) return;

            var package = new ZPackage();
            package.Write((byte)kind);
            package.Write((byte)verdict.Result);
            package.Write(verdict.Count);
            package.Write(verdict.Feeding);

            ZRoutedRpc.instance.InvokeRoutedRPC(uid, AckMethod, package);
        }

        private static void HandleAck(long sender, ZPackage package)
        {
            if (package == null) return;
            if (Player.m_localPlayer == null) return;

            try
            {
                var kind = (OrderKind)package.ReadByte();
                var verdict = new OrderVerdict
                {
                    Result = (OrderResult)package.ReadByte(),
                    Count = package.ReadInt(),
                    Feeding = package.ReadBool(),
                };

                Log.Debug("Net", "order ack kind=" + kind + " result=" + verdict.Result + " count=" + verdict.Count);
                OrderService.Report(kind, verdict);
            }
            catch (System.Exception ex)
            {
                Log.Warn("order ack unreadable: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ handover

        /// <summary>
        /// The server accepted an Empty Pockets order but cannot see the caller's inventory, so it asks
        /// the caller's own client to hand the stacks over.
        /// </summary>
        private static void AskHandover(long uid, ZDOID bowlId, long playerId)
        {
            if (uid == 0L || ZRoutedRpc.instance == null) return;

            var package = new ZPackage();
            package.Write(bowlId);
            package.Write(playerId);

            ZRoutedRpc.instance.InvokeRoutedRPC(uid, HandoverMethod, package);
            Log.Debug("Orders", "handover asked of peer=" + uid + " player=" + playerId + " claim=" + bowlId);
        }

        private static void HandleHandover(long sender, ZPackage package)
        {
            if (package == null || ZNet.instance == null || ZNet.instance.IsServer()) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            ZDOID bowlId;
            long requestedFor;
            try
            {
                bowlId = package.ReadZDOID();
                requestedFor = package.ReadLong();
            }
            catch (System.Exception ex)
            {
                Log.Warn("handover request unreadable: " + ex.Message);
                return;
            }

            var playerId = player.GetPlayerID();
            if (requestedFor != 0L && requestedFor != playerId)
            {
                Log.Debug("Orders", "handover ignored: asked for " + requestedFor + ", this player is " + playerId);
                return;
            }

            if (ClaimRegistry.Find(bowlId) == null) return;

            var inventory = player.GetInventory();
            if (inventory == null) return;

            var handed = new List<byte[]>();
            foreach (var item in OrderService.PocketItems(player))
            {
                // Serialise before removing: RemoveItem empties the stack it is given.
                var blob = OrderService.SerialiseItem(item);
                if (blob == null) continue;
                if (!inventory.RemoveItem(item, item.m_stack)) continue;
                handed.Add(blob);
            }

            var result = new ZPackage();
            result.Write(bowlId);
            result.Write(playerId);
            result.Write(handed.Count);
            foreach (var blob in handed) result.Write(blob);

            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(HandoverResultMethod, result);

            Log.Debug("Orders", "handover sent stacks=" + handed.Count + " claim=" + bowlId);
        }

        private static void HandleHandoverResult(long sender, ZPackage package)
        {
            if (package == null || ZNet.instance == null || !ZNet.instance.IsServer() || sender == 0L) return;

            ZDOID bowlId;
            long claimed;
            int count;

            try
            {
                bowlId = package.ReadZDOID();
                claimed = package.ReadLong();
                count = Mathf.Clamp(package.ReadInt(), 0, MaxHandover);
            }
            catch (System.Exception ex)
            {
                Log.Warn("handover result unreadable: " + ex.Message);
                return;
            }

            var caller = ResolveCaller(sender, claimed);
            if (!caller.Valid)
            {
                Log.Warn("handover dropped: sender=" + sender + " claimed=" + claimed);
                return;
            }

            var claim = ClaimRegistry.Find(bowlId);
            if (claim == null || claim.Bowl == null)
            {
                Ack(sender, OrderKind.EmptyPockets, OrderVerdict.Of(OrderResult.NoClaim));
                return;
            }

            var queued = OrderService.MaterialisePockets(claim, package, count);
            Ack(sender, OrderKind.EmptyPockets, new OrderVerdict
            {
                Result = queued > 0 ? OrderResult.Queued : OrderResult.Nothing,
                Count = queued,
            });
        }
    }
}
