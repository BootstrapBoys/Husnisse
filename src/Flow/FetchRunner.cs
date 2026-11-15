using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// Holds the one supply errand each claim may have in flight, and keeps the deposit queue from
    /// starting a second errand on the same nisse. A claim has one pair of hands: a fetch runs alone,
    /// and the queue waits for it like it waits for a trip.
    /// </summary>
    public static class FetchRunner
    {
        private static readonly Dictionary<ZDOID, Fetch> Active = new Dictionary<ZDOID, Fetch>();

        public static bool Has(Claim claim)
        {
            if (claim == null) return false;
            Fetch fetch;
            return Active.TryGetValue(claim.BowlId, out fetch) && fetch != null && !fetch.Finished;
        }

        public static Fetch Get(Claim claim)
        {
            if (claim == null) return null;
            Fetch fetch;
            return Active.TryGetValue(claim.BowlId, out fetch) ? fetch : null;
        }

        /// <summary>Starts an errand unless the claim is already walking for a deposit or a fetch.</summary>
        public static bool Start(Claim claim, Fetch fetch)
        {
            if (claim == null || fetch == null) return false;
            if (Has(claim))
            {
                Log.Debug("Flow", "supply: claim " + claim.BowlId + " already has a supply errand");
                return false;
            }
            if (TripRunner.Has(claim))
            {
                Log.Debug("Flow", "supply: claim " + claim.BowlId + " is on a deposit trip; the fetch waits");
                return false;
            }

            Active[claim.BowlId] = fetch;
            DiagnosticsFile.Line("[Supply] errand start token=" + fetch.Token + " player=" + fetch.PlayerId +
                                 " stacks=" + fetch.Planned + " claim=" + claim.BowlId);
            return true;
        }

        public static void Tick(float dt)
        {
            if (Active.Count == 0) return;

            List<ZDOID> finished = null;
            foreach (var pair in Active)
            {
                var fetch = pair.Value;
                if (fetch == null || fetch.Finished)
                {
                    if (finished == null) finished = new List<ZDOID>();
                    finished.Add(pair.Key);
                    continue;
                }

                fetch.Tick(dt);
            }

            if (finished == null) return;
            foreach (var id in finished)
            {
                var fetch = Active[id];
                Active.Remove(id);
                if (fetch != null)
                {
                    DiagnosticsFile.Line("[Supply] errand done token=" + fetch.Token + " player=" + fetch.PlayerId);
                }
            }
        }

        /// <summary>The errand a client's handover answer belongs to.</summary>
        public static Fetch ByToken(long token)
        {
            foreach (var pair in Active)
            {
                if (pair.Value != null && pair.Value.Token == token) return pair.Value;
            }
            return null;
        }

        public static Fetch ForPlayer(long playerId)
        {
            foreach (var pair in Active)
            {
                if (pair.Value != null && pair.Value.PlayerId == playerId) return pair.Value;
            }
            return null;
        }

        public static void Forget(ZDOID bowlId)
        {
            Fetch fetch;
            if (Active.TryGetValue(bowlId, out fetch) && fetch != null) fetch.Abort();
            Active.Remove(bowlId);
        }

        public static void Reset()
        {
            foreach (var pair in Active)
            {
                if (pair.Value != null) pair.Value.Abort();
            }
            Active.Clear();
        }

        public static int Count => Active.Count;

        /// <summary>One line for `husnisse_status` and the diagnostics dump. The count leads so a
        /// gate can assert "the errand is over" on the newest value.</summary>
        public static string Describe()
        {
            if (Active.Count == 0) return "supply errands=0";

            var text = "supply errands=" + Active.Count;
            foreach (var pair in Active)
            {
                var fetch = pair.Value;
                if (fetch == null) continue;
                text += " phase=" + fetch.PhaseName + " token=" + fetch.Token + " player=" + fetch.PlayerId +
                        " carried=" + fetch.Carried + " planned=" + fetch.Planned;
            }
            return text;
        }
    }
}
