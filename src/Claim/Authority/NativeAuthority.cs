namespace Husnisse.Claims.Authority
{
    /// <summary>
    /// The claim every bowl has with no other mod installed: a radius around the bowl, owned by the
    /// player who placed it. This is the floor the mod must work at, so it never fails to answer.
    /// </summary>
    public sealed class NativeAuthority : IClaimAuthority
    {
        public ClaimSource Source
        {
            get { return ClaimSource.Native; }
        }

        public bool TryResolve(ClaimAnchor anchor, out ClaimResolution resolution)
        {
            resolution = new ClaimResolution
            {
                Source = ClaimSource.Native,
                Center = anchor.Position,
                Radius = anchor.NativeRadius,
                OwnerId = anchor.NativeOwnerId,
                WardId = ZDOID.None,
                Permitted = true,
            };
            return true;
        }
    }
}
