using System.Collections.Generic;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Claims
{
    /// <summary>
    /// Rebuilt from the world: every bowl registers itself in Awake, so a world load repopulates this
    /// without a scan or a Harmony patch. No per-frame work happens here.
    /// </summary>
    public static class ClaimRegistry
    {
        private static readonly Dictionary<ZDOID, Claim> Claims = new Dictionary<ZDOID, Claim>();
        private static readonly List<Claim> Buffer = new List<Claim>();

        public static void Register(Claim claim)
        {
            if (claim == null) return;
            Claims[claim.BowlId] = claim;
            Log.Debug("Claims", "registered " + claim.BowlId + " at " + claim.Center + " r=" + claim.Radius);
        }

        public static void Unregister(ZDOID bowlId)
        {
            if (Claims.Remove(bowlId)) Log.Debug("Claims", "unregistered " + bowlId);
        }

        public static bool OverlapsAny(Vector3 center, float radius, ZDOID ignore = default(ZDOID))
        {
            foreach (var claim in Claims.Values)
            {
                if (claim.BowlId == ignore) continue;
                if (claim.Overlaps(center, radius)) return true;
            }
            return false;
        }

        public static List<Claim> All()
        {
            Buffer.Clear();
            Buffer.AddRange(Claims.Values);
            return Buffer;
        }

        public static int Count => Claims.Count;
    }
}
