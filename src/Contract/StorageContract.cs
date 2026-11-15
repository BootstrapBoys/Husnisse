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
    /// The storage contract as Husnisse answers it: a surface over the sources the player can reach
    /// right now — pockets, ExtraSlots rows, the equipped backpack, and the containers of the claim
    /// the player is standing in.
    ///
    /// **Reading is direct; spending is the nisse's work.** `Count` and `CanAfford` scan the
    /// instantiated containers. `TryConsume` debits the three sources a client can write — pockets,
    /// ExtraSlots rows, the equipped pack — and, when the amount needs the claim, it does not return
    /// a promise: a claim chest's contents belong to its ZDO owner, so a client cannot debit one
    /// inside a synchronous call. Instead it asks the nisse for the shortfall (the visible supply
    /// errand) and answers false; the caller retries once the load is in its hands. That keeps the
    /// contract's all-or-nothing rule literally true: false means nothing was mutated.
    /// `ApiVersion` is the contract's own version and moves with the semantics in
    /// STORAGE-CONTRACT.md, never with the plugin version.
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
        private static bool _consuming;
        private static bool _fetchLogged;

        /// <summary>"qi" when Quartermaster_Integrations is loaded and owns the answers, else "native".</summary>
        public static string Provider
        {
            get { return PluginGuards.IntegrationsInstalled() ? ProviderQi : ProviderNative; }
        }

        /// <summary>True when a spend can be attempted: the supply path exists and the mod is running.</summary>
        public static bool CanConsume
        {
            get
            {
                try
                {
                    return HusnissePlugin.Running &&
                           HusnisseConfig.CraftSupply != null && HusnisseConfig.CraftSupply.Value;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// The contract's reentrancy rule: a spend during one of the implementation's own operations
        /// answers false and mutates nothing. A supply errand is such an operation — it is moving the
        /// same materials the contract is counting.
        /// </summary>
        public static bool Busy
        {
            get
            {
                if (_consuming) return true;

                try
                {
                    var player = Player.m_localPlayer;
                    if (player == null) return false;
                    return Flow.FetchRunner.ForPlayer(player.GetPlayerID()) != null;
                }
                catch
                {
                    return false;
                }
            }
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
        /// Take an amount out of what the player carries, in the contract's source order. An amount
        /// that needs the claim cannot be taken here: the claim leg is a chest, the chest belongs to
        /// its ZDO owner, and a synchronous client call cannot debit it without lying. So the nisse
        /// is asked to bring the shortfall, nothing is mutated, and the caller retries when the load
        /// is in its hands.
        /// </summary>
        public static bool TryConsume(string itemName, int amount)
        {
            if (_consuming) return false;

            try
            {
                _consuming = true;

                if (!CanConsume) return false;
                if (!IsReady) return false;
                if (amount <= 0) return false;

                var name = SharedName(itemName);
                if (string.IsNullOrEmpty(name)) return false;

                var scope = Scope();
                if (scope == null) return false;

                if (scope.Carried(name) < amount) return AskForClaim(scope, name, amount);
                return TakeCarried(scope, name, amount);
            }
            catch (Exception ex)
            {
                Log.Debug("Contract", "TryConsume failed: " + ex.Message);
                return false;
            }
            finally
            {
                _consuming = false;
            }
        }

        /// <summary>
        /// The claim leg, honestly: ask the nisse for the shortfall and answer false now. Nothing is
        /// debited, which is what the contract's all-or-nothing rule requires of a false answer.
        /// </summary>
        private static bool AskForClaim(ContractScope scope, string name, int amount)
        {
            var carried = scope.Carried(name);
            var shortage = amount - carried;

            if (scope.Claim == null || !SupplyService.CanCover(scope.Player, new List<KeyValuePair<string, int>>
                {
                    new KeyValuePair<string, int>(name, shortage),
                }))
            {
                if (!_consumeLogged)
                {
                    _consumeLogged = true;
                    Log.Info("[Contract] TryConsume(" + name + ", " + amount + ") -> false: the player carries " +
                             carried + " and the claim cannot cover the rest.");
                }
                return false;
            }

            long token;
            var answer = SupplyService.Request(scope.Player, scope.Claim,
                new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(name, shortage) }, out token);

            if (!_fetchLogged || answer.HasValue && answer.Value != Net.FetchResult.Accepted)
            {
                _fetchLogged = true;
                Log.Info("[Contract] TryConsume(" + name + ", " + amount + ") -> false: asked the nisse for the " +
                         shortage + " the player lacks (token=" + token + ", answer=" +
                         (answer.HasValue ? answer.Value.ToString() : "sent") + ").");
            }

            DiagnosticsFile.Line("[Contract] consume deferred item=" + name + " carried=" + carried +
                                 " shortage=" + shortage + " token=" + token);
            return false;
        }

        /// <summary>
        /// Debits pockets, then ExtraSlots rows, then the equipped backpack. Counted first, then
        /// taken, on one thread: a removal that still fails puts everything back so the answer is
        /// all-or-nothing in practice as well as in the contract.
        /// </summary>
        private static bool TakeCarried(ContractScope scope, string name, int amount)
        {
            var removed = new List<ItemDrop.ItemData>();
            var left = amount;

            var sources = new[] { "pockets", "slots", "backpack" };
            for (var i = 0; i < sources.Length && left > 0; i++)
            {
                var take = Math.Min(left, scope.CountIn(sources[i], name));
                if (take <= 0) continue;

                var inventory = sources[i] == "backpack"
                    ? SourceBindings.EquippedBackpack(scope.Player)
                    : scope.Player.GetInventory();
                if (inventory == null) break;

                var slots = sources[i] == "slots";
                var got = Remove(inventory, name, take, slots, removed);
                left -= got;
                if (got < take) break;
            }

            if (left > 0)
            {
                for (var i = 0; i < removed.Count; i++)
                {
                    var inventory = scope.Player.GetInventory();
                    if (inventory != null) inventory.AddItem(removed[i]);
                }

                Log.Debug("Contract", "TryConsume(" + name + ", " + amount + ") rolled back; " + left + " short");
                return false;
            }

            MarkDirty();
            Log.Debug("Contract", "TryConsume(" + name + ", " + amount + ") -> true (" + removed.Count + " stack(s))");
            DiagnosticsFile.Line("[Contract] consumed item=" + name + " amount=" + amount +
                                 " stacks=" + removed.Count + " carried=" + scope.Carried(name));
            return true;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> from one inventory, restricted to the rows that
        /// source owns: ExtraSlots rows for the slot source, ordinary rows for the pocket source.
        /// Clones of what left are collected so a later failure can put them back.
        /// </summary>
        private static int Remove(Inventory inventory, string name, int amount, bool slots,
                                  List<ItemDrop.ItemData> removed)
        {
            var taken = 0;
            var items = new List<ItemDrop.ItemData>(inventory.GetAllItems());

            for (var i = 0; i < items.Count && taken < amount; i++)
            {
                var item = items[i];
                if (item == null || item.m_shared == null) continue;
                if (item.m_shared.m_name != name) continue;
                if (SourceBindings.IsExtraSlotItem(item) != slots) continue;

                var take = Math.Min(item.m_stack, amount - taken);
                if (take <= 0) continue;

                var clone = item.Clone();
                clone.m_stack = take;

                if (!inventory.RemoveItem(item, take)) continue;

                removed.Add(clone);
                taken += take;
            }

            return taken;
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
                      " busy=" + Busy +
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
