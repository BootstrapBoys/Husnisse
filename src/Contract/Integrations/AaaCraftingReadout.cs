using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// AAA Crafting's counts. AAA asks two questions of the world: `GetItemsInCloseContainers` (what
    /// the containers around the player hold) and `CalculateMaxCraftAmount` (how many fit in one
    /// batch). Both are answered from the claim, so its requirement rows and its batch ceiling show
    /// the base instead of the pockets.
    ///
    /// The data source is the only thing that changes from Quartermaster_Integrations' version: the
    /// patch targets are the same, and AAA still performs the craft itself. 0.4 answers; it does not
    /// spend, so a batch that only the claim could pay for is shown and will not craft until 0.5.
    /// </summary>
    public static class AaaCraftingReadout
    {
        private const int BatchCap = 100;

        private static bool _ready;
        private static FieldInfo _selectedRecipeField;
        private static PropertyInfo _recipeProperty;
        private static PropertyInfo _itemDataProperty;

        public static bool IsReady
        {
            get { return _ready; }
        }

        public static string State { get; private set; }

        public static bool Bind(Harmony harmony)
        {
            State = "absent";

            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(PluginGuards.AaaCraftingGuid, out info) ||
                info == null || info.Instance == null)
            {
                return false;
            }

            try
            {
                var assembly = info.Instance.GetType().Assembly;
                var pluginType = assembly.GetType("AzuAntiArthriticCrafting.AzuAntiArthriticCraftingPlugin");
                var utilities = assembly.GetType("AzuAntiArthriticCrafting.Utilities.Utilities");
                var craft = assembly.GetType("AzuAntiArthriticCrafting.Patches.AAACraft");

                if (utilities == null || craft == null)
                {
                    State = "types-missing";
                    Log.Warn("[Contract] AAA Crafting is installed but its Utilities/AAACraft types were not found; " +
                             "its counts stay pocket-only.");
                    return false;
                }

                var patched = 0;

                var closeContainers = AccessTools.Method(utilities, "GetItemsInCloseContainers", new[] { typeof(string) });
                if (closeContainers != null && closeContainers.ReturnType == typeof(int))
                {
                    harmony.Patch(closeContainers,
                        postfix: new HarmonyMethod(typeof(AaaCraftingReadout), nameof(CloseContainersPostfix)));
                    patched++;
                }

                var maxPostfix = new HarmonyMethod(typeof(AaaCraftingReadout), nameof(MaxCraftPostfix));
                patched += Patch(harmony, AccessTools.Method(craft, "CalculateMaxCraftAmount"), "AAACraft.CalculateMaxCraftAmount", maxPostfix);
                patched += Patch(harmony, AccessTools.Method(utilities, "CalculateMaxCraftAmount"), "Utilities.CalculateMaxCraftAmount", maxPostfix);

                _selectedRecipeField = AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
                var pairType = _selectedRecipeField != null ? _selectedRecipeField.FieldType : null;
                if (pairType != null)
                {
                    _recipeProperty = AccessTools.Property(pairType, "Recipe");
                    _itemDataProperty = AccessTools.Property(pairType, "ItemData");
                }

                // AAA only adds container counts to its own display when it believes a
                // container-crafting mod is present. Husnisse is that source now, so say so — the
                // flag is display-only (AAA's craft still runs through the game's own path).
                if (pluginType != null)
                {
                    var flag = AccessTools.Field(pluginType, "CfcLoaded");
                    if (flag != null)
                    {
                        flag.SetValue(null, true);
                        State = "bound";
                    }
                }

                _ready = patched > 0;
                State = _ready ? (State == "bound" ? "bound" : "bound-no-flag") : "patch-failed";

                if (!_ready)
                {
                    Log.Warn("[Contract] AAA Crafting was found but none of its count methods could be patched.");
                }

                return _ready;
            }
            catch (Exception ex)
            {
                State = "error";
                Log.Warn("[Contract] AAA Crafting binding failed: " + ex.Message);
                return false;
            }
        }

        private static int Patch(Harmony harmony, MethodInfo target, string name, HarmonyMethod postfix)
        {
            if (target == null)
            {
                Log.Warn("[Contract] " + name + " not found; that AAA count stays pocket-only.");
                return 0;
            }

            harmony.Patch(target, postfix: postfix);
            return 1;
        }

        /// <summary>Container-only, because AAA's callers add the player's own count themselves.</summary>
        public static void CloseContainersPostfix(string itemName, ref int __result)
        {
            if (!Enabled() || Player.m_localPlayer == null) return;

            var claim = StorageContract.CountClaim(itemName);
            if (claim <= 0) return;

            __result = (int)Math.Min(int.MaxValue, (long)__result + claim);
        }

        public static void MaxCraftPostfix(ref int __result)
        {
            if (!Enabled()) return;

            var gui = InventoryGui.instance;
            var player = Player.m_localPlayer;
            if (gui == null || player == null || !InventoryGui.IsVisible()) return;

            // AAA answers 0 for "cannot craft at all" (no station, under-levelled). Never raise that.
            if (__result <= 0) return;

            var pair = _selectedRecipeField != null ? _selectedRecipeField.GetValue(gui) : null;
            var recipe = pair != null && _recipeProperty != null ? _recipeProperty.GetValue(pair, null) as Recipe : null;
            var upgradeItem = pair != null && _itemDataProperty != null ? _itemDataProperty.GetValue(pair, null) as ItemDrop.ItemData : null;

            // Upgrades stay AAA's own answer: an upgrade is paid from the player's pockets, and the
            // claim's own material is not part of this release's spend path.
            if (recipe == null || gui.InUpradeTab() || upgradeItem != null) return;

            var noCost = player.NoCostCheat() ||
                         (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));

            if (noCost)
            {
                __result = BatchCap;
                return;
            }

            var trueMax = TrueMax(player, recipe);
            if (trueMax <= 0 && !player.HaveRequirements(recipe, false, 1))
            {
                __result = 0;
                return;
            }

            __result = Mathf.Clamp(Mathf.Max(__result, trueMax), 1, BatchCap);
        }

        /// <summary>
        /// How many crafts the reachable sources allow. Recipes that require only one ingredient take
        /// the sum of what each ingredient affords; everything else takes the smallest. Requirements
        /// the player does not know (modded dummy tokens) are skipped, and a recipe with nothing
        /// evaluable answers 1 so it stays usable.
        /// </summary>
        private static int TrueMax(Player player, Recipe recipe)
        {
            if (player == null || recipe == null || recipe.m_resources == null) return 0;

            var evaluated = false;
            var sum = 0;
            var min = int.MaxValue;

            for (var i = 0; i < recipe.m_resources.Length; i++)
            {
                var requirement = recipe.m_resources[i];
                if (requirement == null || requirement.m_resItem == null) continue;

                var perCraft = requirement.GetAmount(1);
                if (perCraft <= 0) continue;

                var sharedName = requirement.m_resItem.m_itemData != null
                    ? requirement.m_resItem.m_itemData.m_shared.m_name
                    : null;
                if (string.IsNullOrEmpty(sharedName) || !player.IsKnownMaterial(sharedName)) continue;

                evaluated = true;
                var possible = StorageContract.Count(sharedName) / perCraft;
                sum += possible;
                if (possible < min) min = possible;
            }

            if (!evaluated) return 1;
            return recipe.m_requireOnlyOneIngredient ? sum : Mathf.Max(0, min);
        }

        private static bool Enabled()
        {
            return _ready &&
                   HusnisseConfig.RequirementReadouts != null &&
                   HusnisseConfig.RequirementReadouts.Value;
        }
    }
}
