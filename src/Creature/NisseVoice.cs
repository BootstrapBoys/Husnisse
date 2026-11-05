using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Localization;
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
                player.Message(MessageHud.MessageType.TopLeft, Loc.T(Loc.HandName) + ": " + line);
            }
        }

        private static readonly string[] HungryLines =
        {
            Loc.VoiceHungry1,
            Loc.VoiceHungry2,
            Loc.VoiceHungry3,
        };

        public static void Hungry(Claim claim)
        {
            Say(claim, Loc.T(HungryLines[(int)(Time.time / 90f) % HungryLines.Length]));
        }

        /// <summary>Drops the throttle entry for a bowl that no longer exists.</summary>
        public static void Forget(ZDOID bowlId)
        {
            LastSpoke.Remove(bowlId);
        }
    }
}
