using System;
using System.Collections.Generic;
using Husnisse.Bowl;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Flow;
using Husnisse.Net;
using Husnisse.Orders;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Contract
{
    /// <summary>
    /// Craft supply: what a recipe still needs, and the errand that brings it.
    ///
    /// The spend order the storage contract fixes is pockets first, then the claim. A chest's
    /// contents belong to its ZDO owner, so a client cannot debit one inside a synchronous
    /// `TryConsume`; what it can do is ask the nisse, whose errand is the claim leg made visible.
    /// The nisse empties the planned stacks while it stands at each chest, carries them to the bench
    /// and hands them over, and the craft that asked spends the player's own pockets — which now
    /// hold them. Nothing is taken before the nisse is at the chest and nothing arrives before it is
    /// at the bench, so the cause of every move is a creature the player can see.
    /// </summary>
    public static class SupplyService
    {
        /// <summary>The token space for errands. A client only needs it unique among its own asks.</summary>
        private static long _nextToken = Environment.TickCount;

        /// <summary>What the last request was told, for the acknowledgment line and the log.</summary>
        public static string LastNote { get; private set; } = string.Empty;

        // ------------------------------------------------------------------ planning

        /// <summary>
        /// The materials a craft still needs from storage: pockets first, then the claim, mirroring
        /// vanilla's own requirement check (the best single quality tier, multiplied by the batch).
        /// An empty list means the pockets can already pay, and the craft should be left alone.
        /// </summary>
        public static List<KeyValuePair<string, int>> Shortfall(Player player, Recipe recipe, int quality, int batch)
        {
            var items = new List<KeyValuePair<string, int>>();
            if (player == null || recipe == null || recipe.m_item == null) return items;
            if (recipe.m_requireOnlyOneIngredient) return items;

            var inventory = player.GetInventory();
            if (inventory == null) return items;

            var station = player.GetCurrentCraftingStation();
            var upgrader = station != null && station.m_upgrader;

            var resources = recipe.m_resources;
            for (var i = 0; i < resources.Length; i++)
            {
                var requirement = resources[i];
                if (requirement == null || requirement.m_resItem == null) continue;
                if (requirement.m_upgraderResource != upgrader) continue;

                var shared = requirement.m_resItem.m_itemData != null ? requirement.m_resItem.m_itemData.m_shared : null;
                if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;

                var need = requirement.GetAmount(Mathf.Max(1, quality)) * Mathf.Max(1, batch);
                if (need <= 0) continue;

                var carried = BestQualityCount(inventory, shared.m_name, shared.m_maxQuality);
                if (carried >= need) continue;

                Add(items, shared.m_name, need - carried);
            }

            return items;
        }

        /// <summary>The same question for a build piece's requirements (PlanBuild's blueprint cost).</summary>
        public static List<KeyValuePair<string, int>> Shortfall(Piece.Requirement[] resources, int quality, int batch,
                                                               Player player)
        {
            var items = new List<KeyValuePair<string, int>>();
            if (player == null || resources == null) return items;

            var inventory = player.GetInventory();
            if (inventory == null) return items;

            var station = player.GetCurrentCraftingStation();
            var upgrader = station != null && station.m_upgrader;

            for (var i = 0; i < resources.Length; i++)
            {
                var requirement = resources[i];
                if (requirement == null || requirement.m_resItem == null) continue;
                if (requirement.m_upgraderResource != upgrader) continue;

                var shared = requirement.m_resItem.m_itemData != null ? requirement.m_resItem.m_itemData.m_shared : null;
                if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;

                var need = requirement.GetAmount(Mathf.Max(1, quality)) * Mathf.Max(1, batch);
                if (need <= 0) continue;

                var carried = BestQualityCount(inventory, shared.m_name, shared.m_maxQuality);
                if (carried >= need) continue;

                Add(items, shared.m_name, need - carried);
            }

            return items;
        }

        /// <summary>Vanilla's own rule: one quality tier must cover the whole amount, not a sum.</summary>
        private static int BestQualityCount(Inventory inventory, string sharedName, int maxQuality)
        {
            var best = 0;
            for (var quality = 1; quality <= Mathf.Max(1, maxQuality); quality++)
            {
                var count = inventory.CountItems(sharedName, quality);
                if (count > best) best = count;
            }
            return best;
        }

        private static void Add(List<KeyValuePair<string, int>> items, string name, int amount)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Key != name) continue;
                items[i] = new KeyValuePair<string, int>(name, items[i].Value + amount);
                return;
            }
            items.Add(new KeyValuePair<string, int>(name, amount));
        }

        /// <summary>What the claim holds for a name, using the same containers the nisse may use.</summary>
        public static int ClaimCount(Player player, string sharedName)
        {
            var claim = ClaimFor(player);
            if (claim == null || string.IsNullOrEmpty(sharedName)) return 0;

            var total = 0;
            var containers = Eligible(claim);
            for (var i = 0; i < containers.Count; i++)
            {
                var inventory = containers[i].GetInventory();
                if (inventory != null) total += inventory.CountItems(sharedName, -1, true);
            }
            return total;
        }

        /// <summary>Every item in a shortfall that the claim can cover, all or nothing.</summary>
        public static bool CanCover(Player player, List<KeyValuePair<string, int>> items)
        {
            if (items == null || items.Count == 0) return false;

            var claim = ClaimFor(player);
            if (claim == null || claim.Suppressed) return false;
            if (!claim.Allows(player != null ? player.GetPlayerID() : 0L)) return false;

            for (var i = 0; i < items.Count; i++)
            {
                if (ClaimCount(player, items[i].Key) < items[i].Value) return false;
            }
            return true;
        }

        /// <summary>The claim a player is standing in, or null when there is none.</summary>
        public static Claim ClaimFor(Player player)
        {
            if (player == null) return null;
            return ClaimRegistry.FindFor(player.transform.position);
        }

        /// <summary>
        /// The containers a supply errand may draw from: everything inside the claim but the pantry
        /// bowl — that is the nisse's own food — with the per-container blacklist applied. A cart or
        /// a ship inside the claim is filtered out by the same container filter that governs filing.
        /// </summary>
        public static List<Container> Eligible(Claim claim)
        {
            var list = new List<Container>();
            if (claim == null) return list;

            var all = ContainerIndex.InClaim(claim);
            for (var i = 0; i < all.Count; i++)
            {
                var container = all[i];
                if (container == null) continue;
                if (container.GetComponent<BowlPiece>() != null) continue;
                if (ContainerNetwork.IsBlacklisted(container)) continue;
                list.Add(container);
            }
            return list;
        }

        // ------------------------------------------------------------------ client

        /// <summary>
        /// Client: ask the nisse for a load. The token is generated here and kept by the caller, so
        /// the acknowledgment and the handover can be matched to the craft that asked.
        /// </summary>
        public static FetchResult? Request(Player player, Claim claim, List<KeyValuePair<string, int>> items,
                                           out long token)
        {
            token = 0L;
            if (player == null || claim == null || items == null || items.Count == 0) return FetchResult.Nothing;
            if (HusnisseConfig.CraftSupply == null || !HusnisseConfig.CraftSupply.Value) return FetchResult.Disabled;

            token = ++_nextToken;
            if (token <= 0L) token = ++_nextToken;

            return FetchRpc.Request(claim.BowlId, player.GetPlayerID(), token, player.transform.position, items);
        }

        // ------------------------------------------------------------------ server

        /// <summary>
        /// Server: validate a request and start the errand. The plan is built from the server's own
        /// containers, so a client cannot name a chest that is not there or ask for more than one
        /// holds; nothing is removed here — the nisse does that when it reaches each chest.
        /// </summary>
        public static FetchResult Begin(OrderCaller caller, ZDOID bowlId, long token, Vector3 bench,
                                        List<KeyValuePair<string, int>> items)
        {
            LastNote = string.Empty;

            var verdict = Decide(caller, bowlId, token, bench, items);
            if (verdict != FetchResult.Accepted)
            {
                DiagnosticsFile.Line("[Supply] refused result=" + verdict + " note=\"" + (LastNote ?? string.Empty) +
                                     "\" player=" + caller.PlayerId + " claim=" + bowlId);
                Log.Debug("Flow", "supply refused: " + verdict + " (" + (LastNote ?? string.Empty) + ")");
            }

            return verdict;
        }

        private static FetchResult Decide(OrderCaller caller, ZDOID bowlId, long token, Vector3 bench,
                                          List<KeyValuePair<string, int>> items)
        {
            if (HusnisseConfig.CraftSupply == null || !HusnisseConfig.CraftSupply.Value)
            {
                LastNote = "craft supply is off";
                return FetchResult.Disabled;
            }

            if (!caller.Valid) { LastNote = "no caller"; return FetchResult.NotYours; }

            var claim = ClaimRegistry.Find(bowlId);
            if (claim == null || claim.Bowl == null) { LastNote = "no claim"; return FetchResult.NoClaim; }
            if (claim.Suppressed) { LastNote = "claim suppressed"; return FetchResult.NotYours; }
            if (!claim.Allows(caller.PlayerId)) { LastNote = "not permitted"; return FetchResult.NotYours; }

            if (Vector3.Distance(caller.Position, claim.Center) > claim.Radius + 8f)
            {
                LastNote = "caller outside the claim";
                return FetchResult.TooFar;
            }

            if (!claim.Bowl.IsFed) { LastNote = "the nisse is not fed"; return FetchResult.NotWorking; }

            var nisse = NisseFactory.EnsureNisse(claim.Bowl);
            if (nisse == null) { LastNote = "no nisse"; return FetchResult.NotWorking; }

            if (FetchRunner.Has(claim))
            {
                LastNote = "an errand is already running";
                return FetchResult.Busy;
            }

            if (items == null || items.Count == 0) { LastNote = "nothing asked for"; return FetchResult.Nothing; }

            var containers = Eligible(claim);
            if (containers.Count == 0) { LastNote = "no container in the claim"; return FetchResult.Nothing; }

            var picks = new List<Fetch.Pick>();
            for (var i = 0; i < items.Count; i++)
            {
                var name = items[i].Key;
                var amount = items[i].Value;
                if (string.IsNullOrEmpty(name) || amount <= 0) continue;

                var available = 0;
                for (var c = 0; c < containers.Count; c++)
                {
                    var inventory = containers[c].GetInventory();
                    if (inventory != null) available += inventory.CountItems(name, -1, true);
                }

                if (available < amount)
                {
                    LastNote = "missing " + (amount - available) + " of " + amount + " " + name +
                               " (the claim holds " + available + ")";
                    return FetchResult.Missing;
                }

                var left = amount;
                for (var c = 0; c < containers.Count && left > 0; c++)
                {
                    var inventory = containers[c].GetInventory();
                    if (inventory == null) continue;

                    var stacks = inventory.GetAllItems();
                    for (var s = 0; s < stacks.Count && left > 0; s++)
                    {
                        var stack = stacks[s];
                        if (stack == null || stack.m_shared == null) continue;
                        if (stack.m_shared.m_name != name || stack.m_stack <= 0) continue;

                        var take = Mathf.Min(stack.m_stack, left);
                        picks.Add(new Fetch.Pick { Container = containers[c], Item = stack, Amount = take });
                        left -= take;
                    }
                }
            }

            if (picks.Count == 0) { LastNote = "nothing to carry"; return FetchResult.Nothing; }

            var target = bench != Vector3.zero ? bench : caller.Position;
            var fetch = new Fetch(claim, nisse, caller.PlayerId, caller.PeerUid, token, target, picks);
            if (!FetchRunner.Start(claim, fetch))
            {
                LastNote = "the nisse is busy";
                return FetchResult.Busy;
            }

            LastNote = "stacks=" + picks.Count;
            Log.Debug("Flow", "supply accepted token=" + token + " player=" + caller.PlayerId +
                              " stacks=" + picks.Count + " claim=" + claim.BowlId);
            return FetchResult.Accepted;
        }
    }
}
