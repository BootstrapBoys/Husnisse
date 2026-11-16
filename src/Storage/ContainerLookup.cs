using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// One live container near a point. Both RPC channels address a container by position, because a
    /// ZDOID's user component is a per-peer table entry and a client can hold an object the server has
    /// already dropped; the server resolves the position against its own world.
    ///
    /// The bowl is never a container target: its pantry is the nisse's own shelf, not part of the
    /// network, and a rule or a handover aimed at it is a misdirected request.
    /// </summary>
    public static class ContainerLookup
    {
        public static Container At(Vector3 point, float radius = 1.0f)
        {
            Container best = null;
            var bestDistance = radius;

            foreach (var container in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (container == null) continue;
                if (container.GetComponent<Bowl.BowlPiece>() != null) continue;

                var distance = Vector3.Distance(container.transform.position, point);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = container;
            }

            return best;
        }
    }
}
