using HarmonyLib;
using Husnisse.Storage;

namespace Husnisse.Patches
{
    /// <summary>
    /// `ObjectDB.m_items` is empty when Jotunn raises `OnVanillaPrefabsAvailable`; the game copies
    /// the item list into the database immediately afterwards, through <c>CopyOtherDB</c>. This is
    /// the first moment a stack-size write can land on every item, so the ceiling is applied here.
    /// <see cref="StackSizes.Ensure"/> is idempotent and also retried from the flow pump, so a peer
    /// whose database arrives by another path still gets it.
    /// </summary>
    [HarmonyPatch(typeof(ObjectDB), "CopyOtherDB")]
    public static class StackSizesPatch
    {
        public static void Postfix()
        {
            StackSizes.Ensure();
        }
    }
}
