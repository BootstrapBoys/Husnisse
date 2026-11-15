using System.Collections.Generic;
using Husnisse.Claims.Authority;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Claims
{
    /// <summary>
    /// Rebuilt from the world: every bowl registers itself in Awake, so a world load repopulates this
    /// without a scan or a Harmony patch. Authority is re-resolved in a stable bowl-id order, so a
    /// contested ward always answers the same way and two bowls can never swap it back and forth.
    /// </summary>
    public static class ClaimRegistry
    {
        private static readonly Dictionary<ZDOID, Claim> Claims = new Dictionary<ZDOID, Claim>();
        private static readonly List<Claim> Buffer = new List<Claim>();
        private static readonly List<Claim> Ordered = new List<Claim>();
        private static readonly Dictionary<ZDOID, ZDOID> WardHolders = new Dictionary<ZDOID, ZDOID>();

        public static void Register(Claim claim)
        {
            if (claim == null) return;

            Claims[claim.BowlId] = claim;
            RevalidateAll();
            Log.Debug("Claims", "registered " + claim.BowlId + " source=" + claim.SourceName +
                                " ward=" + claim.WardId + " r=" + claim.Radius.ToString("0") +
                                " owner=" + claim.OwnerId + " suppressed=" + claim.Suppressed);
            DiagnosticsFile.Dump("claim registered");
        }

        public static void Unregister(ZDOID bowlId)
        {
            if (!Claims.Remove(bowlId)) return;

            Log.Debug("Claims", "unregistered " + bowlId);
            RevalidateAll();
            DiagnosticsFile.Dump("claim removed");
        }

        /// <summary>
        /// Re-resolves every claim against the wards that exist now and persists the answer through
        /// each bowl. Returns how many claims changed; the live switch is this call on a slow timer.
        /// </summary>
        public static int RevalidateAll()
        {
            Ordered.Clear();
            Ordered.AddRange(Claims.Values);
            Ordered.Sort(CompareBowls);

            WardHolders.Clear();

            var changed = 0;
            foreach (var claim in Ordered)
            {
                if (claim == null) continue;

                if (ClaimResolver.Apply(claim, WardHolders))
                {
                    changed++;
                    Log.Debug("Claims", "resolved " + claim.BowlId + " source=" + claim.SourceName +
                                        " ward=" + claim.WardId + " r=" + claim.Radius.ToString("0") +
                                        " owner=" + claim.OwnerId + " suppressed=" + claim.Suppressed +
                                        (claim.Refusal != null ? " (" + claim.Refusal + ")" : string.Empty));
                }

                if (claim.Bowl != null) claim.Bowl.OnClaimResolved(claim);
            }

            return changed;
        }

        public static Claim Find(ZDOID bowlId)
        {
            Claim claim;
            return Claims.TryGetValue(bowlId, out claim) ? claim : null;
        }

        /// <summary>The bowl that holds a ward, or null. One bowl per ward by construction.</summary>
        public static Claim FindByWard(ZDOID wardId)
        {
            foreach (var claim in Claims.Values)
            {
                if (claim.Source == ClaimSource.Ward && claim.WardId.Equals(wardId)) return claim;
            }
            return null;
        }

        /// <summary>Which claim owns a point; used by machines and point-based features from 0.4.</summary>
        public static Claim FindFor(Vector3 point)
        {
            Claim best = null;
            foreach (var claim in Claims.Values)
            {
                if (claim.Suppressed || !claim.Contains(point)) continue;
                if (best == null || claim.Radius < best.Radius) best = claim;
            }
            return best;
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
            Buffer.Sort(CompareBowls);
            return Buffer;
        }

        public static int Count
        {
            get { return Claims.Count; }
        }

        /// <summary>Claims that are actually working. Diagnostics only.</summary>
        public static int WorkingCount
        {
            get
            {
                var count = 0;
                foreach (var claim in Claims.Values)
                {
                    if (!claim.Suppressed) count++;
                }
                return count;
            }
        }

        private static int CompareBowls(Claim a, Claim b)
        {
            if (a == null) return b == null ? 0 : -1;
            if (b == null) return 1;
            return a.BowlId.CompareTo(b.BowlId);
        }
    }
}
