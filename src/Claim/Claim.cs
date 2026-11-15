using Husnisse.Claims.Authority;
using Husnisse.Compat;
using UnityEngine;

namespace Husnisse.Claims
{
    /// <summary>
    /// A nisse's territory: one bowl, one effective radius, one owner. The ground may come from the
    /// bowl itself (native) or from the WardIsLove ward the bowl stands in (ward). Claims never
    /// overlap: a second bowl placed inside one is refused by the placement check, and a second bowl
    /// in the same ward refuses to settle.
    /// </summary>
    public class Claim
    {
        public ZDOID BowlId { get; }

        /// <summary>Where the bowl piece stands. This never moves with the authority.</summary>
        public Vector3 BowlPosition { get; }

        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }
        public long OwnerId { get; private set; }
        public ClaimSource Source { get; private set; }
        public ZDOID WardId { get; private set; }

        /// <summary>The native rule's radius and owner, recorded when the bowl claims its ground.</summary>
        public float NativeRadius { get; }
        public long NativeOwnerId { get; }

        /// <summary>True once the native values are recorded in the bowl's ZDO as the fallback.</summary>
        public bool HasSnapshot { get; private set; }

        /// <summary>True when the bowl may not work: overlapping claim, or a ward it cannot use.</summary>
        public bool Suppressed { get; private set; }

        public ClaimRefusal RefusalCode { get; private set; }

        /// <summary>Why the claim is suppressed, in words; null when it works.</summary>
        public string Refusal { get; private set; }

        public Bowl.BowlPiece Bowl { get; }

        public Claim(ZDOID bowlId, Vector3 bowlPosition, float nativeRadius, long nativeOwnerId,
                     Bowl.BowlPiece bowl, bool hasSnapshot = false)
        {
            BowlId = bowlId;
            BowlPosition = bowlPosition;
            NativeRadius = nativeRadius;
            NativeOwnerId = nativeOwnerId;
            Bowl = bowl;
            HasSnapshot = hasSnapshot;

            Source = ClaimSource.Native;
            Center = bowlPosition;
            Radius = nativeRadius;
            OwnerId = nativeOwnerId;
            WardId = ZDOID.None;
        }

        public string SourceName
        {
            get { return Source == ClaimSource.Ward ? "ward" : "native"; }
        }

        /// <summary>Applies a resolved authority. A refusal marks the claim suppressed but keeps its ground.
        /// Returns true when the effective answer changed.</summary>
        public bool Apply(ClaimResolution resolution, ClaimRefusal refusalCode, string refusal)
        {
            if (resolution == null) return false;

            var suppressed = refusalCode != ClaimRefusal.None;

            var changed = Source != resolution.Source ||
                          !WardId.Equals(resolution.Source == ClaimSource.Ward ? resolution.WardId : ZDOID.None) ||
                          Radius != resolution.Radius ||
                          OwnerId != resolution.OwnerId ||
                          Center != resolution.Center ||
                          Suppressed != suppressed ||
                          RefusalCode != refusalCode ||
                          Refusal != refusal;

            Source = resolution.Source;
            Center = resolution.Center;
            Radius = resolution.Radius;
            OwnerId = resolution.OwnerId;
            WardId = resolution.Source == ClaimSource.Ward ? resolution.WardId : ZDOID.None;
            Suppressed = suppressed;
            RefusalCode = refusalCode;
            Refusal = refusal;

            // The native values are the fallback the moment a ward takes over.
            if (Source == ClaimSource.Ward) HasSnapshot = true;

            return changed;
        }

        /// <summary>
        /// True when this player may feed, teach or order from the claim. The ward owner and the
        /// players the ward permits act for a ward claim; the placer acts for a native one. An
        /// unknown player (0) is never refused — the server and tools have no identity to check.
        /// </summary>
        public bool Allows(long playerId)
        {
            if (playerId == 0L) return true;
            if (Suppressed) return false;
            if (OwnerId == 0L) return true;
            if (playerId == OwnerId) return true;
            if (Source == ClaimSource.Ward) return WardIsLoveCompat.Permits(WardId, playerId);
            return false;
        }

        public bool Contains(Vector3 point)
        {
            var dx = point.x - Center.x;
            var dz = point.z - Center.z;
            return dx * dx + dz * dz <= Radius * Radius;
        }

        public bool Overlaps(Vector3 center, float radius)
        {
            var dx = center.x - Center.x;
            var dz = center.z - Center.z;
            var reach = Radius + radius;
            return dx * dx + dz * dz < reach * reach;
        }
    }
}
