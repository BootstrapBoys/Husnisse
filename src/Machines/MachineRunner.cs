using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Flow;
using UnityEngine;

namespace Husnisse.Machines
{
    /// <summary>
    /// The one machine errand a claim may have in flight, and the turn-taking that keeps it from
    /// starving the chests.
    ///
    /// A claim has one pair of hands. Machine work is one more kind of visible work, so it runs
    /// beside deposit trips rather than inside them: the scheduler starts whichever is due, and the
    /// two alternate — after a chest trip the machines get the next turn, after a machine errand the
    /// chests do. A base that is filing a floor of loot therefore still gets its furnace lit, and a
    /// furnace that is always hungry cannot hide a deposit chest's work for ever.
    /// </summary>
    public static class MachineRunner
    {
        private static readonly Dictionary<ZDOID, MachineErrand> Active = new Dictionary<ZDOID, MachineErrand>();

        /// <summary>Whose turn it is, per claim: true means the machines go next.</summary>
        private static readonly Dictionary<ZDOID, bool> MachineTurn = new Dictionary<ZDOID, bool>();

        /// <summary>When each claim's machine work may next be looked at.</summary>
        private static readonly Dictionary<ZDOID, float> NextAttempt = new Dictionary<ZDOID, float>();

        /// <summary>
        /// How often a claim's machines are re-planned. The pump is handed the scheduler's frame, so
        /// without this every frame would scan every machine and answer a routing question per
        /// demand; three seconds is far below the time one errand takes to walk to a chest and back,
        /// so nothing waits on it.
        /// </summary>
        public const float PumpSeconds = 3f;

        public static bool Has(Claim claim)
        {
            if (claim == null) return false;
            MachineErrand errand;
            return Active.TryGetValue(claim.BowlId, out errand) && errand != null && !errand.Finished;
        }

        public static MachineErrand Get(Claim claim)
        {
            if (claim == null) return null;
            MachineErrand errand;
            return Active.TryGetValue(claim.BowlId, out errand) ? errand : null;
        }

        public static int Count
        {
            get { return Active.Count; }
        }

        /// <summary>
        /// Starts an errand if there is work and it is the machines' turn. Called by the scheduler
        /// before it considers a deposit trip, so machine work competes for the same pair of hands.
        /// </summary>
        public static bool TryStart(Claim claim, NisseActor nisse)
        {
            if (claim == null || nisse == null) return false;
            if (Has(claim)) return false;
            if (HusnisseConfig.TendMachines == null || !HusnisseConfig.TendMachines.Value) return false;

            var now = Time.time;
            float next;
            if (NextAttempt.TryGetValue(claim.BowlId, out next) && now < next) return false;
            NextAttempt[claim.BowlId] = now + PumpSeconds;

            var job = MachineScan.Next(claim, nisse.transform.position);
            if (job == null) return false;

            // Chests first when the base is behind on filing, unless the machines are owed the turn.
            if (FlowQueue.Count(claim) > 0 && !TurnFor(claim)) return false;

            return Start(claim, nisse, job, false);
        }

        /// <summary>Starts one named job. Used by the scheduler's plan and by the console's force.</summary>
        public static bool Start(Claim claim, NisseActor nisse, MachineJob job, bool forced)
        {
            if (claim == null || nisse == null || job == null) return false;
            if (Has(claim)) return false;

            var errand = new MachineErrand(claim, nisse, job);
            if (errand.Finished)
            {
                Log.Warn("machines: errand refused at start: " + job.Describe());
                return false;
            }

            Active[claim.BowlId] = errand;
            MachineTurn[claim.BowlId] = false;

            Log.Debug("Machines", "errand start claim=" + claim.BowlId + " " + job.Describe() +
                                  (forced ? " forced" : string.Empty) +
                                  " from=" + (job.Source != null ? job.Source.name : "-") +
                                  " leg_m=" + Vector3.Distance(nisse.transform.position, job.Machine.Position).ToString("0.0"));
            DiagnosticsFile.Line("[Machines] errand start claim=" + claim.BowlId + " " + job.Describe() +
                                 " why=" + job.Why + (forced ? " forced=1" : string.Empty));
            return true;
        }

        /// <summary>A chest trip just started: the machines are owed the next turn.</summary>
        public static void NoteChestWork(Claim claim)
        {
            if (claim == null) return;
            MachineTurn[claim.BowlId] = true;
        }

        private static bool TurnFor(Claim claim)
        {
            bool turn;
            if (MachineTurn.TryGetValue(claim.BowlId, out turn)) return turn;
            return true;
        }

        public static void Tick(float dt)
        {
            if (Active.Count == 0) return;

            List<ZDOID> finished = null;
            List<MachineErrand> done = null;
            foreach (var pair in Active)
            {
                var errand = pair.Value;
                if (errand == null || errand.Finished)
                {
                    if (finished == null)
                    {
                        finished = new List<ZDOID>();
                        done = new List<MachineErrand>();
                    }
                    finished.Add(pair.Key);
                    done.Add(errand);
                    continue;
                }

                errand.Tick(dt);
            }

            if (finished == null) return;
            for (var i = 0; i < finished.Count; i++)
            {
                var errand = done[i];
                if (errand != null)
                {
                    DiagnosticsFile.Line("[Machines] errand done landed=" + errand.Landed +
                                         " aborted=" + errand.Aborted +
                                         " kind=" + (errand.Job != null ? errand.Job.Kind.ToString() : "-"));
                    Log.Debug("Machines", "errand done landed=" + errand.Landed + " aborted=" + errand.Aborted);
                }
                Active.Remove(finished[i]);
            }
        }

        public static void Forget(ZDOID bowlId)
        {
            MachineErrand errand;
            if (Active.TryGetValue(bowlId, out errand) && errand != null) errand.Abort();
            Active.Remove(bowlId);
            MachineTurn.Remove(bowlId);
            NextAttempt.Remove(bowlId);
        }

        /// <summary>A world change: drop every errand without touching the world.</summary>
        public static void Reset()
        {
            Active.Clear();
            MachineTurn.Clear();
            NextAttempt.Clear();
            FeedGuard.ForgetAll();
        }

        /// <summary>`husnisse_status` / `husnisse_machines` one-liner.</summary>
        public static string Describe()
        {
            if (Active.Count == 0) return "machine errands=0";

            var text = "machine errands=" + Active.Count;
            foreach (var pair in Active)
            {
                var errand = pair.Value;
                if (errand == null) continue;
                text += " " + pair.Key + ":" + errand.PhaseName + " carried=" + errand.Carried +
                        (errand.Job != null ? " " + errand.Job.Machine.Prefab : string.Empty);
            }
            return text;
        }
    }
}
