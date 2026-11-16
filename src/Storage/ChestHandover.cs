using System.Collections.Generic;
using System.Text;
using Husnisse.Core;
using Husnisse.Net;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>What the flow may do with a container right now.</summary>
    public enum ChestAccess
    {
        /// <summary>This machine owns the ZDO: read it, write it, persist it.</summary>
        Owned,

        /// <summary>Somebody else owns it and has been asked; try again on a later tick.</summary>
        Deferred,

        /// <summary>It cannot be used at all: no ZDO, gone, or a write from this peer is impossible.</summary>
        Refused,
    }

    /// <summary>
    /// The server half of the shared-chest handshake, and the one gate every server-side inventory
    /// move goes through.
    ///
    /// A container's contents are written by its ZDO's owner. On a dedicated server that is a player
    /// for every chest that player opened last, and ownership is a revision race the receiver
    /// arbitrates: <c>ZDO.SetOwner</c> bumps <c>OwnerRevision</c> and a peer keeps whichever copy has
    /// the higher revision. A blind claim by the server can lose that race — the server believes it
    /// owns the chest, writes into its own copy, and the owning client's next save overwrites the lot.
    /// Items vanish, on the server's side, silently.
    ///
    /// So: ask the owner. It saves, hands the ZDO over, and the server's next ownership check is true.
    /// A chest somebody has open on their machine is never taken; the errand waits, and says so.
    /// Only when the recorded owner is not a connected player at all (nobody to ask, a stale record, a
    /// disconnected peer) does the server claim, and even then it re-checks <c>IsOwner</c> before
    /// reporting success.
    /// </summary>
    public static class ChestHandover
    {
        private sealed class Wait
        {
            public long Request;
            public int Asks;
            public float NextAsk;
            public float BusyUntil;
            public float LastNote;
            public string Why = string.Empty;
        }

        /// <summary>Enough asks to ride out a lost package, few enough not to chatter.</summary>
        private const int MaxAsks = 3;

        private const float AskEvery = 1.2f;

        /// <summary>How long a chest that answered "busy" is left alone before asking again.</summary>
        private const float BusyEvery = 3f;

        /// <summary>One line every thirty seconds while a player keeps a chest open.</summary>
        private const float NoteEvery = 30f;

        private static readonly Dictionary<ZDOID, Wait> Waits = new Dictionary<ZDOID, Wait>();

        private static long _request;
        private static int _rejected;
        private static float _lastRejectedNote;

        /// <summary>Chests currently being asked for.</summary>
        public static int Waiting => Waits.Count;

        /// <summary>Claims that came back not-owner, i.e. a race the server lost and retried.</summary>
        public static int Rejected => _rejected;

        /// <summary>
        /// True when the flow should route around this chest for now: a player has it open (the
        /// owner writes vanilla's <c>inUse</c> flag) or it has just refused a handover.
        /// </summary>
        public static bool Busy(Container container)
        {
            var zdo = ZdoOf(container);
            if (zdo == null) return false;

            if (zdo.GetInt(ZDOVars.s_inUse, 0) == 1 && IsPlayerPeer(zdo.GetOwner())) return true;

            // An outstanding ask counts as busy: the errand must not walk to a chest whose owner has
            // been asked and has not answered yet, or the nisse paces to it and back every tick.
            Wait wait;
            if (Waits.TryGetValue(zdo.m_uid, out wait)) return true;

            return false;
        }

        /// <summary>
        /// Takes the container if it can, asks for it if a player holds it, and reports which. The
        /// caller must not move an item on anything but <see cref="ChestAccess.Owned"/>.
        /// </summary>
        public static ChestAccess Acquire(Container container, string why)
        {
            var view = ViewOf(container);
            if (view == null || !view.IsValid()) return ChestAccess.Refused;

            if (view.IsOwner())
            {
                Wait wait;
                if (Waits.TryGetValue(view.GetZDO().m_uid, out wait)) Waits.Remove(view.GetZDO().m_uid);
                return ChestAccess.Owned;
            }

            if (ZNet.instance == null || !ZNet.instance.IsServer())
            {
                Log.Warn("chest: " + Name(container) + " needs " + why + " but this peer does not own its ZDO");
                return ChestAccess.Refused;
            }

            var zdo = view.GetZDO();
            if (zdo == null) return ChestAccess.Refused;

            var owner = zdo.GetOwner();
            if (IsPlayerPeer(owner)) return Ask(container, zdo, owner, why);

            // Nobody who could answer owns it: a fresh chest, a stale owner id, or a peer that left.
            // A claim is the only way in, and it is verified before anything is moved.
            view.ClaimOwnership();
            if (view.IsOwner())
            {
                Log.Debug("Chest", "claimed " + Name(container) + " for " + why + " (owner was " + owner +
                                   " orev=" + zdo.OwnerRevision + ")");
                return ChestAccess.Owned;
            }

            NoteRejected(container, owner, why);
            return ChestAccess.Deferred;
        }

        /// <summary>
        /// The owner answered. <paramref name="position"/> is where the chest was when it was asked;
        /// the server resolves it in its own world, so an answer cannot name a chest that is not here.
        /// </summary>
        public static void Answer(Vector3 position, ChestReply reply, long request, ushort revision)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

            var container = ContainerLookup.At(position);
            var view = ViewOf(container);
            var zdo = view != null ? view.GetZDO() : null;
            if (zdo == null) return;

            Wait wait;
            if (!Waits.TryGetValue(zdo.m_uid, out wait)) return;
            if (wait.Request != request) return;   // an answer to an older ask

            switch (reply)
            {
                case ChestReply.Released:
                    // The owner set the server as owner; the server's next acquire finds itself the
                    // owner and writes. Nothing is forced here: if the package is still in flight the
                    // next tick simply defers again.
                    wait.Asks = 0;
                    wait.NextAsk = 0f;
                    wait.BusyUntil = 0f;
                    Log.Debug("Chest", "released " + Name(container) + " for " + wait.Why +
                                       " orev=" + revision + " serverOwns=" + view.IsOwner());
                    break;

                case ChestReply.Busy:
                    wait.Asks = 0;
                    wait.BusyUntil = Time.time + BusyEvery;
                    wait.NextAsk = wait.BusyUntil;
                    Log.Debug("Chest", "busy " + Name(container) + " (a player has it open) why=" + wait.Why);
                    break;

                case ChestReply.NotMine:
                    // The record the server held was stale: drop the wait so the next acquire re-reads
                    // the owner and asks whoever really has it.
                    Waits.Remove(zdo.m_uid);
                    Log.Debug("Chest", "not mine " + Name(container) + " owner=" + zdo.GetOwner() +
                                       " why=" + wait.Why);
                    break;

                default:
                    wait.Asks = MaxAsks;
                    wait.NextAsk = Time.time + NoteEvery;
                    Log.Debug("Chest", "unavailable " + Name(container) + " why=" + wait.Why);
                    break;
            }
        }

        private static ChestAccess Ask(Container container, ZDO zdo, long owner, string why)
        {
            var now = Time.time;

            Wait wait;
            if (!Waits.TryGetValue(zdo.m_uid, out wait))
            {
                wait = new Wait();
                Waits[zdo.m_uid] = wait;
            }

            if (now < wait.NextAsk) return ChestAccess.Deferred;

            if (wait.Asks >= MaxAsks)
            {
                // A connected player owns it and is not answering. Stealing it is exactly the write
                // that loses items, so the errand waits instead — and says why, rarely.
                if (now - wait.LastNote > NoteEvery)
                {
                    wait.LastNote = now;
                    Log.Warn("chest: " + Name(container) + " is owned by player " + owner + " and has not " +
                             "answered " + wait.Asks + " handover asks; leaving it alone (why=" + why + ")");
                }
                wait.NextAsk = now + NoteEvery;
                return ChestAccess.Deferred;
            }

            wait.Request = ++_request;
            wait.Asks++;
            wait.NextAsk = now + AskEvery;
            wait.Why = why;

            ChestRpc.Ask(owner, container.transform.position, wait.Request);

            Log.Debug("Chest", "ask " + Name(container) + " owner=" + owner + " n=" + wait.Asks +
                               " why=" + why + " orev=" + zdo.OwnerRevision);
            return ChestAccess.Deferred;
        }

        /// <summary>Counts a claim the peers (or the ZDO) did not accept; logged rarely.</summary>
        private static void NoteRejected(Container container, long owner, string why)
        {
            _rejected++;

            if (Time.time - _lastRejectedNote < NoteEvery) return;
            _lastRejectedNote = Time.time;
            Log.Warn("chest: the claim on " + Name(container) + " was not accepted (owner=" + owner +
                     " why=" + why + "); nothing will be moved until this machine owns it");
        }

        /// <summary>
        /// True when this uid belongs to a connected peer that is a player. A peer record exists for
        /// the server too; it has no character ZDO, so it is not a player and never needs asking.
        /// </summary>
        public static bool IsPlayerPeer(long uid)
        {
            if (uid == 0L || ZNet.instance == null) return false;

            var peer = ZNet.instance.GetPeer(uid);
            if (peer == null) return false;
            if (peer.m_characterID == ZDOID.None) return false;

            return true;
        }

        public static void Forget(ZDOID id)
        {
            Waits.Remove(id);
        }

        /// <summary>
        /// The outstanding ask for a container, or 0 when nobody is being asked. The harness uses it
        /// to answer as the owning peer would; there is no other reader.
        /// </summary>
        public static long RequestFor(Container container)
        {
            var zdo = ZdoOf(container);
            if (zdo == null) return 0L;

            Wait wait;
            return Waits.TryGetValue(zdo.m_uid, out wait) ? wait.Request : 0L;
        }

        public static void Reset()
        {
            Waits.Clear();
        }

        /// <summary>One line for the diagnostics dump, so a stuck errand is visible without debug logs.</summary>
        public static string Describe()
        {
            if (Waits.Count == 0) return "chest_waits=0 rejected=" + _rejected;

            var text = new StringBuilder();
            text.Append("chest_waits=").Append(Waits.Count).Append(" rejected=").Append(_rejected);
            foreach (var pair in Waits)
            {
                text.Append(" [").Append(pair.Key).Append(" asks=").Append(pair.Value.Asks)
                    .Append(" why=").Append(pair.Value.Why).Append(']');
            }
            return text.ToString();
        }

        private static ZNetView ViewOf(Container container)
        {
            return container != null ? container.GetComponent<ZNetView>() : null;
        }

        private static ZDO ZdoOf(Container container)
        {
            var view = ViewOf(container);
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }

        private static string Name(Container container)
        {
            if (container == null) return "container";
            var prefab = Utils.GetPrefabName(container.gameObject);
            var at = container.transform.position;
            return prefab + " at=(" + at.x.ToString("0.#") + "," + at.z.ToString("0.#") + ")";
        }
    }
}
