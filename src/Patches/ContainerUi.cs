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

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
    public static class ContainerUiHidePatch
    {
        private static void Postfix()
        {
            ContainerRulesUi.ClosePanel();
        }
    }
}
