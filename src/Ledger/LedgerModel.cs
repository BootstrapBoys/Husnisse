using System;
using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Orders;
using Husnisse.Storage;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Ledger
{
    /// <summary>
    /// One container as the ledger sees it. Everything is read from the ZDO and the filter, so the
    /// ledger, the container window and the router answer from the same state and cannot drift.
    /// </summary>
    public sealed class LedgerRow
    {
        public Container Container;
        public string Prefab;
        public Vector3 Position;
        public ContainerRule Rule;
        public bool OnNetwork;
        public bool Blacklisted;
        public bool Offers;
        public bool Unknown;

        public string Describe()
        {
            return Prefab + " [" + Index + "] " +
                   Position.x.ToString("0.#") + "," + Position.z.ToString("0.#") +
                   " net=" + (OnNetwork ? "on" : "off") +
                   " rule=" + Rule.Describe() +
                   (Rule.Priority != 0 ? " p=" + Rule.Priority : string.Empty) +
                   " blacklisted=" + Blacklisted +
                   " ui=" + Offers +
                   " unknown=" + Unknown;
        }

        /// <summary>The index the console and the harness use for this container, among its prefab.</summary>
        public int Index;
    }

    /// <summary>
    /// The ledger's data: which containers a claim holds, which of them nobody has classified yet,
    /// and the claim's pickup filter. Read-only; every edit goes through <see cref="LedgerActions"/>,
    /// which is the same writer the container window uses.
    /// </summary>
    public static class LedgerModel
    {
        public static Claim At(Vector3 point)
        {
            var claim = ClaimRegistry.FindFor(point);
            return claim != null && claim.Bowl != null ? claim : null;
        }

        public static List<LedgerRow> Rows(Claim claim)
        {
            var rows = new List<LedgerRow>();
            if (claim == null) return rows;

            foreach (var container in ContainerIndex.InClaim(claim))
            {
                if (container == null) continue;

                var rule = ContainerNetwork.RuleOf(container);
                var blacklisted = ContainerNetwork.IsBlacklisted(container);
                var row = new LedgerRow
                {
                    Container = container,
                    Prefab = Utils.GetPrefabName(container.gameObject),
                    Position = container.transform.position,
                    Rule = rule,
                    OnNetwork = ContainerNetwork.IsOnNetwork(container),
                    Blacklisted = blacklisted,
                    Offers = ContainerNetwork.OffersUi(claim, container),
                };

                // "Unknown" is a container nobody has classified yet: on no network, no rule, not
                // skipped for what it is. A blacklisted one is a decision, not an unknown.
                row.Unknown = !row.Blacklisted && !row.OnNetwork && rule.IsNone &&
                              ContainerFilter.CanUse(claim, container);
                rows.Add(row);
            }

            Sort(rows);
            foreach (var row in rows) row.Index = IndexOf(rows, row);
            return rows;
        }

        /// <summary>x, then z, then y — the same order `husnisse_rule` and the harness `zdo` use, so
        /// "chest 3" means one container everywhere.</summary>
        private static void Sort(List<LedgerRow> rows)
        {
            rows.Sort(delegate(LedgerRow a, LedgerRow b)
            {
                var first = a.Position;
                var second = b.Position;
                if (Math.Abs(first.x - second.x) > 0.01f) return first.x < second.x ? -1 : 1;
                if (Math.Abs(first.z - second.z) > 0.01f) return first.z < second.z ? -1 : 1;
                return first.y.CompareTo(second.y);
            });
        }

        private static int IndexOf(List<LedgerRow> rows, LedgerRow row)
        {
            var index = 0;
            foreach (var candidate in rows)
            {
                if (ReferenceEquals(candidate, row)) return index;
                if (candidate.Prefab != row.Prefab) continue;
                index++;
            }
            return 0;
        }

        /// <summary>The ledger row for one container of a claim, or null when the claim cannot use it.</summary>
        public static LedgerRow RowFor(Claim claim, Container container)
        {
            if (claim == null || container == null) return null;
            foreach (var row in Rows(claim))
            {
                if (ReferenceEquals(row.Container, container)) return row;
            }
            return null;
        }

        /// <summary>Every live container of one prefab, in the console/harness order.</summary>
        public static List<Container> Containers(string prefab)
        {
            var matches = new List<Container>();
            foreach (var container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (container == null) continue;
                if (container.GetComponent<Bowl.BowlPiece>() != null) continue;
                if (prefab != null && Utils.GetPrefabName(container.gameObject) != prefab) continue;

                var view = ContainerNet.View(container);
                if (view == null || view.GetZDO() == null) continue;
                matches.Add(container);
            }

            matches.Sort(delegate(Container a, Container b)
            {
                var first = a.transform.position;
                var second = b.transform.position;
                if (Math.Abs(first.x - second.x) > 0.01f) return first.x < second.x ? -1 : 1;
                if (Math.Abs(first.z - second.z) > 0.01f) return first.z < second.z ? -1 : 1;
                return first.y.CompareTo(second.y);
            });
            return matches;
        }

        public static int UnknownCount(Claim claim)
        {
            var count = 0;
            foreach (var row in Rows(claim)) if (row.Unknown) count++;
            return count;
        }

        /// <summary>The summary line the ledger panel and `husnisse_ledger` both print.</summary>
        public static string Describe(Claim claim)
        {
            if (claim == null) return "ledger claim=none";

            var rows = Rows(claim);
            var unknown = 0;
            var networked = 0;
            var ruled = 0;
            foreach (var row in rows)
            {
                if (row.Unknown) unknown++;
                if (row.OnNetwork) networked++;
                if (!row.Rule.IsNone) ruled++;
            }

            return "ledger claim=" + claim.BowlId +
                   " source=" + claim.SourceName +
                   " owner=" + claim.OwnerId +
                   " r=" + claim.Radius.ToString("0") +
                   " rows=" + rows.Count +
                   " networked=" + networked +
                   " ruled=" + ruled +
                   " unknown=" + unknown +
                   " pickup=" + PickupFilter.Of(claim).Describe() +
                   " presets=" + PresetStore.Describe();
        }
    }

    /// <summary>
    /// The ledger's edits. Each one is a request the server validates, sent through the same channel
    /// the container window uses — one writer for the button, the ledger and teaching. A console
    /// command that wants to prove a ledger edit goes through here too, so a headless scenario
    /// exercises the same path a click does.
    /// </summary>
    public static class LedgerActions
    {
        public static void SetNetwork(Claim claim, LedgerRow row, bool on)
        {
            if (!Editable(claim, row)) return;
            Net.RuleRpc.SendNetwork(row.Position, PlayerId(), on);
        }

        public static void SetRule(Claim claim, LedgerRow row, ContainerRule rule)
        {
            if (!Editable(claim, row)) return;
            Net.RuleRpc.SendRule(row.Position, PlayerId(), rule);
        }

        /// <summary>Give a container a category and put it on the network in one write: the
        /// "classify this unknown chest" action.</summary>
        public static void Classify(Claim claim, LedgerRow row, ItemCategory category)
        {
            if (!Editable(claim, row) || category == ItemCategory.None) return;

            var rule = (row.Rule ?? ContainerRule.None()).Clone();
            rule.Kind = RuleKind.Category;
            rule.Category = category;
            Net.RuleRpc.SendRuleAndNetwork(row.Position, PlayerId(), rule);
        }

        public static void SetBlacklist(Claim claim, LedgerRow row, bool blacklisted)
        {
            if (!Editable(claim, row)) return;
            Net.RuleRpc.SendBlacklist(row.Position, PlayerId(), blacklisted);
        }

        /// <summary>
        /// Pause or resume every machine in the claim (0.6). One request per machine, each validated
        /// against the claim that contains it — the same op the console's `pause` uses, so a client's
        /// click and an admin's command cannot take different paths.
        /// </summary>
        public static void SetMachinesPaused(Claim claim, bool paused)
        {
            if (claim == null || !CanEdit(claim)) return;

            var count = 0;
            foreach (var machine in Machines.MachineScan.All(claim))
            {
                Net.RuleRpc.SendMachinePause(machine.Position, PlayerId(), paused);
                count++;
            }

            Note("machines pause=" + paused + " count=" + count + " claim=" + claim.BowlId);
        }

        public static void SetPriority(Claim claim, LedgerRow row, int priority)
        {
            if (!Editable(claim, row)) return;

            var rule = (row.Rule ?? ContainerRule.None()).Clone();
            if (rule.IsNone) return;
            rule.Priority = Mathf.Clamp(priority, -9, 9);
            Net.RuleRpc.SendRule(row.Position, PlayerId(), rule);
        }

        /// <summary>The "sort a chest you do not own" half of the old chest dock: one order, no UI.</summary>
        public static bool Tidy(Claim claim, LedgerRow row)
        {
            if (claim == null || row == null || row.Container == null) return false;

            var player = Player.m_localPlayer;
            if (player == null)
            {
                Log.Warn("ledger: tidy needs a local player (a dedicated server has none)");
                return false;
            }

            OrderService.RequestTidy(player, claim, row.Container);
            Note("tidy " + row.Prefab + "[" + row.Index + "] claim=" + claim.BowlId);
            return true;
        }

        public static void SetPickup(Claim claim, ItemCategory category, bool on)
        {
            if (claim == null || category == ItemCategory.None) return;

            var filter = PickupFilter.Of(claim).With(category, on);
            Net.RuleRpc.SendPickup(claim.BowlPosition, PlayerId(), filter);
            Note("pickup " + filter.Describe() + " claim=" + claim.BowlId);
        }

        public static void SetPickup(Claim claim, PickupFilter filter)
        {
            if (claim == null) return;
            Net.RuleRpc.SendPickup(claim.BowlPosition, PlayerId(), filter);
            Note("pickup " + (filter ?? PickupFilter.All()).Describe() + " claim=" + claim.BowlId);
        }

        private static bool Editable(Claim claim, LedgerRow row)
        {
            if (claim == null || row == null || row.Container == null) return false;
            if (!CanEdit(claim)) return false;
            return true;
        }

        /// <summary>
        /// Whether this machine's caller may write. No local player is the server console; a player
        /// with id 0 is a tool or a probe (a real client always has the id its character ZDO carries)
        /// and is left to the server's own validation, exactly as `husnisse_rule` has always been.
        /// </summary>
        public static bool CanEdit(Claim claim)
        {
            var player = Player.m_localPlayer;
            if (player == null) return claim != null;
            if (player.GetPlayerID() == 0L) return claim != null;
            return ContainerNetwork.CanEdit(claim, player.GetPlayerID());
        }

        private static long PlayerId()
        {
            var player = Player.m_localPlayer;
            return player != null ? player.GetPlayerID() : 0L;
        }

        private static void Note(string line)
        {
            Log.Debug("Ledger", line);
            DiagnosticsFile.Line("[Ledger] " + line);
        }
    }
}
