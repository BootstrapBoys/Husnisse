using UnityEngine;

namespace Husnisse.Claims.Authority
{
    /// <summary>Where a claim's ground comes from. <see cref="ClaimSource"/> names are what the ZDO stores.</summary>
    public enum ClaimSource
    {
        Native = 0,
        Ward = 1,
    }

    /// <summary>One bowl, as the authorities see it: where it stands and what the native rule would say.</summary>
    public struct ClaimAnchor
    {
        public Vector3 Position;
        public float NativeRadius;
        public long NativeOwnerId;

        public ClaimAnchor(Vector3 position, float nativeRadius, long nativeOwnerId)
        {
            Position = position;
            NativeRadius = nativeRadius;
            NativeOwnerId = nativeOwnerId;
        }
    }

    /// <summary>An authority's answer for one anchor. Suppression is the resolver's job, not the provider's.</summary>
    public sealed class ClaimResolution
    {
        public ClaimSource Source = ClaimSource.Native;
        public Vector3 Center;
        public float Radius;
        public long OwnerId;
        public ZDOID WardId = ZDOID.None;

        /// <summary>True when the bowl's own player may use this ground (the ward permits them, or there is no ward).</summary>
        public bool Permitted = true;
    }

    /// <summary>
    /// A provider of claim ground. Exactly one authority owns a surface: when an installed mod governs
    /// the place a bowl stands, its answer replaces the native one and the two never run at once.
    /// Native is always available; a soft dependency is only consulted when it is installed and the
    /// point falls inside its ground. An authority that cannot answer returns false and the next one
    /// is asked, so a half-bound provider can never invent a claim.
    /// </summary>
    public interface IClaimAuthority
    {
        ClaimSource Source { get; }
        bool TryResolve(ClaimAnchor anchor, out ClaimResolution resolution);
    }
}
