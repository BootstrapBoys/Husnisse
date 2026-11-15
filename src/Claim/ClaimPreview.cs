using Husnisse.Config;
using Husnisse.Localization;
using UnityEngine;

namespace Husnisse.Claims.Authority
{
    /// <summary>Why a claim may not stand: at placement, or after it settles.</summary>
    public enum ClaimRefusal
    {
        None = 0,
        WardNotPermitted,
        WardNotOwned,
        Overlap,
        /// <summary>A settled claim only: another bowl already holds this ward.</summary>
        WardHeld,
    }
}

namespace Husnisse.Claims
{
    using Husnisse.Claims.Authority;

    /// <summary>
    /// The claim a bowl would get if it were placed here, worked out before anything exists. One
    /// answer feeds three surfaces: the placement guard (refuse or allow), the ring drawn around the
    /// ghost, and `husnisse_preview` (the ledger's data seam). They cannot disagree because they all
    /// read this object.
    ///
    /// It answers with the same rules ClaimResolver applies to a settled bowl: a bowl inside a ward
    /// gets the ward's bubble and permits; outside every ward it gets its own radius and placer.
    /// </summary>
    public sealed class ClaimPreview
    {
        public Vector3 Position { get; private set; }
        public Vector3 Center { get; private set; }
        public float Radius { get; private set; }
        public ClaimSource Source { get; private set; }
        public ZDOID WardId { get; private set; }
        public long OwnerId { get; private set; }
        public bool Permitted { get; private set; }
        public ClaimRefusal Refusal { get; private set; }

        private ClaimPreview()
        {
            WardId = ZDOID.None;
            Source = ClaimSource.Native;
            Permitted = true;
            Refusal = ClaimRefusal.None;
        }

        public bool Placeable
        {
            get { return Refusal == ClaimRefusal.None; }
        }

        public string SourceName
        {
            get { return Source == ClaimSource.Ward ? "ward" : "native"; }
        }

        /// <summary>Machine-readable verdict: ok, ward_not_permitted, ward_not_owned, overlap.</summary>
        public string VerdictName
        {
            get
            {
                switch (Refusal)
                {
                    case ClaimRefusal.WardNotPermitted: return "ward_not_permitted";
                    case ClaimRefusal.WardNotOwned: return "ward_not_owned";
                    case ClaimRefusal.Overlap: return "overlap";
                    case ClaimRefusal.WardHeld: return "ward_held";
                    default: return "ok";
                }
            }
        }

        /// <summary>The player-facing message for a refusal, or null when the bowl may be placed.</summary>
        public string MessageKey
        {
            get
            {
                switch (Refusal)
                {
                    case ClaimRefusal.WardNotPermitted: return Loc.PlaceWard;
                    case ClaimRefusal.WardNotOwned: return Loc.PlaceOtherWard;
                    case ClaimRefusal.Overlap: return Loc.PlaceOverlap;
                    default: return null;
                }
            }
        }

        /// <summary>
        /// Answers for a bowl standing at <paramref name="position"/> placed by
        /// <paramref name="playerId"/>. <paramref name="nativeRadius"/> &lt;= 0 uses the configured
        /// radius, which is what the hammer placement path wants.
        /// </summary>
        public static ClaimPreview Evaluate(Vector3 position, long playerId, float nativeRadius = 0f)
        {
            var radius = nativeRadius > 0f ? nativeRadius : HusnisseConfig.ClaimRadius.Value;

            var preview = new ClaimPreview
            {
                Position = position,
                Center = position,
                Radius = radius,
                OwnerId = playerId,
            };

            ClaimResolution ward;
            if (HusnisseConfig.RespectWards.Value &&
                new WardAuthority().TryResolve(new ClaimAnchor(position, radius, playerId), out ward))
            {
                preview.Source = ClaimSource.Ward;
                preview.Center = ward.Center;
                preview.Radius = ward.Radius;
                preview.OwnerId = ward.OwnerId;
                preview.WardId = ward.WardId;
                preview.Permitted = ward.Permitted;

                if (!ward.Permitted)
                {
                    preview.Refusal = ClaimRefusal.WardNotPermitted;
                }
                else if (HusnisseConfig.RequireWardOwnership.Value &&
                         playerId != 0L && ward.OwnerId != 0L && ward.OwnerId != playerId)
                {
                    preview.Refusal = ClaimRefusal.WardNotOwned;
                }
            }

            // Overlap is judged on the ground the bowl would actually own: a second bowl in the same
            // ward would take the same bubble and therefore overlap the first.
            if (preview.Refusal == ClaimRefusal.None &&
                ClaimRegistry.OverlapsAny(preview.Center, preview.Radius))
            {
                preview.Refusal = ClaimRefusal.Overlap;
            }

            return preview;
        }

        public override string ToString()
        {
            return "preview pos=" + Position.x.ToString("0.#") + "," + Position.z.ToString("0.#") +
                   " center=" + Center.x.ToString("0.#") + "," + Center.z.ToString("0.#") +
                   " source=" + SourceName +
                   " ward=" + (WardId.IsNone() ? "0" : WardId.ToString()) +
                   " owner=" + OwnerId +
                   " r=" + Radius.ToString("0.#") +
                   " verdict=" + VerdictName;
        }
    }
}
