using System.Collections.Generic;
using Husnisse.Claims;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// Per-claim work queue. Producers enqueue tickets; the scheduler drains them at the configured
    /// rate. A claim that is unfed or suppressed keeps its queue and resumes when it is working again.
    /// </summary>
    public static class FlowQueue
    {
        private static readonly Dictionary<ZDOID, LinkedList<FlowTicket>> Queues =
            new Dictionary<ZDOID, LinkedList<FlowTicket>>();

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
            if (InFlight(claim, item)) return false;

            Enqueue(FlowTicket.FromContainer(claim, container, item));
            return true;
        }

        /// <summary>Whether this stack is already spoken for — waiting in the queue or on a trip.</summary>
        public static bool InFlight(Claim claim, ItemDrop.ItemData item)
        {
            if (claim == null || item == null) return false;

            LinkedList<FlowTicket> queue;
            if (Queues.TryGetValue(claim.BowlId, out queue))
            {
                foreach (var ticket in queue)
                {
                    if (ticket.Source == FlowSource.Container && ReferenceEquals(ticket.Item, item)) return true;
                }
            }

            var trip = TripRunner.Get(claim);
            return trip != null && trip.Carries(item);
        }

        public static void EnqueuePockets(Claim claim, Inventory inventory, ItemDrop.ItemData item)
        {
            Enqueue(FlowTicket.FromPockets(claim, inventory, item));
        }

        public static void Enqueue(FlowTicket ticket)
        {
            QueueFor(ticket)?.AddLast(ticket);
        }

        /// <summary>Puts a ticket back at the head: a trip that did not take it must not lose its place.</summary>
        public static void EnqueueFront(FlowTicket ticket)
        {
            QueueFor(ticket)?.AddFirst(ticket);
        }

        private static LinkedList<FlowTicket> QueueFor(FlowTicket ticket)
        {
            if (ticket == null || ticket.Claim == null) return null;

            LinkedList<FlowTicket> queue;
            if (!Queues.TryGetValue(ticket.Claim.BowlId, out queue))
            {
                queue = new LinkedList<FlowTicket>();
                Queues[ticket.Claim.BowlId] = queue;
            }
            return queue;
        }

        public static bool TryDequeue(Claim claim, out FlowTicket ticket)
        {
            ticket = null;
            LinkedList<FlowTicket> queue;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out queue) || queue.Count == 0) return false;

            ticket = queue.First.Value;
            queue.RemoveFirst();
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
            LinkedList<FlowTicket> queue;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out queue) || queue.Count == 0) return null;
            return queue.First.Value;
        }

        public static int Count(Claim claim)
        {
            LinkedList<FlowTicket> queue;
            return claim != null && Queues.TryGetValue(claim.BowlId, out queue) ? queue.Count : 0;
        }

        public static void Forget(ZDOID bowlId)
        {
            Queues.Remove(bowlId);
            QueuedDrops.Remove(bowlId);
        }
    }
}
