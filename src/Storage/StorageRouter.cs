using System;
using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Memory;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>Why a stack went where it went. The trip log and the diagnostics print this.</summary>
    public enum RouteTier
    {
        None = 0,

        /// <summary>A container whose preset category covers the item.</summary>
        Category = 1,

        /// <summary>A container whose saved list names the item.</summary>
        Custom = 2,

        /// <summary>The claim's overflow chest: it takes what no rule claimed.</summary>
        Overflow = 3,

        /// <summary>A container on the network with no rule: the general-purpose chest.</summary>
        Generic = 4,
    }

    /// <summary>A routing answer, including why, so a misfiled stack can be explained.</summary>
    public struct RouteDecision
    {
        public Container Container;
        public RouteTier Tier;
        public ContainerRule Rule;
        public string Why;

        public bool Found
        {
            get { return Container != null; }
        }

        public static RouteDecision Unrouted(string why)
        {
            return new RouteDecision { Container = null, Tier = RouteTier.None, Why = why };
        }
    }

    /// <summary>
    /// Decides which chest a stack belongs in. The rules a player wrote are the first and only
    /// authority: a stack goes to the container whose preset category or saved list accepts it, the
    /// highest priority first, and to an Overflow chest or a container with no rule only when no
    /// rule claimed it. A Deposit chest is a source and is never a destination.
    ///
    /// "Full" is a property of the stack, not the chest: a chest with no empty slot and no stack of
    /// this item cannot take it, so it drops out of the running and the next chest of the same
    /// category — at any priority — is tried before Overflow. Only when nothing that claims the
    /// stack can hold it does it fall through, and the reason then names the rules that were full.
    ///
    /// The nisse's memory no longer overrides a rule — a player who says "metals live here" outranks
    /// a habit — but it still breaks a tie between two containers that accept the same item, which is
    /// what keeps a two-chest base from filling in an order nobody chose.
    /// </summary>
    public static class StorageRouter
    {
        private struct Candidate
        {
            public Container Container;
            public ContainerRule Rule;
            public RouteTier Tier;
            public int Priority;
            public bool Remembered;
            public bool HoldsItem;
            public float Distance;
            public ZDOID Id;
        }

        /// <summary>Whether a container's rule claims this stack, whatever its room or state.</summary>
        public static bool Accepts(ContainerRule rule, ItemDrop.ItemData item)
        {
            if (rule == null || item == null || item.m_shared == null) return false;

            switch (rule.Kind)
            {
                case RuleKind.Category:
                    return CategoryMap.Accepts(rule.Category, item);

                case RuleKind.Custom:
                    if (rule.Items == null) return false;
                    foreach (var name in rule.Items)
                    {
                        if (string.Equals(name, item.m_shared.m_name, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// The containers a claim could file into at all: in the claim, on the network, not
        /// blacklisted, and not a deposit chest. Gathered once and reused when a caller is routing a
        /// pile of stacks (a floor of drops), so a sweep does not rescan the scene per stack.
        /// Room and the item itself are decided per stack.
        /// </summary>
        public static List<Container> NetworkCandidates(Claim claim)
        {
            return NetworkCandidates(claim, ContainerIndex.InClaim(claim));
        }

        /// <summary>
        /// The same filter against a container list the caller already gathered — a refill pass
        /// gathers the claim once and asks this twice (for the ground drops and for the deposit
        /// chests) instead of scanning the scene per question.
        /// </summary>
        public static List<Container> NetworkCandidates(Claim claim, List<Container> containers)
        {
            var candidates = new List<Container>();
            if (claim == null || containers == null) return candidates;

            foreach (var container in containers)
            {
                if (container == null) continue;
                if (ContainerNetwork.IsBlacklisted(container)) continue;
                if (!ContainerNetwork.IsOnNetwork(container)) continue;

                // A deposit chest is where a player puts things for the nisse to take away. Filing
                // into one would undo the only thing it is for.
                if (ContainerNetwork.RuleOf(container).Kind == RuleKind.Deposit) continue;
                candidates.Add(container);
            }

            return candidates;
        }

        /// <summary>
        /// The full answer for one stack: the chest, the tier that chose it and the reason, or the
        /// reason nothing did. Never throws on a missing claim or a malformed item; a stack with no
        /// home is simply not this trip's work.
        /// </summary>
        public static RouteDecision Decide(Claim claim, ItemDrop.ItemData item, ItemMemory memory, Container exclude = null)
        {
            return Decide(claim, NetworkCandidates(claim), item, memory, exclude);
        }

        /// <summary>The same decision against a candidate list the caller already gathered.</summary>
        public static RouteDecision Decide(Claim claim, List<Container> network, ItemDrop.ItemData item,
                                           ItemMemory memory, Container exclude = null)
        {
            if (claim == null) return RouteDecision.Unrouted("no claim");
            if (item == null || item.m_shared == null) return RouteDecision.Unrouted("no item");
            if (network == null || network.Count == 0) return RouteDecision.Unrouted("nothing on the network");

            var name = item.m_shared.m_name;
            ZDOID remembered = ZDOID.None;
            if (memory != null) memory.TryRecall(name, out remembered);

            var candidates = new List<Candidate>();
            var full = new List<string>();
            var onNetwork = 0;

            foreach (var container in network)
            {
                if (container == null || container == exclude) continue;

                onNetwork++;

                var rule = ContainerNetwork.RuleOf(container);
                var tier = TierOf(rule, item);
                if (tier == RouteTier.None) continue;

                if (!CanTake(container, item))
                {
                    // The rule claimed this stack but the chest cannot hold it. That is the "full
                    // category" case, and naming it is what turns "nowhere to put it" into a reason
                    // a player can act on.
                    full.Add(RuleToken(rule));
                    continue;
                }

                candidates.Add(Describe(claim, container, rule, tier, remembered, name));
            }

            if (onNetwork == 0) return RouteDecision.Unrouted("nothing on the network");

            // A container whose rule accepts the stack is the first answer at any priority: a
            // lower-priority chest of the same category with room is still the right home, and
            // Overflow is for what no rule can take *today*, not for what a shipping order
            // pushed out. Where several accept it, Pick's total order decides.
            var best = Pick(candidates, RouteTier.Category, RouteTier.Custom);
            if (best.Container != null) return Answer(best, string.Empty);

            // No rule-bearing chest can take it. Overflow is the chest a player designated for
            // "everything else" — full categories included — and it outranks a chest that merely
            // has no rule, because it is a decision and not a default.
            var note = FullNote(full);
            best = Pick(candidates, RouteTier.Overflow);
            if (best.Container != null) return Answer(best, note);

            best = Pick(candidates, RouteTier.Generic);
            if (best.Container != null) return Answer(best, note);

            return RouteDecision.Unrouted("no chest has room for " + name + note);
        }

        private static RouteTier TierOf(ContainerRule rule, ItemDrop.ItemData item)
        {
            if (rule.Kind == RuleKind.Overflow) return RouteTier.Overflow;
            if (rule.Kind == RuleKind.None) return RouteTier.Generic;
            if (!Accepts(rule, item)) return RouteTier.None;
            return rule.Kind == RuleKind.Category ? RouteTier.Category : RouteTier.Custom;
        }

        private static Candidate Describe(Claim claim, Container container, ContainerRule rule, RouteTier tier,
                                          ZDOID remembered, string itemName)
        {
            var view = container.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            var inventory = container.GetInventory();

            return new Candidate
            {
                Container = container,
                Rule = rule,
                Tier = tier,
                Priority = rule.Priority,
                Remembered = zdo != null && zdo.m_uid == remembered,
                HoldsItem = inventory != null && inventory.ContainsItemByName(itemName),
                Distance = Vector3.Distance(container.transform.position, claim.Center),
                Id = zdo != null ? zdo.m_uid : ZDOID.None,
            };
        }

        /// <summary>
        /// The best of one tier: highest priority, then the chest the nisse remembers, then one that
        /// already holds the item, then the nearest to the bowl, then the id. Every step is a total
        /// order, so two runs over the same base pick the same chest.
        /// </summary>
        private static Candidate Pick(List<Candidate> candidates, params RouteTier[] tiers)
        {
            var best = default(Candidate);
            var found = false;

            foreach (var candidate in candidates)
            {
                if (Array.IndexOf(tiers, candidate.Tier) < 0) continue;
                if (!found || Better(candidate, best))
                {
                    best = candidate;
                    found = true;
                }
            }

            return found ? best : default(Candidate);
        }

        private static bool Better(Candidate a, Candidate b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            if (a.Remembered != b.Remembered) return a.Remembered;
            if (a.HoldsItem != b.HoldsItem) return a.HoldsItem;
            if (Mathf.Abs(a.Distance - b.Distance) > 0.01f) return a.Distance < b.Distance;
            if (a.Id != b.Id) return a.Id.CompareTo(b.Id) < 0;
            return false;
        }

        private static RouteDecision Answer(Candidate best, string note)
        {
            var rule = best.Rule;
            string why;
            switch (best.Tier)
            {
                case RouteTier.Category:
                    why = "cat:" + ContainerRule.CategoryToken(rule.Category);
                    break;
                case RouteTier.Custom:
                    why = "custom:" + (rule.Items != null ? rule.Items.Count : 0);
                    break;
                case RouteTier.Overflow:
                    why = "overflow";
                    break;
                default:
                    why = "no rule";
                    break;
            }

            if (best.Priority != 0) why += " p=" + best.Priority;
            if (best.Remembered) why += " remembered";
            else if (best.HoldsItem) why += " already holds it";

            return new RouteDecision
            {
                Container = best.Container,
                Tier = best.Tier,
                Rule = rule,
                Why = why + note,
            };
        }

        /// <summary>The token a rule is known by in a reason: cat:metals, custom, overflow.</summary>
        private static string RuleToken(ContainerRule rule)
        {
            switch (rule.Kind)
            {
                case RuleKind.Category:
                    return "cat:" + ContainerRule.CategoryToken(rule.Category);
                case RuleKind.Custom:
                    return "custom";
                case RuleKind.Overflow:
                    return "overflow";
                case RuleKind.Deposit:
                    return "deposit";
                default:
                    return "no rule";
            }
        }

        /// <summary>
        /// " (full: cat:metals+overflow)" — which rules wanted the stack and could not hold it.
        /// Sorted and capped so the same base always prints the same reason.
        /// </summary>
        private static string FullNote(List<string> full)
        {
            if (full == null || full.Count == 0) return string.Empty;

            var distinct = new List<string>();
            foreach (var token in full)
            {
                if (token != null && !distinct.Contains(token)) distinct.Add(token);
            }
            distinct.Sort(StringComparer.Ordinal);

            const int show = 3;
            var text = distinct.Count > show ? string.Join("+", distinct.GetRange(0, show).ToArray()) + "+"
                                             : string.Join("+", distinct.ToArray());
            return " (full: " + text + ")";
        }

        /// <summary>
        /// Room for *this* stack: an empty slot, or a stack of the same item with room in it. A chest
        /// with eight full stacks of coal and one free slot is full for an axe.
        /// </summary>
        private static bool CanTake(Container container, ItemDrop.ItemData item)
        {
            var inventory = container.GetInventory();
            if (inventory == null) return false;
            if (inventory.GetEmptySlots() > 0) return true;
            if (!inventory.ContainsItemByName(item.m_shared.m_name)) return false;
            return inventory.CanAddItem(item.Clone());
        }

        public static void Learn(Container container, string itemName, ItemMemory memory)
        {
            if (container == null || memory == null) return;
            var view = container.GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null) return;
            memory.Remember(itemName, view.GetZDO().m_uid);
            Log.Debug("Memory", "learned " + itemName + " -> " + view.GetZDO().m_uid);
        }
    }
}
