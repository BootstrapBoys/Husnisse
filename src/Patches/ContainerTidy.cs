using HarmonyLib;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Orders;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// Alt-interact on a chest inside a claim is a Tidy order: the nisse files what is in it. Normal
    /// interaction is untouched, so chests still open as chests.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    public static class ContainerTidy
    {
        private static bool Prefix(Container __instance, Humanoid character, bool hold, bool alt, ref bool __result)
        {
            if (hold || !alt) return true;
            if (!HusnisseConfig.AllowOrders.Value) return true;
            if (__instance == null) return true;

            var claim = ClaimRegistry.FindFor(__instance.transform.position);
            if (claim == null) return true;

            var player = character as Player;
            if (player == null) return true;

            __result = OrderService.Tidy(__instance, claim, player);
            return false;
        }
    }
}
