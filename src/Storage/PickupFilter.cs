using System;
using System.Collections.Generic;
using System.Text;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Util;

namespace Husnisse.Storage
{
    /// <summary>
    /// What the nisse picks up off the floor of one claim, per item type. The filter is per claim
    /// and lives in the bowl's ZDO, so every peer reads the same answer and the server's scan and a
    /// player's ledger agree by construction.
    ///
    /// Absent means "all eight presets" — a bowl placed before 0.3.1 keeps collecting everything.
    /// An item no preset classifies (weapons, armour, gear) is always collected: the filter is a
    /// per-type choice and none of the toggles covers gear, so dropping it would be a silent
    /// behaviour change for a chest of spare axes.
    ///
    /// Ordering a stack out of a container or handing pockets over is not pickup: the player chose
    /// that move, so the filter never applies to it.
    /// </summary>
    public sealed class PickupFilter
    {
        public const string Version = "v1";

        private readonly HashSet<ItemCategory> _allowed;

        private PickupFilter(HashSet<ItemCategory> allowed)
        {
            _allowed = allowed ?? new HashSet<ItemCategory>();
        }

        public static PickupFilter All()
        {
            var allowed = new HashSet<ItemCategory>();
            foreach (var category in ContainerRule.Presets) allowed.Add(category);
            return new PickupFilter(allowed);
        }

        public static PickupFilter None()
        {
            return new PickupFilter(new HashSet<ItemCategory>());
        }

        public bool Allows(ItemCategory category)
        {
            if (category == ItemCategory.None) return true;
            return _allowed.Contains(category);
        }

        public bool IsAll
        {
            get { return _allowed.Count == ContainerRule.Presets.Length; }
        }

        public int Count
        {
            get { return _allowed.Count; }
        }

        public PickupFilter With(ItemCategory category, bool on)
        {
            if (category == ItemCategory.None) return this;
            var allowed = new HashSet<ItemCategory>(_allowed);
            if (on) allowed.Add(category);
            else allowed.Remove(category);
            return new PickupFilter(allowed);
        }

        /// <summary>All is the absent key; anything else is the allowed list, so a bowl that never
        /// touched the ledger stores nothing at all.</summary>
        public string Encode()
        {
            if (IsAll) return string.Empty;

            var text = new StringBuilder(Version);
            text.Append(";c=");
            var first = true;
            foreach (var category in ContainerRule.Presets)
            {
                if (!_allowed.Contains(category)) continue;
                if (!first) text.Append(',');
                text.Append(ContainerRule.CategoryToken(category));
                first = false;
            }
            return text.ToString();
        }

        /// <summary>
        /// A value this build cannot read is "all", never "none": a newer filter must not stop the
        /// nisse collecting, and silently narrowing would look like a broken claim.
        /// </summary>
        public static PickupFilter Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return All();

            var parts = encoded.Split(';');
            if (parts.Length < 2 || !string.Equals(parts[0], Version, StringComparison.Ordinal))
            {
                Log.Debug("Ledger", "pickup filter not understood, treating as all: " + encoded);
                return All();
            }

            var allowed = new HashSet<ItemCategory>();
            for (var i = 1; i < parts.Length; i++)
            {
                var pair = parts[i];
                var split = pair.IndexOf('=');
                if (split <= 0 || pair.Substring(0, split) != "c") continue;

                var value = pair.Substring(split + 1);
                if (value.Length == 0) continue;

                foreach (var token in value.Split(','))
                {
                    var category = ContainerRule.CategoryFromToken(token.Trim());
                    if (category == ItemCategory.None)
                    {
                        // One unknown token and the whole answer is untrustworthy.
                        Log.Debug("Ledger", "pickup filter has an unknown category, treating as all: " + encoded);
                        return All();
                    }
                    allowed.Add(category);
                }
            }

            return new PickupFilter(allowed);
        }

        public string Describe()
        {
            if (IsAll) return "all";
            if (_allowed.Count == 0) return "none";

            var text = new StringBuilder();
            foreach (var category in ContainerRule.Presets)
            {
                if (!_allowed.Contains(category)) continue;
                if (text.Length > 0) text.Append(',');
                text.Append(ContainerRule.CategoryToken(category));
            }
            return text.ToString();
        }

        public static ZDO ZdoOf(Claim claim)
        {
            if (claim == null || claim.Bowl == null) return null;
            var view = claim.Bowl.GetComponent<ZNetView>();
            return view != null ? view.GetZDO() : null;
        }

        public static PickupFilter Of(Claim claim)
        {
            var zdo = ZdoOf(claim);
            if (zdo == null) return All();
            return Decode(zdo.GetString(ZdoKeys.ClaimPickup, string.Empty));
        }

        /// <summary>Whether a loose stack is picked up by this claim's nisse.</summary>
        public static bool Allows(Claim claim, ItemDrop.ItemData item)
        {
            if (claim == null) return false;
            return Of(claim).Allows(CategoryMap.Of(item));
        }

        /// <summary>The server's write path; the bowl's ZDO is the only copy.</summary>
        public static bool Write(Claim claim, PickupFilter filter, string who)
        {
            var zdo = ZdoOf(claim);
            if (zdo == null) return false;

            var encoded = (filter ?? All()).Encode();
            if (encoded.Length == 0) zdo.Set(ZdoKeys.ClaimPickup, string.Empty);
            else zdo.Set(ZdoKeys.ClaimPickup, encoded);

            Log.Debug("Ledger", "pickup filter " + (filter ?? All()).Describe() + " claim=" +
                               claim.BowlId + " by " + who);
            return true;
        }
    }
}
