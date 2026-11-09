using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Husnisse.Bowl
{
    /// <summary>
    /// The bowl's inventory accepts food and nothing else. The container itself has no item-type
    /// filter, so the guard lives on the inventory methods the UI calls while moving items.
    /// </summary>
    public static class BowlFoodGuard
    {
        private static readonly HashSet<Inventory> BowlInventories = new HashSet<Inventory>();

        public static void Register(Container container)
        {
            var inventory = container != null ? container.GetInventory() : null;
            if (inventory != null) BowlInventories.Add(inventory);
        }

        public static void Unregister(Container container)
        {
            var inventory = container != null ? container.GetInventory() : null;
            if (inventory != null) BowlInventories.Remove(inventory);
        }

        public static bool IsBowl(Inventory inventory)
        {
            return inventory != null && BowlInventories.Contains(inventory);
        }

        public static bool IsFood(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null && item.m_shared.m_food > 0f;
        }
    }

    [HarmonyPatch(typeof(Inventory), "CanAddItem", new Type[] { typeof(ItemDrop.ItemData), typeof(int) })]
    public static class BowlCanAddItemPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!BowlFoodGuard.IsBowl(__instance) || BowlFoodGuard.IsFood(item)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", new Type[] { typeof(ItemDrop.ItemData) })]
    public static class BowlAddItemPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!BowlFoodGuard.IsBowl(__instance) || BowlFoodGuard.IsFood(item)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), "AddItem", new Type[] { typeof(ItemDrop.ItemData), typeof(Vector2i) })]
    public static class BowlAddItemAtPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!BowlFoodGuard.IsBowl(__instance) || BowlFoodGuard.IsFood(item)) return true;
            __result = false;
            return false;
        }
    }
}
