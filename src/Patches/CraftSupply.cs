using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Contract;
using Husnisse.Core;
using Husnisse.Localization;
using Husnisse.Net;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// Craft from the claim, in the only order a client can honestly do it: ask the nisse for the
    /// materials the pockets lack, let it carry them to the bench, and then let the game's own craft
    /// spend the player's pockets. The nisse never spends a chest on a promise and the craft never
    /// runs before its materials are on the player.
    ///
    /// The craft button is what starts it. While a recipe is selected the requirement rows already
    /// show what the claim can reach (0.4); this patch makes the press agree with the row: if the
    /// pockets cannot pay but the claim can, the press becomes a supply errand and the craft is run
    /// again — unchanged, vanilla's own — the moment the load is in the player's hands.
    /// </summary>
    public static class CraftSupply
    {
        private struct Pending
        {
            public long Token;
            public string Item;
            public int Quality;
            public int Batch;
            public int Variant;
            public bool Multi;
            public float At;
        }

        /// <summary>How long a delivered-but-unclaimed load waits for its craft before being forgotten.</summary>
        private const float PendingSeconds = 120f;

        private static Pending _pending;
        private static bool _hasPending;
        private static float _lastBusyMessage;

        private static FieldInfo _selectedRecipe;
        private static PropertyInfo _selectedRecipeProperty;
        private static PropertyInfo _selectedItemData;
        private static FieldInfo _craftRecipe;
        private static FieldInfo _craftUpgradeItem;
        private static FieldInfo _craftVariant;
        private static FieldInfo _multiCrafting;
        private static FieldInfo _multiAmount;
        private static FieldInfo _touchMultiCrafting;
        private static FieldInfo _selectedVariant;
        private static MethodInfo _doCrafting;

        private static bool _aaaScanned;
        private static FieldInfo _aaaAmount;
        private static FieldInfo _aaaInput;

        public static int SuppliesStarted { get; private set; }
        public static int SuppliesLanded { get; private set; }

        public static bool Bind(Harmony harmony)
        {
            try
            {
                var requirements = AccessTools.Method(typeof(Player), "HaveRequirements",
                    new[] { typeof(Recipe), typeof(bool), typeof(int), typeof(int) });
                var pressed = AccessTools.Method(typeof(InventoryGui), "OnCraftPressed", Type.EmptyTypes);
                var update = AccessTools.Method(typeof(InventoryGui), "Update", Type.EmptyTypes);

                _selectedRecipe = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
                _craftRecipe = AccessTools.Field(typeof(InventoryGui), "m_craftRecipe");
                _craftUpgradeItem = AccessTools.Field(typeof(InventoryGui), "m_craftUpgradeItem");
                _craftVariant = AccessTools.Field(typeof(InventoryGui), "m_craftVariant");
                _multiCrafting = AccessTools.Field(typeof(InventoryGui), "m_multiCrafting");
                _multiAmount = AccessTools.Field(typeof(InventoryGui), "m_multiCraftAmount");
                _touchMultiCrafting = AccessTools.Field(typeof(InventoryGui), "m_touchMultiCrafting");
                _selectedVariant = AccessTools.Field(typeof(InventoryGui), "m_selectedVariant");
                _doCrafting = AccessTools.Method(typeof(InventoryGui), "DoCrafting", new[] { typeof(Player) });

                if (_selectedRecipe != null)
                {
                    var pairType = _selectedRecipe.FieldType;
                    _selectedRecipeProperty = AccessTools.Property(pairType, "Recipe");
                    _selectedItemData = AccessTools.Property(pairType, "ItemData");
                }

                if (requirements == null || pressed == null || _craftRecipe == null || _doCrafting == null)
                {
                    Log.Warn("[Supply] craft-from-claim bindings incomplete: HaveRequirements=" +
                             (requirements != null) + " OnCraftPressed=" + (pressed != null) +
                             " m_craftRecipe=" + (_craftRecipe != null) + " DoCrafting=" + (_doCrafting != null) +
                             "; crafting stays pocket-only.");
                    return false;
                }

                harmony.Patch(requirements,
                    postfix: new HarmonyMethod(typeof(CraftSupply), nameof(HaveRequirementsPostfix)));
                harmony.Patch(pressed,
                    prefix: new HarmonyMethod(typeof(CraftSupply), nameof(CraftPressedPrefix)));

                if (update != null)
                {
                    harmony.Patch(update,
                        postfix: new HarmonyMethod(typeof(CraftSupply), nameof(UpdatePostfix)));
                }

                Log.Info("[Supply] craft from the claim bound: a shortfall becomes a visible supply errand.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("[Supply] craft-from-claim binding failed: " + ex.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ the button

        /// <summary>
        /// Makes the craft button agree with the requirement row: a recipe the claim can finish is
        /// craftable. Only the item requirements are moved — the station and DLC checks still decide
        /// first, because a nisse cannot carry a forge to you.
        /// </summary>
        public static void HaveRequirementsPostfix(Player __instance, Recipe recipe, bool discover, int qualityLevel,
                                                   int amount, ref bool __result)
        {
            if (__result || discover || recipe == null || __instance == null) return;
            if (!ReferenceEquals(__instance, Player.m_localPlayer)) return;
            if (!Enabled() || recipe.m_requireOnlyOneIngredient) return;

            var shared = recipe.m_item != null && recipe.m_item.m_itemData != null
                ? recipe.m_item.m_itemData.m_shared
                : null;
            if (shared != null && !string.IsNullOrEmpty(shared.m_dlc) &&
                (DLCMan.instance == null || !DLCMan.instance.IsDLCInstalled(shared.m_dlc)))
            {
                return;
            }

            var player = __instance;
            if (player.NoCostCheat() ||
                (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost)))
            {
                return;
            }

            if (!player.RequiredCraftingStation(recipe, qualityLevel, true)) return;

            var items = SupplyService.Shortfall(player, recipe, qualityLevel, amount);
            if (items.Count == 0) return;
            if (!SupplyService.CanCover(player, items)) return;

            __result = true;
        }

        /// <summary>
        /// A press the pockets cannot pay becomes an errand. The craft is suppressed here and run
        /// again when the load lands, so no recipe is ever crafted before its materials are held.
        /// </summary>
        public static bool CraftPressedPrefix(InventoryGui __instance)
        {
            if (!Enabled() || __instance == null) return true;

            var player = Player.m_localPlayer;
            if (player == null) return true;

            var pair = _selectedRecipe != null ? _selectedRecipe.GetValue(__instance) : null;
            var recipe = pair != null && _selectedRecipeProperty != null
                ? _selectedRecipeProperty.GetValue(pair, null) as Recipe
                : null;
            var upgrade = pair != null && _selectedItemData != null
                ? _selectedItemData.GetValue(pair, null) as ItemDrop.ItemData
                : null;

            if (recipe == null || recipe.m_item == null) return true;
            if (recipe.m_requireOnlyOneIngredient) return true;

            if (player.NoCostCheat() ||
                (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost)))
            {
                return true;
            }

            var quality = upgrade == null ? 1 : upgrade.m_quality + 1;
            bool multi;
            var batch = Batch(__instance, upgrade, out multi);
            var items = SupplyService.Shortfall(player, recipe, quality, batch);
            if (items.Count == 0) return true;

            var claim = SupplyService.ClaimFor(player);
            if (claim == null || !SupplyService.CanCover(player, items))
            {
                // Nothing the claim can do: let vanilla refuse in its own words.
                return true;
            }

            if (!RoomFor(player, recipe, batch, items)) return false;

            long token;
            var answer = SupplyService.Request(player, claim, items, out token);
            if (answer.HasValue && answer.Value != FetchResult.Accepted)
            {
                Refuse(player, answer.Value, SupplyService.LastNote);
                return false;
            }

            Remember(token, recipe, quality, batch, upgrade, multi);
            SuppliesStarted++;
            player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyFetching));

            Log.Debug("Supply", "craft press deferred: recipe=" + recipe.m_item.name + " batch=" + batch +
                                " items=" + items.Count + " token=" + token + " claim=" + claim.BowlId);
            DiagnosticsFile.Line("[Supply] craft deferred recipe=" + recipe.m_item.name + " quality=" + quality +
                                 " batch=" + batch + " token=" + token + " claim=" + claim.BowlId);
            return false;
        }

        /// <summary>
        /// The batch the press is asking for, read the way the mods that own the count read it:
        /// vanilla's Alt-hold multi-craft first, then AAA Crafting's own batch field (AAA applies its
        /// batch by repeating a single craft, so `multi` is only true for vanilla's mode).
        /// </summary>
        private static int Batch(InventoryGui gui, ItemDrop.ItemData upgrade, out bool multi)
        {
            multi = false;
            if (upgrade != null) return 1;

            var touch = _touchMultiCrafting != null && _touchMultiCrafting.GetValue(gui) is bool held && held;
            if (ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyLStick") || touch)
            {
                multi = true;
                var amount = _multiAmount != null ? _multiAmount.GetValue(gui) : null;
                return amount is int value ? Mathf.Max(1, value) : 1;
            }

            return AaaBatch();
        }

        /// <summary>
        /// AAA Crafting's selected batch, when AAA is installed. AAA keeps its count on its own
        /// patch type (`AAACraft.amount`, or the text of `inputAmount`), not in `m_multiCraftAmount`,
        /// so a press with AAA's count at 20 must fetch for twenty.
        /// </summary>
        private static int AaaBatch()
        {
            if (_aaaScanned) return ReadAaa();
            _aaaScanned = true;

            try
            {
                BepInEx.PluginInfo info;
                if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(Husnisse.Compat.PluginGuards.AaaCraftingGuid, out info) ||
                    info == null || info.Instance == null)
                {
                    return 1;
                }

                var type = info.Instance.GetType().Assembly.GetType("AzuAntiArthriticCrafting.Patches.AAACraft");
                _aaaAmount = type != null ? AccessTools.Field(type, "amount") : null;
                _aaaInput = type != null ? AccessTools.Field(type, "inputAmount") : null;
            }
            catch (Exception ex)
            {
                Log.Debug("Supply", "AAA batch probe failed: " + ex.Message);
            }

            return ReadAaa();
        }

        private static int ReadAaa()
        {
            try
            {
                if (_aaaInput != null && _aaaInput.GetValue(null) is TMPro.TMP_InputField input && input != null)
                {
                    int parsed;
                    if (int.TryParse(input.text, out parsed) && parsed > 0) return parsed;
                }

                if (_aaaAmount != null && _aaaAmount.GetValue(null) is int amount && amount > 0) return amount;
            }
            catch
            {
                // AAA changed its shape; a single craft is the safe answer.
            }

            return 1;
        }

        /// <summary>
        /// Whether the load and the finished item both fit. Vanilla checks the product only when it
        /// crafts from the pockets; this press fetches first, so both halves have to fit or the load
        /// would arrive and the product would not.
        /// </summary>
        private static bool RoomFor(Player player, Recipe recipe, int batch,
                                    List<KeyValuePair<string, int>> items)
        {
            var inventory = player.GetInventory();
            var database = ObjectDB.instance;
            if (inventory == null || database == null) return true;

            var product = recipe.m_amount * Mathf.Max(1, batch);
            if (!inventory.CanAddItem(recipe.m_item.gameObject, product))
            {
                player.Message(MessageHud.MessageType.Center, "$inventory_full");
                return false;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var prefab = database.GetItemPrefab(items[i].Key);
                if (prefab == null) continue;
                if (inventory.CanAddItem(prefab, items[i].Value)) continue;

                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyNoRoom));
                return false;
            }

            return true;
        }

        private static void Remember(long token, Recipe recipe, int quality, int batch, ItemDrop.ItemData upgrade,
                                     bool multi)
        {
            var variant = 0;
            var gui = InventoryGui.instance;
            if (gui != null && _selectedVariant != null && _selectedVariant.GetValue(gui) is int selected) variant = selected;

            _pending = new Pending
            {
                Token = token,
                Item = recipe.m_item.m_itemData.m_shared.m_name,
                Quality = quality,
                Batch = Mathf.Max(1, batch),
                Variant = variant,
                Multi = multi,
                At = Time.time,
            };
            _hasPending = true;
        }

        // ------------------------------------------------------------------ the answer

        /// <summary>The server's verdict on the request: a refusal is said out loud and forgotten.</summary>
        public static void OnSupplyAnswer(long token, FetchResult result, string note)
        {
            if (result == FetchResult.Accepted) return;
            if (!_hasPending || _pending.Token != token) return;

            _hasPending = false;
            var player = Player.m_localPlayer;
            if (player == null) return;

            Refuse(player, result, note);
        }

        private static void Refuse(Player player, FetchResult result, string note)
        {
            if (player == null) return;

            switch (result)
            {
                case FetchResult.NotWorking: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderNotWorking)); break;
                case FetchResult.NotYours: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.BowlNotYours)); break;
                case FetchResult.TooFar: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.OrderTooFar)); break;
                case FetchResult.Busy:
                    if (Time.time - _lastBusyMessage > 2f)
                    {
                        _lastBusyMessage = Time.time;
                        player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyBusy));
                    }
                    break;
                case FetchResult.Disabled: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyOff)); break;
                case FetchResult.Missing: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyMissing)); break;
                default: player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyMissing)); break;
            }

            DiagnosticsFile.Line("[Supply] refused result=" + result + " note=" + (note ?? string.Empty));
            Log.Debug("Supply", "supply refused: " + result + " (" + (note ?? string.Empty) + ")");
        }

        /// <summary>
        /// The load is in the player's pockets. Craft exactly what was asked for, if the window is
        /// still open on the same recipe; otherwise the materials simply stay with the player.
        /// </summary>
        public static void OnSupplied(long token, int stacks)
        {
            if (!_hasPending || _pending.Token != token) return;

            var pending = _pending;
            _hasPending = false;
            SuppliesLanded++;

            var player = Player.m_localPlayer;
            var gui = InventoryGui.instance;
            if (player == null || gui == null || stacks <= 0)
            {
                if (player != null) player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyFetched));
                return;
            }

            if (!InventoryGui.IsVisible() || !SameSelection(gui, pending.Item))
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyFetched));
                DiagnosticsFile.Line("[Supply] delivered but the craft window moved on: token=" + token +
                                     " item=" + pending.Item);
                return;
            }

            Run(gui, player, pending);
        }

        private static bool SameSelection(InventoryGui gui, string item)
        {
            var pair = _selectedRecipe != null ? _selectedRecipe.GetValue(gui) : null;
            var recipe = pair != null && _selectedRecipeProperty != null
                ? _selectedRecipeProperty.GetValue(pair, null) as Recipe
                : null;
            if (recipe == null || recipe.m_item == null) return false;

            var shared = recipe.m_item.m_itemData != null ? recipe.m_item.m_itemData.m_shared : null;
            return shared != null && shared.m_name == item;
        }

        /// <summary>
        /// Runs the game's own craft for the deferred press. The fields vanilla's press would have
        /// set are set here (recipe, upgrade, variant, batch), then `DoCrafting` — the same method a
        /// finished craft timer calls — does the work: it re-checks the requirements, adds the item,
        /// consumes the materials and repaints the panel.
        /// </summary>
        private static void Run(InventoryGui gui, Player player, Pending pending)
        {
            try
            {
                var pair = _selectedRecipe != null ? _selectedRecipe.GetValue(gui) : null;
                var recipe = pair != null && _selectedRecipeProperty != null
                    ? _selectedRecipeProperty.GetValue(pair, null) as Recipe
                    : null;
                var upgrade = pair != null && _selectedItemData != null
                    ? _selectedItemData.GetValue(pair, null) as ItemDrop.ItemData
                    : null;

                _craftRecipe.SetValue(gui, recipe);
                if (_craftUpgradeItem != null) _craftUpgradeItem.SetValue(gui, upgrade);
                if (_craftVariant != null) _craftVariant.SetValue(gui, pending.Variant);
                if (_multiCrafting != null) _multiCrafting.SetValue(gui, pending.Multi);
                if (_multiAmount != null) _multiAmount.SetValue(gui, pending.Multi ? pending.Batch : 1);

                _doCrafting.Invoke(gui, new object[] { player });

                DiagnosticsFile.Line("[Supply] craft resumed item=" + pending.Item + " batch=" + pending.Batch);
                Log.Debug("Supply", "craft resumed item=" + pending.Item + " batch=" + pending.Batch);
            }
            catch (Exception ex)
            {
                Log.Warn("[Supply] could not finish the deferred craft: " + ex.Message);
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.SupplyFetched));
            }
        }

        /// <summary>Forgets a pending craft that nobody came back for, so a later token cannot match it.</summary>
        public static void UpdatePostfix()
        {
            CraftRepaint.FlushIfIdle(InventoryGui.instance);

            if (!_hasPending) return;
            if (Time.time - _pending.At < PendingSeconds) return;

            _hasPending = false;
            Log.Debug("Supply", "pending craft expired token=" + _pending.Token);
        }

        private static bool Enabled()
        {
            return HusnisseConfig.CraftSupply != null && HusnisseConfig.CraftSupply.Value;
        }
    }
}
