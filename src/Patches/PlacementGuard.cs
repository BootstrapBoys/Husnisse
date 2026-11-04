using HarmonyLib;
using Husnisse.Bowl;
using Husnisse.Claims;
using Husnisse.Compat;
using Husnisse.Config;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// Refuses a bowl before it is placed, rather than suppressing it afterwards. Same rules as the
    /// claim registry: no overlap, and no ward the placer cannot use.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    public static class PlacementGuard
    {
        private static bool Prefix(Player __instance, Piece piece)
        {
            if (__instance == null || piece == null) return true;
            if (Utils.GetPrefabName(piece.gameObject) != BowlPrefab.PrefabName) return true;

            var ghostField = AccessTools.Field(typeof(Player), "m_placementGhost");
            var ghost = ghostField != null ? ghostField.GetValue(__instance) as GameObject : null;
            var position = ghost != null ? ghost.transform.position : __instance.transform.position;

            if (ClaimRegistry.OverlapsAny(position, HusnisseConfig.ClaimRadius.Value))
            {
                __instance.Message(MessageHud.MessageType.Center, "Another nisse already claims this ground.");
                return false;
            }

            var playerId = __instance.GetPlayerID();

            if (HusnisseConfig.RespectWards.Value && !WardIsLoveCompat.IsPermitted(playerId, position))
            {
                __instance.Message(MessageHud.MessageType.Center, "You are not permitted to build here.");
                return false;
            }

            if (HusnisseConfig.RespectWards.Value && HusnisseConfig.RequireWardOwnership.Value)
            {
                long wardOwner;
                if (WardIsLoveCompat.TryGetWardOwner(position, out wardOwner) && wardOwner != 0L && wardOwner != playerId)
                {
                    __instance.Message(MessageHud.MessageType.Center, "Another player's ward holds this ground.");
                    return false;
                }
            }

            return true;
        }
    }
}
