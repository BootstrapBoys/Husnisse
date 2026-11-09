using HarmonyLib;
using Husnisse.Bowl;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Orders;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// The bowl is a container, so its own Alt handling lives here: Alt toggles the feeding policy,
    /// plain interact opens the four food slots. For every other container, Alt is the Tidy order.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    public static class ContainerTidy
    {
        private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (hold || __instance == null) return true;

            var bowl = __instance.GetComponent<BowlPiece>();
            if (bowl != null)
            {
                if (!alt) return true;

                bowl.ToggleFeeding(character);
                __result = true;
                return false;
            }

            if (!alt) return true;
            if (!HusnisseConfig.AllowOrders.Value) return true;

            var claim = ClaimRegistry.FindFor(__instance.transform.position);
            Log.Debug("Compat", "tidy hit alt=" + alt + " container=" + __instance.name +
                                " claim=" + (claim != null ? claim.BowlId.ToString() : "none"));
            if (claim == null) return true;

            var player = character as Player;
            if (player == null) return true;

            __result = OrderService.RequestTidy(player, claim, __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    public static class BowlHover
    {
        private static bool Prefix(Container __instance, ref string __result)
        {
            var bowl = __instance != null ? __instance.GetComponent<BowlPiece>() : null;
            if (bowl == null) return true;

            __result = bowl.HoverText();
            return false;
        }
    }
}
