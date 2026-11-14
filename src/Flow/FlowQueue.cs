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

        public static void EnqueueContainer(Claim claim, Container container, ItemDrop.ItemData item)
        {
            Enqueue(FlowTicket.FromContainer(claim, container, item));
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
