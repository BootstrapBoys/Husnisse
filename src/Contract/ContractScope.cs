using System;
using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Contract
{
    /// <summary>
    /// Source 4: the claim the player is standing in. The nisse's own containers are the storage, so
    /// the claim leg asks exactly the question the nisse asks when it files a stack — the same
    /// filter, the same blacklist, the same ward ground rule — and adds nothing of its own. A claim
    /// that refuses the player, or that is suppressed, contributes nothing: the contract says a
    /// source that is not permitted contributes nothing, not that it warns.
    /// </summary>
    public sealed class ClaimSupplySource : IContractSource
    {
        private readonly Claim _claim;
        private readonly List<Container> _containers;
        private readonly string _refusal;

        public ClaimSupplySource(Claim claim, List<Container> containers, string refusal)
        {
            _claim = claim;
            _containers = containers;
            _refusal = refusal;
        }

        public string Name
        {
            get { return "claim"; }
        }

        public bool Present
        {
            get { return _claim != null && _containers != null; }
        }

        public int Count(string sharedName)
        {
            if (!Present || string.IsNullOrEmpty(sharedName)) return 0;

            var total = 0;
            for (var i = 0; i < _containers.Count; i++)
            {
                var container = _containers[i];
                if (container == null) continue;
                var inventory = container.GetInventory();
                if (inventory == null) continue;
                total += inventory.CountItems(sharedName, -1, true);
            }

            return total;
        }

        public string Describe()
        {
            if (_claim == null) return "-";
            if (_containers == null) return "refused=" + (_refusal ?? "unknown");

            var stacks = 0;
            for (var i = 0; i < _containers.Count; i++)
            {
                var container = _containers[i];
                var inventory = container != null ? container.GetInventory() : null;
                if (inventory != null) stacks += inventory.NrOfItems();
            }

            return "containers=" + _containers.Count + " stacks=" + stacks;
        }
    }

    /// <summary>
    /// What the contract can reach for one player right now. A scope is not a distance: it is the
    /// sources in the contract's order, resolved once, and it must not change inside a single query
    /// — which is why it is rebuilt on a timer rather than read per source.
    /// </summary>
    public sealed class ContractScope
    {
        public Player Player { get; private set; }
        public Claim Claim { get; private set; }
        public List<IContractSource> Sources { get; private set; }
        public string Description { get; private set; }
        public string ClaimNote { get; private set; }

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        public static ContractScope Build(Player player)
        {
            var scope = new ContractScope
            {
                Player = player,
                Sources = new List<IContractSource>(4)
            };

            scope.Sources.Add(new PocketSource(scope));
            scope.Sources.Add(new SlotSource(scope));
            scope.Sources.Add(new BackpackSource(SourceBindings.EquippedBackpack(player)));

            scope.ResolveClaim();

            return scope;
        }

        private PlayerHoldings _holdings;

        /// <summary>
        /// The player's holdings, read on the first question of this scope rather than at build
        /// time: the claim leg reads the containers live, and a pocket snapshot taken half a second
        /// earlier would let one answer count the same stack twice while it is being moved.
        /// </summary>
        public PlayerHoldings Holdings
        {
            get { return _holdings ?? (_holdings = PlayerHoldings.Read(Player)); }
        }

        /// <summary>
        /// The claim leg. `ClaimRegistry.FindFor` answers the claim that owns the point; the player
        /// must also be permitted on it, because a claim the player cannot use is not a source.
        /// </summary>
        private void ResolveClaim()
        {
            var player = Player;
            if (player == null)
            {
                ClaimNote = "no player";
                Description = "none (no local player)";
                Sources.Add(new ClaimSupplySource(null, null, "no player"));
                return;
            }

            var position = player.transform.position;
            var claim = ClaimRegistry.FindFor(position);
            if (claim == null)
            {
                ClaimNote = "outside";
                Description = "no claim at " + Fmt(position);
                Sources.Add(new ClaimSupplySource(null, null, "outside every claim"));
                return;
            }

            Claim = claim;

            var playerId = player.GetPlayerID();
            if (!claim.Allows(playerId))
            {
                ClaimNote = "not permitted";
                Description = "claim " + claim.BowlId + " at r=" + claim.Radius.ToString("0") +
                              " owner=" + claim.OwnerId + " permitted=False";
                Sources.Add(new ClaimSupplySource(claim, null, "not permitted"));
                return;
            }

            var containers = new List<Container>();
            var all = ContainerIndex.InClaim(claim);
            for (var i = 0; i < all.Count; i++)
            {
                var container = all[i];
                if (container == null) continue;
                if (ContainerNetwork.IsBlacklisted(container)) continue;

                // The contract answers a crafting question, so a chest whose owner has switched
                // crafting off as a supply purpose is not counted: a number the errand cannot pay
                // would be a promise, and 0.5 spent a release learning not to make one.
                if (!ContainerNetwork.RuleOf(container).Supplies(SupplyPurpose.Crafting)) continue;

                containers.Add(container);
            }

            ClaimNote = "permitted";
            Description = "claim " + claim.BowlId + " at " + Fmt(claim.Center) +
                          " r=" + claim.Radius.ToString("0") +
                          " source=" + claim.SourceName +
                          " owner=" + claim.OwnerId +
                          " containers=" + containers.Count +
                          " permitted=True";

            Sources.Add(new ClaimSupplySource(claim, containers, null));
        }

        /// <summary>Every source, in contract order. One dictionary entry per name per scope.</summary>
        public int Count(string sharedName)
        {
            if (string.IsNullOrEmpty(sharedName)) return 0;

            int cached;
            if (_counts.TryGetValue(sharedName, out cached)) return cached;

            var total = 0;
            for (var i = 0; i < Sources.Count; i++)
            {
                var source = Sources[i];
                if (source == null || !source.Present) continue;
                total += source.Count(sharedName);
            }

            _counts[sharedName] = total;
            return total;
        }

        /// <summary>Everything the player carries — sources 1 to 3, without the claim.</summary>
        public int Carried(string sharedName)
        {
            var total = 0;
            for (var i = 0; i < Sources.Count; i++)
            {
                var source = Sources[i];
                if (source == null || !source.Present || source.Name == "claim") continue;
                total += source.Count(sharedName);
            }
            return total;
        }

        /// <summary>The claim leg alone. Used by the split display and by the harness.</summary>
        public int ClaimCount(string sharedName)
        {
            for (var i = 0; i < Sources.Count; i++)
            {
                var source = Sources[i];
                if (source != null && source.Name == "claim" && source.Present) return source.Count(sharedName);
            }
            return 0;
        }

        /// <summary>
        /// One named source on its own — the spend path needs to know how much of an amount the
        /// pockets can pay before the slots, and the slots before the pack.
        /// </summary>
        public int CountIn(string sourceName, string sharedName)
        {
            if (string.IsNullOrEmpty(sourceName) || string.IsNullOrEmpty(sharedName)) return 0;

            for (var i = 0; i < Sources.Count; i++)
            {
                var source = Sources[i];
                if (source == null || !source.Present || source.Name != sourceName) continue;
                return source.Count(sharedName);
            }
            return 0;
        }

        private static string Fmt(Vector3 point)
        {
            return point.x.ToString("0.#") + "," + point.z.ToString("0.#");
        }
    }
}
