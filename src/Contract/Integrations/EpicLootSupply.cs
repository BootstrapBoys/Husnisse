using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// EpicLoot's material costs, paid from the claim.
    ///
    /// EpicLoot's inventory-provider API looks like a count-plus-pay seam, and it is not one a claim
    /// can honestly fill: its cost list is a list of **specific stacks**, `HasItem` asks
    /// `CountItem(name) >= stack`, and payment is `RemoveItem(stack)` — pockets first, providers for
    /// the remainder. A provider that counted a claim chest would make the button say affordable and
    /// then fail to take the remainder, and EpicLoot's pay path does not check the return: the
    /// enchantment would go through with the cost unpaid (or half-paid, if part of the stack was in
    /// the pockets). That is a free enchant, not a storage feature — which is why 0.4 deferred the
    /// provider and why 0.5 does not bring one either.
    ///
    /// So this seam is a **fetch before the pay**, not a provider. The one question every EpicLoot
    /// cost path asks first is `EnchantingTableUIPanelBase.LocalPlayerCanAffordCost(cost)`: the
    /// button's state, the shard and entity tabs, the rune and upgrade tables all go through it. The
    /// prefix reads the cost, and when the pockets cannot cover it and the claim can, it asks the
    /// nisse for the difference. The answer is left exactly as EpicLoot computed it, so the button
    /// stays unavailable until the load is in the player's hands — and then EpicLoot's own pay path
    /// takes the materials from the pockets, which is the order the storage contract fixes.
    ///
    /// The claim never offers itself as an editable EpicLoot inventory, so no item is enchanted in a
    /// chest where the change would not be saved.
    /// </summary>
    public static class EpicLootSupply
    {
        /// <summary>One ask per cost per this many seconds: the afford check runs whenever the UI ticks.</summary>
        private const float AskInterval = 6f;

        private static bool _bound;
        private static MethodInfo _getItem;
        private static float _lastAsk;
        private static string _lastKey = string.Empty;

        public static string State { get; private set; } = "absent";

        public static bool IsReady
        {
            get { return _bound && Enabled(); }
        }

        public static void Bind(Harmony harmony)
        {
            State = "absent";

            if (!PluginGuards.Installed(PluginGuards.EpicLootGuid)) return;

            if (!StorageContract.CanConsume)
            {
                State = "off";
                Log.Info("[Contract] EpicLoot is installed; craft from the claim is off, so its costs stay " +
                         "pocket-only.");
                return;
            }

            try
            {
                var info = Chainloader.PluginInfos[PluginGuards.EpicLootGuid];
                var assembly = info.Instance.GetType().Assembly;
                var panel = assembly.GetType("EpicLoot_UnityLib.EnchantingTableUIPanelBase");

                // The cost parameter is `List<EpicLoot's own element type>`; find the method by name
                // and shape rather than naming a type this assembly does not reference.
                MethodInfo afford = null;
                if (panel != null)
                {
                    foreach (var method in panel.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (method.Name != "LocalPlayerCanAffordCost") continue;
                        var parameters = method.GetParameters();
                        if (parameters.Length != 1 || !parameters[0].ParameterType.IsGenericType) continue;
                        afford = method;
                        break;
                    }
                }

                if (afford == null)
                {
                    State = "types-missing";
                    Log.Warn("[Contract] EpicLoot is installed but " +
                             "EnchantingTableUIPanelBase.LocalPlayerCanAffordCost was not found; its costs stay " +
                             "pocket-only.");
                    return;
                }

                var element = afford.GetParameters()[0].ParameterType.GetGenericArguments()[0];
                _getItem = AccessTools.Method(element, "GetItem", Type.EmptyTypes);

                if (_getItem == null)
                {
                    State = "types-missing";
                    Log.Warn("[Contract] EpicLoot's cost elements have no GetItem(); its costs stay pocket-only.");
                    return;
                }

                harmony.Patch(afford, prefix: new HarmonyMethod(typeof(EpicLootSupply), nameof(AffordPrefix)));

                _bound = true;
                State = "bound";
                Log.Info("[Contract] EpicLoot costs fetch from the claim: a cost the pockets cannot cover asks " +
                         "the nisse for the shortfall, and EpicLoot pays from the pockets once it lands.");
            }
            catch (Exception ex)
            {
                State = "error";
                Log.Warn("[Contract] EpicLoot supply binding failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Reads the cost list and starts a fetch for what the pockets lack. The return value is
        /// untouched: EpicLoot's own answer is what the button shows, and it becomes true because
        /// the materials arrive, not because this mod said so.
        /// </summary>
        public static void AffordPrefix(object[] __args)
        {
            if (!IsReady) return;
            if (__args == null || __args.Length == 0) return;

            var cost = __args[0] as IEnumerable;
            if (cost == null) return;

            var player = Player.m_localPlayer;
            if (player == null || player.NoCostCheat()) return;

            var needed = new List<KeyValuePair<string, int>>();
            var key = string.Empty;

            foreach (var entry in cost)
            {
                if (entry == null) continue;

                ItemDrop.ItemData item;
                try
                {
                    item = _getItem.Invoke(entry, null) as ItemDrop.ItemData;
                }
                catch
                {
                    continue;
                }

                if (item == null || item.m_shared == null || item.m_stack <= 0) continue;

                var name = item.m_shared.m_name;
                if (string.IsNullOrEmpty(name)) continue;

                // Only the difference is asked for: EpicLoot counts what the player holds and pays
                // from it, so the claim supplies the rest.
                var carried = StorageContract.CountCarried(name);
                var need = Mathf.Max(0, item.m_stack - carried);
                key += name + "x" + need + ";";
                if (need > 0) Add(needed, name, need);
            }

            if (needed.Count == 0) return;

            var claim = SupplyService.ClaimFor(player);
            if (claim == null || !SupplyService.CanCover(player, needed)) return;

            if (key == _lastKey && Time.time - _lastAsk < AskInterval) return;
            _lastKey = key;
            _lastAsk = Time.time;

            long token;
            var answer = SupplyService.Request(player, claim, needed, out token);
            if (!answer.HasValue || answer.Value == Net.FetchResult.Accepted)
            {
                player.Message(MessageHud.MessageType.Center, Localization.Loc.T(Localization.Loc.SupplyFetching));
                Log.Debug("Contract", "[EpicLoot] cost fetch asked: " + key + " token=" + token);
            }
        }

        private static void Add(List<KeyValuePair<string, int>> items, string name, int amount)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Key != name) continue;
                items[i] = new KeyValuePair<string, int>(name, items[i].Value + amount);
                return;
            }
            items.Add(new KeyValuePair<string, int>(name, amount));
        }

        private static bool Enabled()
        {
            return HusnisseConfig.CraftSupply != null && HusnisseConfig.CraftSupply.Value;
        }
    }
}
