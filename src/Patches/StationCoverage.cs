using HarmonyLib;
using Husnisse.Build;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// The single gate vanilla uses for building, structure repair and dismantling. A postfix that
    /// only fills a null result leaves every vanilla answer — a station genuinely in range, or a
    /// station of the wrong kind — exactly as it was; only the claim's own radius can add one.
    /// </summary>
    [HarmonyPatch(typeof(CraftingStation), "HaveBuildStationInRange")]
    public static class StationCoveragePatch
    {
        public static void Postfix(string name, Vector3 point, ref CraftingStation __result)
        {
            if (__result != null) return;
            __result = StationCoverage.Find(name, point);
        }
    }
}
