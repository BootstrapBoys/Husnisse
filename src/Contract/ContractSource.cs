using System;
using System.Collections.Generic;

namespace Husnisse.Contract
{
    /// <summary>
    /// One place a contract query may draw from. The order of the list is the contract's order —
    /// pockets, extra slots, equipped backpack, claim — and it is the same order a future
    /// `TryConsume` must spend in, which is why the sources are objects rather than four sums.
    /// </summary>
    public interface IContractSource
    {
        /// <summary>pockets · slots · backpack · claim — the contract's own names.</summary>
        string Name { get; }

        /// <summary>False when the source is not there at all (mod absent, no pack equipped, no claim).</summary>
        bool Present { get; }

        /// <summary>How many items with this shared name the source holds.</summary>
        int Count(string sharedName);

        /// <summary>One line for diagnostics, e.g. `pockets stacks=3 items=7`.</summary>
        string Describe();
    }

    /// <summary>
    /// The player's own inventory split into the contract's first two sources. ExtraSlots extends the
    /// same <see cref="Inventory"/>, so the split is a partition, not a second count: every item is
    /// either an ordinary row or an extra row, and nothing is counted twice.
    /// </summary>
    public sealed class PlayerHoldings
    {
        private readonly Dictionary<string, int> _pockets = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _slots = new Dictionary<string, int>(StringComparer.Ordinal);

        public int PocketStacks { get; private set; }
        public int PocketItems { get; private set; }
        public int SlotStacks { get; private set; }
        public int SlotItems { get; private set; }

        public static PlayerHoldings Read(Player player)
        {
            var inventory = player != null ? player.GetInventory() : null;
            if (inventory == null) return null;

            var holdings = new PlayerHoldings();

            var items = inventory.GetAllItems();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                var shared = item.m_shared;
                if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;

                var inSlot = SourceBindings.IsExtraSlotItem(item);
                var counts = inSlot ? holdings._slots : holdings._pockets;

                int current;
                counts.TryGetValue(shared.m_name, out current);
                counts[shared.m_name] = current + item.m_stack;

                if (inSlot)
                {
                    holdings.SlotStacks++;
                    holdings.SlotItems += item.m_stack;
                }
                else
                {
                    holdings.PocketStacks++;
                    holdings.PocketItems += item.m_stack;
                }
            }

            return holdings;
        }

        public int CountPockets(string sharedName)
        {
            int value;
            return !string.IsNullOrEmpty(sharedName) && _pockets.TryGetValue(sharedName, out value) ? value : 0;
        }

        public int CountSlots(string sharedName)
        {
            int value;
            return !string.IsNullOrEmpty(sharedName) && _slots.TryGetValue(sharedName, out value) ? value : 0;
        }
    }

    /// <summary>Source 1: the player's ordinary inventory rows.</summary>
    public sealed class PocketSource : IContractSource
    {
        private readonly ContractScope _scope;

        public PocketSource(ContractScope scope)
        {
            _scope = scope;
        }

        public string Name
        {
            get { return "pockets"; }
        }

        public bool Present
        {
            get { return _scope != null && _scope.Holdings != null; }
        }

        public int Count(string sharedName)
        {
            var holdings = _scope != null ? _scope.Holdings : null;
            return holdings == null ? 0 : holdings.CountPockets(sharedName);
        }

        public string Describe()
        {
            var holdings = _scope != null ? _scope.Holdings : null;
            return holdings == null ? "-" : "stacks=" + holdings.PocketStacks + " items=" + holdings.PocketItems;
        }
    }

    /// <summary>Source 2: ExtraSlots rows, when that mod is installed.</summary>
    public sealed class SlotSource : IContractSource
    {
        private readonly ContractScope _scope;

        public SlotSource(ContractScope scope)
        {
            _scope = scope;
        }

        public string Name
        {
            get { return "slots"; }
        }

        public bool Present
        {
            get { return _scope != null && _scope.Holdings != null && SourceBindings.SlotsBound; }
        }

        public int Count(string sharedName)
        {
            if (!Present) return 0;
            return _scope.Holdings.CountSlots(sharedName);
        }

        public string Describe()
        {
            if (!Present) return "-";
            var holdings = _scope.Holdings;
            return "stacks=" + holdings.SlotStacks + " items=" + holdings.SlotItems;
        }
    }

    /// <summary>Source 3: the equipped AdventureBackpacks container, when that mod is installed.</summary>
    public sealed class BackpackSource : IContractSource
    {
        private readonly Inventory _backpack;

        public BackpackSource(Inventory backpack)
        {
            _backpack = backpack;
        }

        public string Name
        {
            get { return "backpack"; }
        }

        public bool Present
        {
            get { return _backpack != null; }
        }

        public int Count(string sharedName)
        {
            if (_backpack == null || string.IsNullOrEmpty(sharedName)) return 0;
            return _backpack.CountItems(sharedName, -1, true);
        }

        public string Describe()
        {
            if (_backpack == null) return "-";

            var stacks = 0;
            var items = 0;
            var all = _backpack.GetAllItems();
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;
                stacks++;
                items += all[i].m_stack;
            }

            return "stacks=" + stacks + " items=" + items;
        }
    }
}
