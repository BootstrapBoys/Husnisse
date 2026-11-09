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
        private static readonly Dictionary<ZDOID, Queue<FlowTicket>> Queues =
            new Dictionary<ZDOID, Queue<FlowTicket>>();

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
            if (ticket == null || ticket.Claim == null) return;

            Queue<FlowTicket> queue;
            if (!Queues.TryGetValue(ticket.Claim.BowlId, out queue))
            {
                queue = new Queue<FlowTicket>();
                Queues[ticket.Claim.BowlId] = queue;
            }

            queue.Enqueue(ticket);
        }

        public static bool TryDequeue(Claim claim, out FlowTicket ticket)
        {
            ticket = null;
            Queue<FlowTicket> queue;
            if (claim == null || !Queues.TryGetValue(claim.BowlId, out queue) || queue.Count == 0) return false;

            ticket = queue.Dequeue();
            if (ticket != null && ticket.Source == FlowSource.Ground && ticket.Drop != null)
            {
                HashSet<int> seen;
                if (QueuedDrops.TryGetValue(claim.BowlId, out seen)) seen.Remove(ticket.Drop.GetInstanceID());
            }

            return true;
        }

        public static int Count(Claim claim)
        {
            Queue<FlowTicket> queue;
            return claim != null && Queues.TryGetValue(claim.BowlId, out queue) ? queue.Count : 0;
        }

        public static void Forget(ZDOID bowlId)
        {
            Queues.Remove(bowlId);
            QueuedDrops.Remove(bowlId);
        }
    }
}
