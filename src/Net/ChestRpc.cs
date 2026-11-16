using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Net
{
    /// <summary>What a chest request is.</summary>
    public enum ChestOp : byte
    {
        /// <summary>Server to the peer that owns a container: may the nisse have it for a moment?</summary>
        Ask = 1,

        /// <summary>That peer back to the server: the answer, with the owner revision it left behind.</summary>
        Answer = 2,
    }

    /// <summary>What the owning peer says about a chest request.</summary>
    public enum ChestReply : byte
    {
        /// <summary>The chest is mine, it is closed, it is saved, and I have handed it to the server.</summary>
        Released = 1,

        /// <summary>Someone has this chest open on my machine; do not take it.</summary>
        Busy = 2,

        /// <summary>I do not own it any more, so there is nothing for me to hand over.</summary>
        NotMine = 3,

        /// <summary>I cannot see that chest, or I have no server to hand it to.</summary>
        Unavailable = 4,
    }

    /// <summary>
    /// The shared-chest handshake, peer half. Ownership of a container's ZDO is a revision race the
    /// server arbitrates: <c>ZDO.SetOwner</c> bumps <c>OwnerRevision</c>, and a receiver only accepts
    /// an owner change whose revision is higher than the copy it holds. A dedicated server that
    /// simply claims a chest a client owns therefore loses the race against a client that opened it
    /// last, and every item it wrote is overwritten by that client's next save.
    ///
    /// The only claim that always wins is the owner's own. So the server asks, and the owner answers:
    /// it saves what it has, hands the ZDO to the server by setting the server's session id as the
    /// owner, and says so. The server's next <c>IsOwner()</c> check is then true and its write carries
    /// the highest revision.
    ///
    /// The request is addressed by <b>position</b>, like the rule channel: a ZDOID's user component is
    /// a per-peer table entry, and a client can hold a container the server has already dropped.
    /// </summary>
    public static class ChestRpc
    {
        private const string Method = "Husnisse_Chest";

        /// <summary>Containers are point objects; 1 m is generous and still one container.</summary>
        private const float MatchRadius = 1.0f;

        private static bool _registered;

        public static void Register()
        {
            if (_registered || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(Method, Handle);
            _registered = true;
            Log.Info("chest RPC registered");
        }

        /// <summary>Server to the owning peer: may the nisse have this container?</summary>
        public static void Ask(long peerUid, Vector3 position, long request)
        {
            if (peerUid == 0L || ZRoutedRpc.instance == null) return;

            var package = new ZPackage();
            package.Write((byte)ChestOp.Ask);
            package.Write(position);
            package.Write(request);
            package.Flush();
            package.SetPos(0);

            ZRoutedRpc.instance.InvokeRoutedRPC(peerUid, Method, package);
        }

        private static void Reply(long peerUid, Vector3 position, long request, ChestReply reply, ushort revision)
        {
            if (peerUid == 0L || ZRoutedRpc.instance == null) return;

            var package = new ZPackage();
            package.Write((byte)ChestOp.Answer);
            package.Write(position);
            package.Write(request);
            package.Write((byte)reply);
            package.Write(revision);
            package.Flush();
            package.SetPos(0);

            ZRoutedRpc.instance.InvokeRoutedRPC(peerUid, Method, package);
        }

        private static void Handle(long sender, ZPackage package)
        {
            if (package == null || ZNet.instance == null) return;

            try
            {
                package.SetPos(0);
                var op = (ChestOp)package.ReadByte();
                var position = package.ReadVector3();
                var request = package.ReadLong();

                if (op == ChestOp.Ask)
                {
                    // Only a client answers; the server never asks itself for its own chest.
                    if (ZNet.instance.IsServer()) return;
                    HandleAsk(sender, position, request);
                    return;
                }

                if (op != ChestOp.Answer) return;
                if (!ZNet.instance.IsServer()) return;

                var reply = (ChestReply)package.ReadByte();
                var revision = package.ReadUShort();
                Storage.ChestHandover.Answer(position, reply, request, revision);
            }
            catch (System.Exception ex)
            {
                Log.Warn("chest RPC failed: " + ex.Message);
            }
        }

        /// <summary>
        /// The owner's half. A chest somebody has open is never handed over: the player would keep a
        /// stale copy of the grid and its next move would resurrect what the nisse took. Otherwise the
        /// owner saves its own copy first (that save is what makes the handover lossless) and then
        /// gives the ZDO to the server.
        /// </summary>
        private static void HandleAsk(long sender, Vector3 position, long request)
        {
            var container = ContainerAt(position);
            if (container == null)
            {
                Reply(sender, position, request, ChestReply.Unavailable, 0);
                return;
            }

            var view = container.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            if (view == null || !view.IsValid() || zdo == null)
            {
                Reply(sender, position, request, ChestReply.Unavailable, 0);
                return;
            }

            if (!view.IsOwner())
            {
                Reply(sender, position, request, ChestReply.NotMine, zdo.OwnerRevision);
                return;
            }

            if (container.IsInUse())
            {
                Reply(sender, position, request, ChestReply.Busy, zdo.OwnerRevision);
                Log.Debug("Chest", "lend refused: " + Name(container) + " is open here (rev=" + zdo.OwnerRevision + ")");
                return;
            }

            // Flush before handing over: the packed inventory in the ZDO is the copy the server will
            // read, and this save is the whole point of asking instead of claiming.
            Storage.ContainerWrite.Persist(container, "lend");

            var server = ZNet.instance.GetServerPeer();
            if (server == null)
            {
                Reply(sender, position, request, ChestReply.Unavailable, zdo.OwnerRevision);
                return;
            }

            zdo.SetOwner(server.m_uid);
            if (ZDOMan.instance != null) ZDOMan.instance.ForceSendZDO(server.m_uid, zdo.m_uid);

            Log.Debug("Chest", "lent " + Name(container) + " to the server uid=" + server.m_uid +
                              " orev=" + zdo.OwnerRevision);
            Reply(sender, position, request, ChestReply.Released, zdo.OwnerRevision);
        }

        private static Container ContainerAt(Vector3 point)
        {
            return Storage.ContainerLookup.At(point, MatchRadius);
        }

        private static string Name(Container container)
        {
            var prefab = Utils.GetPrefabName(container.gameObject);
            var at = container.transform.position;
            return prefab + " at=(" + at.x.ToString("0.#") + "," + at.z.ToString("0.#") + ")";
        }
    }
}
