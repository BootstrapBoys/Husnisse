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

            // Pocket stack merge on sort: two half stacks of coal are one stack before the nisse
            // starts carrying them, so it does not spend a trip slot on each half.
            var merged = MergePockets(player);
            if (merged > 0) Note("pockets merged units=" + merged + " claim=" + claim.BowlId);

            if (PocketItems(player).Count == 0)
            {
                Report(player, OrderKind.EmptyPockets, OrderVerdict.Of(OrderResult.Nothing));
                return true;
            }

            var answer = OrderRpc.Send(OrderKind.EmptyPockets, claim.BowlId, ZDOID.None, caller.PlayerId);
            if (answer.HasValue) Report(player, OrderKind.EmptyPockets, answer.Value);
            return true;
        }

        /// <summary>
        /// Merges identical partial stacks among the stacks an order may carry, before anything is
        /// queued. Only the orderable stacks are merged: the hotbar belongs to the player's hands,
        /// and merging a pocket stack into a hotbar one would hide it from the order that just ran.
        ///
        /// Two stacks merge only when the game itself would stack them — same shared data, world
        /// level, quality, variant and durability — because a merge that changed a quality or a
        /// durability would be the one lossy thing a sort can do.
        /// </summary>
        public static int MergePockets(Player player)
        {
            var inventory = player != null ? player.GetInventory() : null;
            if (inventory == null) return 0;

            var items = PocketItems(player);
            var moved = 0;

            for (var i = 0; i < items.Count; i++)
            {
                var target = items[i];
                if (target == null || target.m_stack <= 0) continue;
                if (target.m_shared.m_maxStackSize <= 1) continue;

                for (var j = i + 1; j < items.Count; j++)
                {
                    var source = items[j];
                    if (source == null || source.m_stack <= 0 || ReferenceEquals(source, target)) continue;
                    if (!SameStack(target, source)) continue;

                    var room = target.m_shared.m_maxStackSize - target.m_stack;
                    if (room <= 0) break;

                    var take = Mathf.Min(room, source.m_stack);
                    if (take <= 0) continue;

                    target.m_stack += take;
                    source.m_stack -= take;
                    moved += take;

                    if (source.m_stack <= 0) inventory.RemoveItem(source);
                }
            }

            if (moved > 0 && inventory.m_onChanged != null) inventory.m_onChanged();
            return moved;
        }

        private static bool SameStack(ItemDrop.ItemData first, ItemDrop.ItemData second)
        {
            if (first.m_shared == null || second.m_shared == null) return false;
            if (!ReferenceEquals(first.m_shared, second.m_shared)) return false;
            if (first.m_quality != second.m_quality) return false;
            if (first.m_variant != second.m_variant) return false;
            if (first.m_worldLevel != second.m_worldLevel) return false;
            if (Mathf.Abs(first.m_durability - second.m_durability) > 0.01f) return false;
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

        /// <summary>
        /// Deposit-all: leave everything the player is carrying in a container.
        ///
        /// Which half of the feature runs is the chest's own state, not a mode the player picks.
        /// On a chest that is on the claim's network the request is Empty Pockets aimed at that
        /// chest — the nisse takes the stacks and files them by their rules at the queue's rate —
        /// because a player's inventory lives on the player's machine and a dedicated server has
        /// no copy of it. On any other chest the stacks go straight in, unsorted: that is the
        /// manual "dump it here and deal with it later" action, the player's own move, and it
        /// needs neither a fed nisse nor a queue.
        /// </summary>
        public static bool RequestDepositAll(Player player, Claim claim, Container container)
        {
            if (player == null || container == null) return false;
            if (!HusnisseConfig.AllowOrders.Value) return true;

            if (IsNetworkTarget(claim, container)) return RequestEmptyPockets(player, claim);
            return DumpPockets(player, container);
        }

        /// <summary>
        /// Whether deposit-all at this chest means "the nisse sorts it". A bowl is never a
        /// deposit target — its inventory refuses anything that is not food — and a chest that is
        /// off the network or outside the claim is simply a chest.
        /// </summary>
        private static bool IsNetworkTarget(Claim claim, Container container)
        {
            if (claim == null || container == null) return false;
            if (container.GetComponent<Bowl.BowlPiece>() != null) return false;
            if (!claim.Contains(container.transform.position)) return false;
            if (ContainerNetwork.IsBlacklisted(container)) return false;
            return ContainerNetwork.IsOnNetwork(container);
        }

        /// <summary>
        /// The off-network half: move every pocket stack into the chest as it is, stopping when the
        /// chest is full. Add first, remove second — the player's stack is the only copy until the
        /// chest holds it — so a refused add costs nothing.
        /// </summary>
        private static bool DumpPockets(Player player, Container container)
        {
            if (container.GetComponent<Bowl.BowlPiece>() != null) return false;

            var inventory = player != null ? player.GetInventory() : null;
            var target = container.GetInventory();
            if (inventory == null || target == null) return false;

            var moved = 0;
            var spilled = 0;

            foreach (var item in PocketItems(player))
            {
                if (item == null || item.m_shared == null) continue;

                var clone = item.Clone();
                if (!target.CanAddItem(clone) || !target.AddItem(clone))
                {
                    spilled++;
                    continue;
                }

                inventory.RemoveItem(item, item.m_stack);
                moved++;
            }

            if (moved > 0 && spilled > 0)
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderDepositFull, moved, spilled));
            else if (moved > 0)
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderDeposited, moved));
            else if (spilled > 0)
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNoRoom));
            else
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderPocketsEmpty));

            Note("deposit-all dumped=" + moved + " left=" + spilled + " container=" + container.name +
                 " at=" + Where(container.transform.position));
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

            // Every verdict is worth a line in the diagnostics file: an order that "did nothing" is
            // the hardest thing to debug from a player's report, and the reason is one word.
            Note("order " + kind + " -> " + verdict.Result +
                 " player=" + player.GetPlayerID() +
                 " count=" + verdict.Count +
                 (kind == OrderKind.ToggleFeeding ? " feeding=" + verdict.Feeding : string.Empty));

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
            var network = StorageRouter.NetworkCandidates(claim);

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

                // The drop exists either way — the player handed it over and can see it — but only
                // one the network accepts is queued, so an unstorable stack lies by the bowl instead
                // of cycling through the queue for ever.
                var decision = StorageRouter.Decide(claim, network, item, null, null);
                if (decision.Found) FlowQueue.EnqueueDrop(claim, drop);
                else Note("empty pockets left on the ground: " + item.m_shared.m_name + " why=" + decision.Why);
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
            var network = StorageRouter.NetworkCandidates(claim);
            var queued = 0;
            var unwanted = 0;
            foreach (var item in items)
            {
                // Only stacks with somewhere to go are queued: a stack nothing accepts would be a
                // promise the nisse cannot keep, so it stays in the player's pocket and the answer
                // says how many really move.
                var decision = StorageRouter.Decide(claim, network, item, null, null);
                if (!decision.Found)
                {
                    unwanted++;
                    DiagnosticsFile.Line("[Flow] unmatched " + item.m_shared.m_name +
                                          " why=" + decision.Why + " from=pockets claim=" + claim.BowlId);
                    continue;
                }

                FlowQueue.EnqueuePockets(claim, inventory, item);
                queued++;
            }

            Note("empty pockets queued=" + queued + " unmatched=" + unwanted + " claim=" + claim.BowlId);
            return queued;
        }

        private static int EnqueueContainer(Container source, Claim claim)
        {
            var inventory = source.GetInventory();
            if (inventory == null) return 0;

            var network = StorageRouter.NetworkCandidates(claim);
            var queued = 0;
            var unwanted = 0;
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;

                var decision = StorageRouter.Decide(claim, network, item, null, source);
                if (!decision.Found)
                {
                    unwanted++;
                    DiagnosticsFile.Line("[Flow] unmatched " + item.m_shared.m_name +
                                          " why=" + decision.Why + " from=" + source.name + " claim=" + claim.BowlId);
                    continue;
                }

                FlowQueue.EnqueueContainer(claim, source, item);
                queued++;
            }

            Note("tidy queued=" + queued + " unmatched=" + unwanted + " claim=" + claim.BowlId +
                 " source=" + source.name);
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

        private static string Where(Vector3 point)
        {
            return "(" + point.x.ToString("0.#") + "," + point.z.ToString("0.#") + ")";
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
