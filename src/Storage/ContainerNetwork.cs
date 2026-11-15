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

        /// <summary>Every container in the claim that is opted in, for diagnostics and the router.</summary>
        public static List<Container> OnNetwork(Claim claim)
        {
            var result = new List<Container>();
            if (claim == null) return result;

            foreach (var container in ContainerIndex.InClaim(claim))
            {
                if (IsOnNetwork(container)) result.Add(container);
            }
            return result;
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
                   (IsBlacklisted(container) ? " blacklisted" : string.Empty);
        }

        /// <summary>The server's write path: one Set per change, whole rule, never a partial field.</summary>
        public static bool WriteRule(Container container, ContainerRule rule, string who)
        {
            var zdo = Zdo(container);
            if (zdo == null) return false;

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
    }
}
