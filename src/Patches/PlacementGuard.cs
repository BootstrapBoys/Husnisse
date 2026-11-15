using HarmonyLib;
using Husnisse.Bowl;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Localization;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// Refuses a bowl before it is placed, rather than suppressing it afterwards. The decision comes
    /// from <see cref="ClaimPreview"/>, the same answer the ring under the ghost is drawn from, so the
    /// red ring and the refusal can never tell the player different stories.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    public static class PlacementGuard
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostField =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        private static bool Prefix(Player __instance, Piece piece)
        {
            if (__instance == null || piece == null) return true;
            if (Utils.GetPrefabName(piece.gameObject) != BowlPrefab.PrefabName) return true;

            var ghost = GhostField(__instance);
            var position = ghost != null ? ghost.transform.position : __instance.transform.position;

            var preview = ClaimPreview.Evaluate(position, __instance.GetPlayerID(), HusnisseConfig.ClaimRadius.Value);
            if (preview.Placeable) return true;

            __instance.Message(MessageHud.MessageType.Center, Loc.T(preview.MessageKey));
            return false;
        }
    }

    /// <summary>
    /// Draws the claim the bowl would get while the player holds it over the ground. Runs after the
    /// game has positioned the ghost for this frame, so the ring follows the aim instead of lagging
    /// one frame behind it. Only the local player draws; a dedicated server has no camera.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    public static class PlacementPreview
    {
        private static readonly AccessTools.FieldRef<Player, GameObject> GhostField =
            AccessTools.FieldRefAccess<Player, GameObject>("m_placementGhost");

        private static readonly Color Placeable = new Color(0.55f, 0.95f, 0.55f, 0.85f);
        private static readonly Color Refused = new Color(0.95f, 0.35f, 0.30f, 0.90f);

        private static void Postfix(Player __instance)
        {
            if (__instance == null || __instance != Player.m_localPlayer) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;

            var ghost = GhostField(__instance);
            if (ghost == null || !ghost.activeInHierarchy ||
                Utils.GetPrefabName(ghost) != BowlPrefab.PrefabName)
            {
                ClaimRing.Hide();
                return;
            }

            var preview = ClaimPreview.Evaluate(ghost.transform.position, __instance.GetPlayerID(),
                                                HusnisseConfig.ClaimRadius.Value);
            ClaimRing.Show(preview.Center, preview.Radius, preview.Placeable ? Placeable : Refused);
        }
    }
}
