using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// Holds the one trip each claim may have in flight. The pump starts trips; this keeps them
    /// ticked and forgets them when their bowl goes away.
    /// </summary>
    public static class TripRunner
    {
        /// <summary>A nisse carries a batch, not a warehouse; ten stacks is the ceiling.</summary>
        public const int MaxCarry = 10;

        private static readonly Dictionary<ZDOID, Trip> Active = new Dictionary<ZDOID, Trip>();

        public static bool Has(Claim claim)
        {
            if (claim == null) return false;
            Trip trip;
            return Active.TryGetValue(claim.BowlId, out trip) && !trip.Finished;
        }

        public static Trip Get(Claim claim)
        {
            if (claim == null) return null;
            Trip trip;
            return Active.TryGetValue(claim.BowlId, out trip) ? trip : null;
        }

        /// <summary>Starts a trip if the claim is free and there is work worth walking for.</summary>
        public static bool Start(Claim claim, NisseActor nisse, int budget)
        {
            if (Has(claim) || nisse == null) return false;
            if (budget <= 0) return false;

            var trip = Trip.Start(claim, nisse, Mathf.Min(budget, MaxCarry));
            if (trip == null) return false;

            Active[claim.BowlId] = trip;
            return true;
        }

        public static void Tick(float dt)
        {
            if (Active.Count == 0) return;

            List<ZDOID> finished = null;
            foreach (var pair in Active)
            {
                var trip = pair.Value;
                if (trip == null || trip.Finished)
                {
                    if (finished == null) finished = new List<ZDOID>();
                    finished.Add(pair.Key);
                    continue;
                }

                trip.Tick(dt);
            }

            if (finished == null) return;
            foreach (var id in finished) Active.Remove(id);
        }

        public static void Forget(ZDOID bowlId)
        {
            Trip trip;
            if (Active.TryGetValue(bowlId, out trip) && trip != null) trip.Abort();
            Active.Remove(bowlId);
        }

        /// <summary>Drops every trip without touching the queue, for a world change.</summary>
        public static void Reset()
        {
            Active.Clear();
        }

        public static int Count => Active.Count;

        /// <summary>Pokes the tile the next trip will walk to, so the build starts before it is needed.</summary>
        public static void PokeNext(Claim claim)
        {
            if (claim == null || Pathfinding.instance == null) return;

            var ticket = FlowQueue.Peek(claim);
            var nisse = NisseFactory.Find(claim.Bowl);
            if (ticket == null || nisse == null) return;

            var target = Storage.StorageRouter.Decide(claim, ticket.Item, null, ticket.ExcludeContainer());
            if (!target.Found) return;

            var point = target.Container.transform.position;
            // A null path list still pokes both areas; no result is used here on purpose.
            Pathfinding.instance.GetPath(nisse.transform.position, point, null,
                Pathfinding.AgentType.Humanoid, requireFullPath: false, cleanup: false);
        }

        /// <summary>Walks a nisse back to its bowl when it has nothing else to do.</summary>
        public static void GoHome(Claim claim, NisseActor nisse)
        {
            if (claim == null || nisse == null || claim.Bowl == null) return;
            if (Has(claim) || HusnisseConfig.Enabled.Value == false) return;
            if (FetchRunner.Has(claim)) return;

            var motion = nisse.GetComponent<NisseMotion>();
            if (motion == null || motion.IsFading || motion.IsTravelling) return;

            var bowl = claim.Bowl.transform;
            var forward = bowl.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;

            var home = bowl.position + forward.normalized * 1.5f + Vector3.up * 0.1f;
            if (Vector3.Distance(nisse.transform.position, home) < 3f) return;

            motion.Travel(home, 1f);
        }
    }
}
