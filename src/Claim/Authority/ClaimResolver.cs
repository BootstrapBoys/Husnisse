using System.Collections.Generic;
using Husnisse.Config;
using UnityEngine;

namespace Husnisse.Claims.Authority
{
    /// <summary>
    /// Decides which authority governs one bowl. The ward provider replaces the native one; it never
    /// runs beside it. A bowl that cannot settle keeps its ward ground but is marked suppressed, so
    /// nothing works while the refusal stands and the reason survives in diagnostics.
    /// </summary>
    public static class ClaimResolver
    {
        private static readonly NativeAuthority Native = new NativeAuthority();
        private static readonly WardAuthority Ward = new WardAuthority();

        /// <summary>
        /// Applies the effective authority to one claim. <paramref name="wardHolders"/> maps a ward to
        /// the bowl that already holds it; callers walk claims in a stable order and pass the same
        /// dictionary for the whole pass, which is what enforces one bowl per ward.
        /// </summary>
        public static bool Apply(Claim claim, Dictionary<ZDOID, ZDOID> wardHolders)
        {
            if (claim == null) return false;

            var anchor = new ClaimAnchor(claim.BowlPosition, claim.NativeRadius, claim.NativeOwnerId);
            ClaimResolution resolution = null;
            string refusal = null;

            if (HusnisseConfig.RespectWards.Value && Ward.TryResolve(anchor, out resolution))
            {
                if (!resolution.Permitted)
                {
                    refusal = "ward does not permit the bowl's owner";
                }
                else if (HusnisseConfig.RequireWardOwnership.Value &&
                         anchor.NativeOwnerId != 0L && resolution.OwnerId != 0L &&
                         resolution.OwnerId != anchor.NativeOwnerId)
                {
                    refusal = "ward belongs to another player";
                }
                else if (wardHolders != null)
                {
                    ZDOID holder;
                    if (wardHolders.TryGetValue(resolution.WardId, out holder) && holder != claim.BowlId)
                    {
                        refusal = "another bowl already holds this ward";
                    }
                    else
                    {
                        wardHolders[resolution.WardId] = claim.BowlId;
                    }
                }
            }

            if (resolution == null) Native.TryResolve(anchor, out resolution);

            return claim.Apply(resolution, refusal);
        }
    }
}
