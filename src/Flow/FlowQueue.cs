using System;
using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// Per-claim work queue, one lane per task type. Producers enqueue tickets; the scheduler drains
    /// them at the configured rate. A claim that is unfed or suppressed keeps its queue and resumes
    /// when it is working again.
    ///
    /// The lane served rotates after every ticket handed out, so a floor of ground drops cannot starve
    /// a deposit chest or a player's order waiting behind it: with twenty drops and one deposit stack
    /// the nisse alternates trips instead of working through the drops for a minute first. Fairness is
    /// per task type and not per container — the queue has no idea which chest a ticket will end in
    /// until a trip routes it — and a machine that refills later (0.6) joins as its own lane.
    /// </summary>
    public static class FlowQueue
    {
        private sealed class Lanes
        {
            public readonly LinkedList<FlowTicket>[] BySource =
                new LinkedList<FlowTicket>[Enum.GetValues(typeof(FlowSource)).Length];

            /// <summary>The lane the next dequeue starts looking from.</summary>
            public int Cursor;
        }

        private static readonly Dictionary<ZDOID, Lanes> Queues = new Dictionary<ZDOID, Lanes>();

        private static readonly Dictionary<ZDOID, HashSet<int>> QueuedDrops =
            new Dictionary<ZDOID, HashSet<int>>();

        public static void EnqueueDrop(Claim claim, ItemDrop drop)
        {
            if (claim == null || drop == null) return;

            HashSet<int> seen;
            if (!QueuedDrops.TryGetValue(claim.BowlId, out seen))
            {
                seen = new HashSet<int>();
                QueuedDrops[claim.BowlId] = seen;
            }

            var id = drop.GetInstanceID();
            if (!seen.Add(id)) return;

            Enqueue(FlowTicket.FromDrop(claim, drop));
        }

        /// <summary>
        /// Queues one stack out of a container. A stack already waiting in the queue or riding on
        /// the nisse's back is not queued again: a deposit chest is re-scanned on every reconcile
        /// tick, and the stack has not moved yet while the trip that took it is still walking.
        /// </summary>
        public static bool EnqueueContainer(Claim claim, Container container, ItemDrop.ItemData item)
        {
            if (claim == null || container == null || item == null) return false;
            if (InFlight(claim, container, item)) return false;

            Enqueue(FlowTicket.FromContainer(claim, container, item));
            return true;
        }

        /// <summary>
        /// Whether this stack is already spoken for — waiting in the queue or on a trip.
        /// Identity is the stack's slot, not the object: a container whose ZDO is written reloads its
        /// inventory, and every <c>ItemData</c> in it is a new object afterwards. Comparing references
        /// alone would let the next reconcile queue the same stack a second time; the slot, the count
        /// and the shared name survive the reload.
        /// </summary>
        public static bool InFlight(Claim claim, Container container, ItemDrop.ItemData item)
        {
            if (claim == null || container == null || item == null) return false;

            Lanes lanes;
            if (Queues.TryGetValue(claim.BowlId, out lanes))
            {
                foreach (var lane in lanes.BySource)
                {
                    if (lane == null) continue;
                    foreach (var ticket in lane)
                    {
                        if (ticket.Source == FlowSource.Container && SameStack(ticket, container, item)) return true;
                    }
                }
            }

            var trip = TripRunner.Get(claim);
            return trip != null && trip.Carries(item);
        }

        private static bool SameStack(FlowTicket ticket, Container container, ItemDrop.ItemData item)
        {
            if (ticket.Item == null || item == null) return false;
            if (ReferenceEquals(ticket.Item, item)) return true;
            if (!ReferenceEquals(ticket.Container, container)) return false;

            var queued = ticket.Item;
            if (queued.m_shared == null || item.m_shared == null) return false;

            return queued.m_stack == item.m_stack &&
                   queued.m_gridPos == item.m_gridPos &&
                   string.Equals(queued.m_shared.m_name, item.m_shared.m_name, StringComparison.Ordinal);
        }

        public static void EnqueuePockets(Claim claim, Inventory inventory, ItemDrop.ItemData item)
        {
            Enqueue(FlowTicket.FromPockets(claim, inventory, item));
        }

        public static void Enqueue(FlowTicket ticket)
        {
            var lane = LaneFor(ticket);
            if (lane == null) return;
            lane.AddLast(ticket);
        }

        /// <summary>Puts a ticket back at the head of its own lane: a trip that did not take it must
        /// not lose its place, and must not lose it to another task type either.</summary>
        public static void EnqueueFront(FlowTicket ticket)
        {
            var lane = LaneFor(ticket);
            if (lane == null) return;
            lane.AddFirst(ticket);
        }

        private static LinkedList<FlowTicket> LaneFor(FlowTicket ticket)
        {
            if (ticket == null || ticket.Claim == null) return null;

            Lanes lanes;
            if (!Queues.TryGetValue(ticket.Claim.BowlId, out lanes))
            {
                lanes = new Lanes();
                Queues[ticket.Claim.BowlId] = lanes;
            }

            var index = (int)ticket.Source;
            var lane = lanes.BySource[index];
            if (lane == null)
            {
                lane = new LinkedList<FlowTicket>();
                lanes.BySource[index] = lane;
            }
            return lane;
        }

        /// <summary>
        /// Takes the next ticket, round-robin across the task types present. The lane just served is
        /// not asked again until the others have had their turn.
        /// </summary>
        public static bool TryDequeue(Claim claim, out FlowTicket ticket)
        {
            ticket = null;

            Lanes lanes;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out lanes)) return false;

            for (var step = 0; step < lanes.BySource.Length; step++)
            {
                var index = (lanes.Cursor + step) % lanes.BySource.Length;
                var lane = lanes.BySource[index];
                if (lane == null || lane.Count == 0) continue;

                ticket = lane.First.Value;
                lane.RemoveFirst();
                lanes.Cursor = (index + 1) % lanes.BySource.Length;

                if (ticket != null && ticket.Source == FlowSource.Ground && ticket.Drop != null)
                {
                    HashSet<int> seen;
                    if (QueuedDrops.TryGetValue(claim.BowlId, out seen)) seen.Remove(ticket.Drop.GetInstanceID());
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Takes the next ticket from one named lane, without touching the rotation. A trip that has
        /// already claimed the fair lane uses this to fill its batch from that same task type, so
        /// round-robin costs the batch nothing: the other lane waits one trip, not one stack.
        /// </summary>
        public static bool TryDequeueFrom(Claim claim, FlowSource source, out FlowTicket ticket)
        {
            ticket = null;

            Lanes lanes;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out lanes)) return false;

            var lane = lanes.BySource[(int)source];
            if (lane == null || lane.Count == 0) return false;

            ticket = lane.First.Value;
            lane.RemoveFirst();

            if (ticket != null && ticket.Source == FlowSource.Ground && ticket.Drop != null)
            {
                HashSet<int> seen;
                if (QueuedDrops.TryGetValue(claim.BowlId, out seen)) seen.Remove(ticket.Drop.GetInstanceID());
            }

            return true;
        }

        /// <summary>The next ticket without taking it, for poking the tile it will need.</summary>
        public static FlowTicket Peek(Claim claim)
        {
            Lanes lanes;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out lanes)) return null;

            for (var step = 0; step < lanes.BySource.Length; step++)
            {
                var index = (lanes.Cursor + step) % lanes.BySource.Length;
                var lane = lanes.BySource[index];
                if (lane == null || lane.Count == 0) continue;
                return lane.First.Value;
            }

            return null;
        }

        public static int Count(Claim claim)
        {
            Lanes lanes;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out lanes)) return 0;

            var count = 0;
            foreach (var lane in lanes.BySource)
            {
                if (lane != null) count += lane.Count;
            }
            return count;
        }

        public static int Count(Claim claim, FlowSource source)
        {
            Lanes lanes;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out lanes)) return 0;

            var lane = lanes.BySource[(int)source];
            return lane != null ? lane.Count : 0;
        }

        /// <summary>
        /// One line for `husnisse_status`: how much is waiting, per task type, and which lane goes
        /// next. This is where a starvation bug is visible without a trip log. The lane counts are
        /// prefixed `lane_` because `containers=` on the same status output is the number of
        /// containers the claim can use, and two different counts under one name is a trap for
        /// anyone reading the log and for the rig's numeric assertions.
        /// </summary>
        public static string Describe(Claim claim)
        {
            Lanes lanes;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out lanes))
            {
                return "queued=0 lane_ground=0 lane_container=0 lane_pockets=0 next=none";
            }

            var total = 0;
            var text = "queued=";
            foreach (var lane in lanes.BySource)
            {
                if (lane != null) total += lane.Count;
            }
            text += total;

            text += " lane_ground=" + Count(claim, FlowSource.Ground) +
                    " lane_container=" + Count(claim, FlowSource.Container) +
                    " lane_pockets=" + Count(claim, FlowSource.Pockets) +
                    " next=" + Next(claim);
            return text;
        }

        /// <summary>The task type the next dequeue will serve, or "none".</summary>
        public static string Next(Claim claim)
        {
            var ticket = Peek(claim);
            return ticket == null ? "none" : Token(ticket.Source);
        }

        public static string Token(FlowSource source)
        {
            switch (source)
            {
                case FlowSource.Ground:
                    return "ground";
                case FlowSource.Container:
                    return "container";
                case FlowSource.Pockets:
                    return "pockets";
                default:
                    return "unknown";
            }
        }

        public static void Forget(ZDOID bowlId)
        {
            Queues.Remove(bowlId);
            QueuedDrops.Remove(bowlId);
        }
    }
}
