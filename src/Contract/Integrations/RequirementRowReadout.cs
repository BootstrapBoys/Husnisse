using System;
using HarmonyLib;
using Husnisse.Config;
using Husnisse.Core;
using TMPro;
using UnityEngine;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// The crafting and build requirement rows. Vanilla writes the needed amount and compares it with
    /// the player's pockets; this postfix writes the same amount followed by what the player can
    /// actually reach, so a recipe in a base reads `10 (42 [12+30])` — twelve carried, thirty in the
    /// claim.
    ///
    /// Only the display moves: 0.4 answers questions, it does not spend. A row with no claim stock
    /// behind it is left exactly as vanilla wrote it, so the mod is invisible outside a base, and a
    /// row another mod has already formatted is deferred to rather than overwritten.
    /// </summary>
    public static class RequirementRowReadout
    {
        public const string SufficientColour = "ADE8E6";
        public const string InsufficientColour = "FF7A6B";

        private static bool _deferLogged;

        public static bool Bind(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(InventoryGui), "SetupRequirement", new[]
            {
                typeof(Transform), typeof(Piece.Requirement), typeof(Player), typeof(bool), typeof(int), typeof(int)
            });

            if (target == null)
            {
                Log.Warn("[Contract] InventoryGui.SetupRequirement(6) not found; requirement rows stay pocket-only.");
                return false;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(typeof(RequirementRowReadout), nameof(Postfix)));
            return true;
        }

        public static void Postfix(Transform elementRoot, Piece.Requirement req, Player player, bool craft,
                                   int quality, int craftMultiplier, bool __result)
        {
            if (!__result) return;
            if (elementRoot == null || req == null || req.m_resItem == null) return;
            if (HusnisseConfig.RequirementReadouts == null || !HusnisseConfig.RequirementReadouts.Value) return;

            var local = Player.m_localPlayer;
            if (local == null || player == null || !ReferenceEquals(player, local)) return;

            var amount = req.GetAmount(quality) * Mathf.Max(1, craftMultiplier);
            if (amount <= 0) return;

            var noCost = player.NoCostCheat() ||
                         (ZoneSystem.instance != null &&
                          ZoneSystem.instance.GetGlobalKey(craft ? GlobalKeys.NoCraftCost : GlobalKeys.NoBuildCost));
            if (noCost) return;

            var shared = req.m_resItem.m_itemData != null ? req.m_resItem.m_itemData.m_shared : null;
            var sharedName = shared != null ? shared.m_name : null;
            if (string.IsNullOrEmpty(sharedName)) return;

            var claim = StorageContract.CountClaim(sharedName);
            if (claim <= 0) return;

            var carried = StorageContract.CountCarried(sharedName);
            var total = carried + claim;

            var row = elementRoot.Find("res_amount");
            var label = row != null ? row.GetComponent<TMP_Text>() : null;
            if (label == null) return;

            // Another mod's number is its own: vanilla writes a bare integer, so anything else was
            // formatted by someone else (MyLittleUI, AAA). Husnisse's own previous write is never
            // seen here — vanilla rewrites the bare number immediately before this postfix.
            int _;
            if (!string.IsNullOrEmpty(label.text) && !int.TryParse(label.text.Trim(), out _))
            {
                if (!_deferLogged)
                {
                    _deferLogged = true;
                    Log.Info("[Contract] a requirement row is formatted by another mod; deferring to it.");
                }
                return;
            }

            var split = HusnisseConfig.SplitRequirementCounts != null && HusnisseConfig.SplitRequirementCounts.Value;
            var available = split
                ? total + " [" + carried + "+" + claim + "]"
                : total.ToString();

            var colour = total >= amount ? SufficientColour : InsufficientColour;
            label.text = amount + " <color=#" + colour + ">(" + available + ")</color>";
            label.color = Color.white;
        }
    }
}
