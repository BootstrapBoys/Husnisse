using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// The container as a member of a claim's network: opted in or not, blacklisted or not, and what
    /// rule it carries. The ZDO is the only copy — the UI, the router and the ledger all read the same
    /// value, so a client can never hold a rule the server does not have.
    /// </summary>
    public static class ContainerNetwork
    {
        public static ZDO Zdo(Container container)
        {
            if (container == null) return null;
            var view = container.GetComponent<ZNetView>();
            return view != null ? view.GetZDO() : null;
        }

        public static bool IsOnNetwork(Container container)
        {
            var zdo = Zdo(container);
            return zdo != null && zdo.GetBool(ZdoKeys.ContainerNetwork, false);
        }

        /// <summary>
        /// Blacklisted means "no Husnisse buttons and never part of a network". A prefab that ships
        /// blacklisted (carts, ships, display piles) answers true even with no ZDO flag, which is why
        /// the default list lives in <see cref="ContainerFilter"/> and not in save data.
        /// </summary>
        public static bool IsBlacklisted(Container container)
        {
            if (container == null) return true;
            var zdo = Zdo(container);
            if (zdo != null && zdo.GetBool(ZdoKeys.ContainerBlacklist, false)) return true;
            return ContainerFilter.DefaultBlacklisted(container);
        }

        public static ContainerRule RuleOf(Container container)
        {
            var zdo = Zdo(container);
            if (zdo == null) return ContainerRule.None();
            return ContainerRule.Decode(zdo.GetString(ZdoKeys.ContainerRule, string.Empty));
        }

        /// <summary>
        /// Who may change a rule: the claim's owner, or a player the ward permits. A player id of 0
        /// is a server or a tool with no identity, and is refused here — the server's own write path
        /// does not come through this gate.
        /// </summary>
        public static bool CanEdit(Claim claim, long playerId)
        {
            if (claim == null || claim.Suppressed) return false;
            if (playerId == 0L) return false;
            return claim.Allows(playerId);
        }

        /// <summary>
        /// The one question behind the container window: may this container show the three buttons?
        /// A claim must own it, the filter must allow it (not the bowl, not a backpack, not on a
        /// ward's ground the claim does not own) and it must not be blacklisted. The UI draws from
        /// this and a harness asks the same function, so "a blacklisted container never shows a
        /// button" is proven on one predicate instead of a copy of the conditions inside a patch.
        /// </summary>
        public static bool OffersUi(Claim claim, Container container)
        {
            if (claim == null || container == null) return false;
            if (claim.Suppressed) return false;
            if (container.GetComponent<Bowl.BowlPiece>() != null) return false;
            if (!ContainerFilter.CanUse(claim, container)) return false;
            return !IsBlacklisted(container);
        }

        /// <summary>
        /// Blacklists (or releases) one container. The flag is per container and only ever adds to
        /// the shipped defaults: a cart is blacklisted by class through `4. Storage / Skip Carts and
        /// Ships`, which no per-container flag overrides, so `Describe` is the truth about whether
        /// the flag took effect. Blacklisting also drops the container off the network, because a
        /// blacklisted container is never part of one — leaving `net=on` behind would be a state the
        /// router silently ignores.
        /// </summary>
        public static bool WriteBlacklist(Container container, bool blacklisted, string who)
        {
            var zdo = Zdo(container);
            if (zdo == null) return false;

            zdo.Set(ZdoKeys.ContainerBlacklist, blacklisted);
            if (blacklisted && IsOnNetwork(container)) zdo.Set(ZdoKeys.ContainerNetwork, false);

            Log.Debug("Rules", "blacklist " + (blacklisted ? "on" : "off") + " " +
                               Utils.GetPrefabName(container.gameObject) + " by " + who);
            return true;
        }

        /// <summary>One line per container for `husnisse_status`: position, opt-in and rule.</summary>
        public static string Describe(Container container)
        {
            if (container == null) return "none";
            var rule = RuleOf(container);
            var position = container.transform.position;
            return position.x.ToString("0.#") + "," + position.z.ToString("0.#") +
                   " net=" + (IsOnNetwork(container) ? "on" : "off") +
                   " rule=" + rule.Describe() +
                   (rule.Priority != 0 ? " prio=" + rule.Priority : string.Empty) +
                   rule.SupplyDescribe() +
                   (IsBlacklisted(container) ? " blacklisted" : string.Empty);
        }

        /// <summary>The server's write path: one Set per change, whole rule, never a partial field.</summary>
        public static bool WriteRule(Container container, ContainerRule rule, string who)
        {
            var zdo = Zdo(container);
            if (zdo == null) return false;

            if (rule != null) rule.Sanitise();
            var encoded = (rule ?? ContainerRule.None()).Encode();
            zdo.Set(ZdoKeys.ContainerRule, encoded);
            Log.Debug("Rules", "rule written " + Utils.GetPrefabName(container.gameObject) +
                               " " + encoded + " by " + who);
            return true;
        }

        public static bool WriteNetwork(Container container, bool on, string who)
        {
            var zdo = Zdo(container);
            if (zdo == null) return false;

            zdo.Set(ZdoKeys.ContainerNetwork, on);
            Log.Debug("Rules", "network " + (on ? "on" : "off") + " " +
                               Utils.GetPrefabName(container.gameObject) + " by " + who);
            return true;
        }

        /// <summary>
        /// Turning a container on for the first time also offers it a rule: the preset most of its
        /// contents already belong to (improvement 3). The write is the server's, like every other
        /// rule, and it happens only when the container has no rule at all, so a player's choice is
        /// never overwritten — and never a suggestion, because a chest that already holds the right
        /// category either has its rule or is a plain chest the player left alone.
        ///
        /// The acknowledgement leg and the harness use <see cref="WriteNetwork"/>: a peer applying
        /// the server's accepted value must not compute a suggestion of its own.
        /// </summary>
        public static bool WriteNetworkAndSuggest(Container container, bool on, string who)
        {
            if (!WriteNetwork(container, on, who)) return false;
            if (!on) return true;

            var suggested = Suggestion(container);
            if (suggested.IsNone) return true;

            WriteRule(container, suggested, who + " (from contents)");
            Log.Debug("Rules", "suggested " + suggested.Describe() + " for " +
                               Utils.GetPrefabName(container.gameObject) + " (" + Count(container) + " stack(s))");
            return true;
        }

        /// <summary>
        /// The rule a first toggle would offer: the preset most of the container's stacks belong to,
        /// or none when nothing in it is classified. A suggestion never replaces an existing rule.
        /// </summary>
        public static ContainerRule Suggestion(Container container)
        {
            if (container == null) return ContainerRule.None();
            if (!RuleOf(container).IsNone) return ContainerRule.None();

            var inventory = container.GetInventory();
            if (inventory == null) return ContainerRule.None();

            var items = new List<ItemDrop.ItemData>();
            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item != null) items.Add(item);
            }

            var category = CategoryMap.Dominant(items);
            if (category == ItemCategory.None) return ContainerRule.None();

            return new ContainerRule { Kind = RuleKind.Category, Category = category };
        }

        private static int Count(Container container)
        {
            var inventory = container.GetInventory();
            return inventory != null ? inventory.NrOfItems() : 0;
        }
    }
}
