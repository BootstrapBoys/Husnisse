using System;
using System.Collections.Generic;
using System.Globalization;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Contract
{
    /// <summary>
    /// The storage contract as Husnisse answers it: a read-only surface over the sources the player
    /// can reach right now — pockets, ExtraSlots rows, the equipped backpack, and the containers of
    /// the claim the player is standing in.
    ///
    /// **This build is read-only.** `Count` and `CanAfford` answer; `TryConsume` refuses, because
    /// consumption is the nisse's work and needs a visible delivery before it ships. A consumer must
    /// therefore check <see cref="CanConsume"/> (or treat a false from `TryConsume` as "I cannot
    /// pay") rather than counting on a spend. `ApiVersion` is the contract's own version and moves
    /// with the semantics in STORAGE-CONTRACT.md, never with the plugin version.
    /// </summary>
    public static class StorageContract
    {
        /// <summary>Semantics version from the contract document. Bumps independently of the mod.</summary>
        public const int ApiVersion = 1;

        public const string ProviderNative = "native";
        public const string ProviderQi = "qi";

        /// <summary>How long one resolved scope is reused. A read-only answer may be one tick stale.</summary>
        private const float ScopeSeconds = 0.5f;

        private static ContractScope _scope;
        private static Player _scopePlayer;
        private static long _scopePlayerId;
        private static float _scopeAt = -100f;
        private static bool _dirty = true;
        private static bool _consumeLogged;

        /// <summary>"qi" when Quartermaster_Integrations is loaded and owns the answers, else "native".</summary>
        public static string Provider
        {
            get { return PluginGuards.IntegrationsInstalled() ? ProviderQi : ProviderNative; }
        }

        /// <summary>False in 0.4: the contract answers questions, it does not spend.</summary>
        public static bool CanConsume
        {
            get { return false; }
        }

        /// <summary>Read, player present, world loaded. A dedicated server has no player and is not ready.</summary>
        public static bool IsReady
        {
            get
            {
                try
                {
                    return HusnissePlugin.Running &&
                           Player.m_localPlayer != null &&
                           ZNetScene.instance != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static string ScopeDescription
        {
            get
            {
                var scope = Scope();
                return scope != null ? scope.Description : "none (not ready)";
            }
        }

        /// <summary>How many of one item the player can reach. Never throws, never returns negative.</summary>
        public static int Count(string itemName)
        {
            try
            {
                var scope = Scope();
                return scope == null ? 0 : Math.Max(0, scope.Count(SharedName(itemName)));
            }
            catch (Exception ex)
            {
                Log.Debug("Contract", "count failed for '" + itemName + "': " + ex.Message);
                return 0;
            }
        }

        /// <summary>Sources 1–3 only: what the player carries. The crafting row's split display uses it.</summary>
        public static int CountCarried(string itemName)
        {
            try
            {
                var scope = Scope();
                return scope == null ? 0 : Math.Max(0, scope.Carried(SharedName(itemName)));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>The claim leg alone.</summary>
        public static int CountClaim(string itemName)
        {
            try
            {
                var scope = Scope();
                return scope == null ? 0 : Math.Max(0, scope.ClaimCount(SharedName(itemName)));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// True only when every requirement can be met from the scope, evaluated as one plan. Absent
        /// sources contribute nothing; an invalid amount or an unknown item answers false.
        /// </summary>
        public static bool CanAfford(IReadOnlyDictionary<string, int> requirements)
        {
            if (requirements == null) return false;

            try
            {
                var scope = Scope();
                if (scope == null) return false;
                if (requirements.Count == 0) return true;   // nothing required cannot fail

                var affordable = false;
                foreach (var pair in requirements)
                {
                    if (pair.Value <= 0) return false;
                    var name = SharedName(pair.Key);
                    if (string.IsNullOrEmpty(name)) return false;
                    if (scope.Count(name) < pair.Value) return false;
                    affordable = true;
                }

                return affordable;
            }
            catch (Exception ex)
            {
                Log.Debug("Contract", "CanAfford failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Not implemented in 0.4, and it says so rather than half-working: an all-or-nothing spend
        /// that debited pockets and then could not take the claim leg is how items vanish.
        /// </summary>
        public static bool TryConsume(string itemName, int amount)
        {
            if (!_consumeLogged)
            {
                _consumeLogged = true;
                Log.Info("[Contract] TryConsume refused: this build reads the claim but does not spend it " +
                         "(0.4 is read-only; consumption carries a visible delivery and lands in 0.5).");
            }

            Log.Debug("Contract", "TryConsume('" + itemName + "', " + amount + ") -> false (read-only build)");
            return false;
        }

        /// <summary>Drops the resolved scope, so the next query sees a write that just landed.</summary>
        public static void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// Maps a config or console name to the game's shared item name: `Iron` and `$item_iron` both
        /// answer `$item_iron`, and an unknown name is passed through untouched so `Count` answers 0.
        /// </summary>
        public static string SharedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name[0] == '$') return name;

            try
            {
                var database = ObjectDB.instance;
                if (database != null)
                {
                    var prefab = database.GetItemPrefab(name);
                    if (prefab != null)
                    {
                        var drop = prefab.GetComponent<ItemDrop>();
                        if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null &&
                            !string.IsNullOrEmpty(drop.m_itemData.m_shared.m_name))
                        {
                            return drop.m_itemData.m_shared.m_name;
                        }
                    }
                }
            }
            catch
            {
                // Fall through: an unreadable ObjectDB means the name is used as given.
            }

            return name;
        }

        /// <summary>
        /// The contract's own report: what is reachable, from where, and the answer for one item.
        /// `husnisse_contract` prints these lines and the harness reads them, so a gate asserts the
        /// same numbers the console shows.
        /// </summary>
        public static List<string> Report(string itemName)
        {
            var lines = new List<string>();

            var ready = IsReady;
            lines.Add("[contract] api=" + ApiVersion +
                      " provider=" + Provider +
                      " can_consume=" + CanConsume +
                      " ready=" + ready);

            var scope = Scope();
            if (scope == null)
            {
                lines.Add("[contract] scope none (not ready: " + WhyNotReady() + ")");
                lines.Add("[contract] sources pockets=absent slots=absent backpack=absent claim=absent");
            }
            else
            {
                lines.Add("[contract] scope " + scope.Description);

                var sources = new System.Text.StringBuilder();
                for (var i = 0; i < scope.Sources.Count; i++)
                {
                    var source = scope.Sources[i];
                    if (source == null) continue;
                    if (sources.Length > 0) sources.Append(' ');
                    sources.Append(source.Name).Append('=').Append(source.Present ? source.Describe() : "absent");
                }
                lines.Add("[contract] sources " + sources);
            }

            if (!string.IsNullOrEmpty(itemName))
            {
                var shared = SharedName(itemName);
                var name = string.IsNullOrEmpty(shared) ? itemName : shared;
                lines.Add("[contract] count " + name + "=" + Count(itemName) +
                          " carried=" + CountCarried(itemName) +
                          " claim=" + CountClaim(itemName));
            }

            return lines;
        }

        private static string WhyNotReady()
        {
            if (!HusnissePlugin.Running) return "mod not running";
            if (ZNetScene.instance == null) return "no world";
            if (Player.m_localPlayer == null) return "no local player";
            return "unknown";
        }

        private static ContractScope Scope()
        {
            if (!IsReady)
            {
                _scope = null;
                return null;
            }

            var player = Player.m_localPlayer;
            var playerId = player.GetPlayerID();
            var stale = _scope == null || _dirty || _scopePlayer != player || _scopePlayerId != playerId ||
                        Time.unscaledTime - _scopeAt > ScopeSeconds;

            if (stale)
            {
                _scope = ContractScope.Build(player);
                _scopePlayer = player;
                _scopePlayerId = playerId;
                _scopeAt = Time.unscaledTime;
                _dirty = false;

                Log.Debug("Contract", "scope " + _scope.Description);
            }

            return _scope;
        }

        /// <summary>Formats a count for a UI row: `42` or, when split, `42 [2+40]`.</summary>
        public static string Format(string itemName, bool split)
        {
            var total = Count(itemName);
            if (!split) return total.ToString(CultureInfo.InvariantCulture);

            return total.ToString(CultureInfo.InvariantCulture) +
                   " [" + CountCarried(itemName).ToString(CultureInfo.InvariantCulture) +
                   "+" + CountClaim(itemName).ToString(CultureInfo.InvariantCulture) + "]";
        }

        /// <summary>Reset on world load: a stale scope must never answer for the previous world.</summary>
        public static void Reset()
        {
            _scope = null;
            _scopePlayer = null;
            _scopePlayerId = 0L;
            _scopeAt = -100f;
            _dirty = true;
            SourceBindings.Reset();
        }
    }
}
