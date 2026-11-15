using HarmonyLib;
using Husnisse.Bowl;
using Husnisse.Claims;
using Husnisse.Claims.Authority;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Localization;
using UnityEngine;

namespace Husnisse.Patches
{
    /// <summary>
    /// Refuses a bowl before it is placed, rather than suppressing it afterwards. Same rules as the
    /// registry, judged on the claim the bowl would actually get: inside a ward that is the ward's
    /// bubble, so a second bowl anywhere in the same ward is refused.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    public static class PlacementGuard
    {
        private static readonly WardAuthority Ward = new WardAuthority();

        private static bool Prefix(Player __instance, Piece piece)
        {
            if (__instance == null || piece == null) return true;
            if (Utils.GetPrefabName(piece.gameObject) != BowlPrefab.PrefabName) return true;

            var ghostField = AccessTools.Field(typeof(Player), "m_placementGhost");
            var ghost = ghostField != null ? ghostField.GetValue(__instance) as GameObject : null;
            var position = ghost != null ? ghost.transform.position : __instance.transform.position;
            var playerId = __instance.GetPlayerID();

            var center = position;
            var radius = HusnisseConfig.ClaimRadius.Value;
            ClaimResolution ward = null;

            if (HusnisseConfig.RespectWards.Value &&
                Ward.TryResolve(new ClaimAnchor(position, radius, playerId), out ward))
            {
                center = ward.Center;
                radius = ward.Radius;

                if (!ward.Permitted)
                {
                    __instance.Message(MessageHud.MessageType.Center, Loc.T(Loc.PlaceWard));
                    return false;
                }

                if (HusnisseConfig.RequireWardOwnership.Value &&
                    ward.OwnerId != 0L && playerId != 0L && ward.OwnerId != playerId)
                {
                    __instance.Message(MessageHud.MessageType.Center, Loc.T(Loc.PlaceOtherWard));
                    return false;
                }
            }

            if (ClaimRegistry.OverlapsAny(center, radius))
            {
                __instance.Message(MessageHud.MessageType.Center, Loc.T(Loc.PlaceOverlap));
                return false;
            }

            return true;
        }
    }
}
