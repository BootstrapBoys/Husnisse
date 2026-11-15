using Husnisse.Claims.Authority;
using Husnisse.Localization;

namespace Husnisse.Claims
{
    /// <summary>Turns a refusal code into the localised phrase a player sees.</summary>
    public static class ClaimText
    {
        public static string KeyFor(ClaimRefusal refusal)
        {
            switch (refusal)
            {
                case ClaimRefusal.WardNotPermitted: return Loc.ClaimRefusedWard;
                case ClaimRefusal.WardNotOwned: return Loc.ClaimRefusedOwner;
                case ClaimRefusal.WardHeld: return Loc.ClaimRefusedHeld;
                case ClaimRefusal.Overlap: return Loc.PlaceOverlap;
                default: return null;
            }
        }

        public static string SourceKey(ClaimSource source)
        {
            return source == ClaimSource.Ward ? Loc.ClaimSourceWard : Loc.ClaimSourceNative;
        }
    }
}
