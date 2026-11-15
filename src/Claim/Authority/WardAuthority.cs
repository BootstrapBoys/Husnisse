using System.Collections.Generic;
using Husnisse.Compat;
using UnityEngine;

namespace Husnisse.Claims.Authority
{
    /// <summary>
    /// A bowl standing inside a WardIsLove ward uses that ward's ground: its bubble, its owner, its
    /// permission list. A bowl outside every ward never consults WardIsLove at all.
    ///
    /// Overlapping wards: the ward owned by the bowl's placer wins; if neither is theirs, the smallest
    /// containing ward. Equal radius falls back to the ward id, so the answer is stable across runs.
    /// </summary>
    public sealed class WardAuthority : IClaimAuthority
    {
        private static readonly List<WardInfo> Buffer = new List<WardInfo>();

        public ClaimSource Source
        {
            get { return ClaimSource.Ward; }
        }

        public bool TryResolve(ClaimAnchor anchor, out ClaimResolution resolution)
        {
            resolution = null;
            if (!WardIsLoveCompat.Installed && !WardIsLoveCompat.Bound) return false;

            WardIsLoveCompat.Containing(anchor.Position, Buffer);
            if (Buffer.Count == 0) return false;

            var ward = Select(Buffer, anchor.NativeOwnerId);
            resolution = new ClaimResolution
            {
                Source = ClaimSource.Ward,
                Center = ward.Center,
                Radius = ward.Radius,
                OwnerId = ward.OwnerId,
                WardId = ward.Id,
                Permitted = Permitted(ward, anchor.NativeOwnerId),
            };
            return true;
        }

        private static WardInfo Select(List<WardInfo> wards, long placerId)
        {
            var best = wards[0];
            var bestOwned = placerId != 0L && best.OwnerId == placerId;

            for (var i = 1; i < wards.Count; i++)
            {
                var ward = wards[i];
                var owned = placerId != 0L && ward.OwnerId == placerId;

                if (owned && !bestOwned)
                {
                    best = ward;
                    bestOwned = true;
                    continue;
                }

                if (owned != bestOwned) continue;
                if (!Smaller(ward, best)) continue;
                best = ward;
            }

            return best;
        }

        private static bool Smaller(WardInfo candidate, WardInfo current)
        {
            if (candidate.Radius < current.Radius) return true;
            if (candidate.Radius > current.Radius) return false;
            return candidate.Id.CompareTo(current.Id) < 0;
        }

        private static bool Permitted(WardInfo ward, long playerId)
        {
            if (playerId == 0L) return true;
            if (ward.OwnerId == playerId) return true;
            return WardIsLoveCompat.Permits(ward.Id, playerId);
        }
    }
}
