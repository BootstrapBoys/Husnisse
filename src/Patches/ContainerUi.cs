using HarmonyLib;
using Husnisse.UI;

namespace Husnisse.Patches
{
    /// <summary>
    /// The container window is drawn by InventoryGui; UpdateContainer is the one method that runs
    /// every frame while a container is open and knows which container it is. The buttons live as
    /// children of the container panel, so closing the window hides them with it. A dedicated server
    /// has no InventoryGui instance, so nothing here runs there.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "UpdateContainer")]
    public static class ContainerUiPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            ContainerRulesUi.Refresh(__instance);
        }
    }

    /// <summary>
    /// The ledger lives in the inventory window, so it follows the inventory: the key is read only
    /// while the inventory is visible (world input never sees it), and the panel is a child of the
    /// inventory root so it cannot outlive it.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "Update")]
    public static class LedgerUiPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            LedgerUi.KeyTick(__instance);
            LedgerUi.Refresh(__instance);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    public static class ContainerUiHidePatch
    {
        private static void Postfix()
        {
            ContainerRulesUi.ClosePanel();
            LedgerUi.Close();
        }
    }
}
