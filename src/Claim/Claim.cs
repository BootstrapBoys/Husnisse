using UnityEngine;

namespace Husnisse.Claims
{
    /// <summary>
    /// A nisse's territory: one bowl, one radius, one owner. Claims never overlap; a second bowl
    /// placed inside one is refused by the placement check in <see cref="ClaimRegistry"/>.
    /// </summary>
    public class Claim
    {
        public ZDOID BowlId { get; }
        public Vector3 Center { get; }
        public float Radius { get; }
        public long OwnerId { get; }
        public Bowl.BowlPiece Bowl { get; }

        /// <summary>True when the bowl may not work: overlapping claim, or inside a ward its owner cannot use.</summary>
        public bool Suppressed { get; }

        public Claim(ZDOID bowlId, Vector3 center, float radius, long ownerId, Bowl.BowlPiece bowl, bool suppressed = false)
        {
            BowlId = bowlId;
            Center = center;
            Radius = radius;
            OwnerId = ownerId;
            Bowl = bowl;
            Suppressed = suppressed;
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
