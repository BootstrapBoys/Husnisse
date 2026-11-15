using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Core;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// AAABuildMenu's pinned build rows. The tracker counts the player's pockets per pinned recipe and
    /// only uses the number to choose a colour, so adding the claim makes the pinned row agree with
    /// the build menu without touching what a build spends. A container's own inventory never gains
    /// the claim: it already holds what it holds.
    /// </summary>
    public static class AaaBuildMenuReadout
    {
        private static bool _ready;

        public static string State { get; private set; }

        public static bool Bind(Harmony harmony)
        {
            State = "absent";

            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(PluginGuards.AaaBuildMenuGuid, out info) ||
                info == null || info.Instance == null)
            {
                return false;
            }

            try
            {
                var tracker = info.Instance.GetType().Assembly.GetType("AAABuildMenu.PinnedRecipeTracker");
                var countItem = tracker == null
                    ? null
                    : AccessTools.Method(tracker, "CountItem", new[] { typeof(Inventory), typeof(string) });

                if (countItem == null || countItem.ReturnType != typeof(int))
                {
                    State = "method-missing";
                    Log.Warn("[Contract] AAABuildMenu.PinnedRecipeTracker.CountItem was not found; " +
                             "pinned rows stay pocket-only.");
                    return false;
                }

                harmony.Patch(countItem,
                    postfix: new HarmonyMethod(typeof(AaaBuildMenuReadout), nameof(CountItemPostfix)));

                _ready = true;
                State = "bound";
                return true;
            }
            catch (Exception ex)
            {
                State = "error";
                Log.Warn("[Contract] AAABuildMenu binding failed: " + ex.Message);
                return false;
            }
        }

        public static void CountItemPostfix(Inventory inv, string sharedName, ref int __result)
        {
            if (!_ready) return;
            if (HusnisseConfig.RequirementReadouts == null || !HusnisseConfig.RequirementReadouts.Value) return;

            var player = Player.m_localPlayer;
            if (player == null || inv == null || string.IsNullOrEmpty(sharedName)) return;

            // Only the player's own inventory gains the claim. A container row would double-count
            // what that container already holds.
            if (!ReferenceEquals(inv, player.GetInventory())) return;

            var claim = StorageContract.CountClaim(sharedName);
            if (claim > 0) __result += claim;
        }
    }
}
