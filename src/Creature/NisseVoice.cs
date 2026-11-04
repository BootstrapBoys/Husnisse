using System.Collections.Generic;
using Husnisse.Claims;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// The nisse's voice. It talks to whoever is close enough to hear, never globally, and never more
    /// than once a minute per claim unless it is a deliberate event.
    /// </summary>
    public static class NisseVoice
    {
        private static readonly Dictionary<ZDOID, float> LastSpoke = new Dictionary<ZDOID, float>();
        private const float QuietSeconds = 60f;

        public static void Say(Claim claim, string line, bool force = false)
        {
            if (claim == null) return;

            float last;
            LastSpoke.TryGetValue(claim.BowlId, out last);
            if (!force && Time.time - last < QuietSeconds) return;
            LastSpoke[claim.BowlId] = Time.time;

            foreach (var player in Player.GetAllPlayers())
            {
                if (player == null) continue;
                if (Vector3.Distance(player.transform.position, claim.Center) > claim.Radius + 15f) continue;
                player.Message(MessageHud.MessageType.TopLeft, "Nisse: " + line);
            }
        }

        private static readonly string[] HungryLines =
        {
            "The bowl is empty.",
            "It has been a while since anyone left porridge.",
            "The nisse is watching the bowl, not the chests.",
        };

        public static void Hungry(Claim claim)
        {
            Say(claim, HungryLines[(int)(Time.time / 90f) % HungryLines.Length]);
        }
    }
}
