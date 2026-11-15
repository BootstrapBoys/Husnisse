using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// Who could see a point, and how far away the nearest of them is. The answer drives the animation
    /// LOD, so it has to work on every kind of host:
    ///
    /// - in single-player and on a client, the players are real objects and their distance is exact;
    /// - a dedicated server simulates no player characters of its own (proven on the rig: zero
    ///   `Player.GetAllPlayers()` while a client is connected), so each connected peer's reference
    ///   position is used instead — the same position the game itself measures ZDO sync distance from;
    /// - with nobody connected the distance is infinite: there is no one to animate for, and the work
    ///   stays in the queue.
    /// </summary>
    public static class Viewers
    {
        /// <summary>
        /// Distance to the nearest viewer, or infinity when nobody is looking. <paramref name="count"/>
        /// is how many viewers were considered, so a probe can tell "far" from "nobody here".
        /// </summary>
        public static float Nearest(Vector3 point, out int count)
        {
            var best = float.PositiveInfinity;
            count = 0;

            var players = Player.GetAllPlayers();
            if (players != null)
            {
                foreach (var player in players)
                {
                    if (player == null) continue;
                    count++;
                    var distance = Vector3.Distance(point, player.transform.position);
                    if (distance < best) best = distance;
                }
            }

            if (ZNet.instance == null) return best;

            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer == null) continue;

                // A peer that has not reported a position yet reads as the world origin; counting it
                // would put a phantom viewer at (0,0,0) and animate whatever stands there.
                var position = peer.m_refPos;
                if (position == Vector3.zero) continue;

                count++;
                var distance = Vector3.Distance(point, position);
                if (distance < best) best = distance;
            }

            return best;
        }

        /// <summary>The nearest viewer with no count, for callers that only need the distance.</summary>
        public static float Nearest(Vector3 point)
        {
            int ignored;
            return Nearest(point, out ignored);
        }
    }
}
