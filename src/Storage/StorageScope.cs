using System.Collections.Generic;
using Husnisse.Claims;

namespace Husnisse.Storage
{
    /// <summary>
    /// Read-only view of what a claim can supply. This is deliberately the shape the storage contract
    /// asks for (`Count`, `CanAfford`, a description), so 0.3 can grow `TryConsume` behind it without
    /// the internals hardening into something QI's integrations cannot use.
    /// </summary>
    public interface IStorageScope
    {
        string Description { get; }
        int Count(string itemName);
        bool CanAfford(IReadOnlyDictionary<string, int> requirements);
    }

    public class ClaimStorageScope : IStorageScope
    {
        private readonly Claim _claim;

        public ClaimStorageScope(Claim claim)
        {
            _claim = claim;
        }

        public string Description
        {
            get { return "claim " + _claim.BowlId; }
        }

        public int Count(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return 0;

            var total = 0;
            foreach (var container in ContainerIndex.InClaim(_claim))
            {
                var inventory = container.GetInventory();
                if (inventory == null) continue;
                total += inventory.CountItems(itemName);
            }
            return total;
        }

        public bool CanAfford(IReadOnlyDictionary<string, int> requirements)
        {
            if (requirements == null) return false;

            foreach (var pair in requirements)
            {
                if (Count(pair.Key) < pair.Value) return false;
            }
            return true;
        }
    }
}
