using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Localization;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// The nisse's voice. It talks to whoever is close enough to hear, never globally, and never more
    /// than once a minute per claim — with one slot for idle chatter and a separate one for a
    /// deliberate event, so a remark about an empty bowl can never swallow the line that explains
    /// something the player just watched happen.
    ///
    /// The audience is the connected peers, not <see cref="Player.GetAllPlayers"/>: a dedicated
    /// server simulates no player characters of its own, so that list is empty there and every line
    /// written through it goes nowhere. Each peer's reference position is what the server does know,
    /// and the game's own "ShowMessage" handler — the one <see cref="MessageHud.MessageAll"/>
    /// broadcasts — is delivered peer by peer.
    /// </summary>
    public static class NisseVoice
    {
        /// <summary>How a line is throttled. Ambient remarks share a slot; an event has its own.</summary>
        public enum Kind
        {
            Ambient,
            Event,
        }

        private const float QuietSeconds = 60f;

        /// <summary>How far past the claim edge a player still hears the nisse.</summary>
        private const float HearingSlack = 15f;

        /// <summary>The game's own client-side HUD entry point, registered in MessageHud.Start.</summary>
        private const string ShowMessageRpc = "ShowMessage";

        private static readonly Dictionary<ZDOID, float> LastAmbient = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<ZDOID, float> LastEvent = new Dictionary<ZDOID, float>();

        public static void Say(Claim claim, string line, Kind kind = Kind.Ambient)
        {
            if (claim == null || string.IsNullOrEmpty(line)) return;

            var spoken = kind == Kind.Event ? LastEvent : LastAmbient;
            float last;
            if (spoken.TryGetValue(claim.BowlId, out last) && Time.time - last < QuietSeconds)
            {
                Log.Debug("Creature", "voice: " + kind + " held back (" +
                                      (Time.time - last).ToString("0.0") + "s of " + QuietSeconds + ")");
                return;
            }
            spoken[claim.BowlId] = Time.time;

            var text = Loc.T(Loc.HandName) + ": " + line;
            var heard = DeliverToPeers(claim, text) + DeliverLocally(claim, text);

            Log.Debug("Creature", "voice: " + kind + " line=\"" + text + "\" heard=" + heard +
                                  " reach=" + (claim.Radius + HearingSlack).ToString("0"));
        }

        /// <summary>One line to every connected peer within earshot. Returns how many were addressed.</summary>
        private static int DeliverToPeers(Claim claim, string text)
        {
            if (ZNet.instance == null || ZRoutedRpc.instance == null) return 0;
            if (!ZNet.instance.IsServer()) return 0;

            var reach = claim.Radius + HearingSlack;
            var heard = 0;
            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer == null || !peer.IsReady()) continue;

                var distance = Horizontal(peer.GetRefPos(), claim.Center);
                if (distance > reach)
                {
                    Log.Debug("Creature", "voice: out of earshot " + peer.m_playerName +
                                          " at " + distance.ToString("0.0") + " m");
                    continue;
                }

                ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, ShowMessageRpc,
                    (int)MessageHud.MessageType.TopLeft, text);
                heard++;
                Log.Debug("Creature", "voice: sent to " + peer.m_playerName +
                                      " uid=" + peer.m_uid + " at " + distance.ToString("0.0") + " m");
            }
            return heard;
        }

        /// <summary>
        /// The host's own player: a listen server is a player too, and a single-player game has no
        /// peers at all. A dedicated server has no local player and this is a no-op there.
        /// </summary>
        private static int DeliverLocally(Claim claim, string text)
        {
            var player = Player.m_localPlayer;
            if (player == null) return 0;
            if (Horizontal(player.transform.position, claim.Center) > claim.Radius + HearingSlack) return 0;

            player.Message(MessageHud.MessageType.TopLeft, text);
            return 1;
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

        /// <summary>Drops the throttle entries for a bowl that no longer exists.</summary>
        public static void Forget(ZDOID bowlId)
        {
            LastAmbient.Remove(bowlId);
            LastEvent.Remove(bowlId);
        }

        /// <summary>
        /// Claims are ground, not volume: a player one floor up or down still hears the nisse. A
        /// peer's reference position is only reliable across the ground, which is also how a claim
        /// decides what it contains.
        /// </summary>
        private static float Horizontal(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
    }
}
