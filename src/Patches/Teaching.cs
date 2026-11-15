using System;
using HarmonyLib;
using Husnisse.Teaching;

namespace Husnisse.Patches
{
    /// <summary>
    /// Teaching watches the move, not the item: <see cref="Inventory.MoveItemToThis"/> is the only
    /// path a player's drag or shift-click takes from their grid into a chest (the two overloads are
    /// the drag target and the shift-click/take paths), so the observation cannot fire for the
    /// nisse's own <c>AddItem</c> work or for a chest the player merely opened.
    /// </summary>
    [HarmonyPatch]
    public static class TeachMovePatch
    {
        [HarmonyPatch(typeof(Inventory), "MoveItemToThis", new[] { typeof(Inventory), typeof(ItemDrop.ItemData) })]
        [HarmonyPostfix]
        public static void PostfixWhole(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            Try(__instance, fromInventory, item);
        }

        [HarmonyPatch(typeof(Inventory), "MoveItemToThis",
            new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) })]
        [HarmonyPostfix]
        public static void PostfixGrid(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
        {
            Try(__instance, fromInventory, item);
        }

        private static void Try(Inventory to, Inventory from, ItemDrop.ItemData item)
        {
            // A patch on a hot UI path must never throw into the game's inventory code.
            try
            {
                Teach.Observe(to, from, item);
            }
            catch (Exception ex)
            {
                Core.Log.Warn("teaching observation failed: " + ex.Message);
            }
        }
    }
}
