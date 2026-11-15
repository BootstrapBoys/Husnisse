using Husnisse.Core;

namespace Husnisse.Net
{
    /// <summary>
    /// Who sent a routed RPC. <c>ZNetPeer.m_playerID</c> is never populated on this build — the
    /// server's peer record reads <c>playerID=0</c> for a connected client while the client's own id
    /// is non-zero — so the id is read from the peer's character ZDO, which carries the game's own
    /// <c>playerID</c> field. That works on a dedicated server even though it instantiates no
    /// <see cref="Player"/> objects of its own: the ZDO arrives over the network whether or not the
    /// zone (and therefore the character GameObject) is loaded.
    /// </summary>
    public static class PeerIdentity
    {
        public static long PlayerId(long peerUid)
        {
            if (ZNet.instance == null || ZDOMan.instance == null) return 0L;

            var peer = ZNet.instance.GetPeer(peerUid);
            if (peer == null) return 0L;

            if (peer.m_playerID != 0L) return peer.m_playerID;

            if (peer.m_characterID == ZDOID.None) return 0L;
            var zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
            if (zdo == null)
            {
                Log.Debug("Net", "peer " + peerUid + " has character " + peer.m_characterID +
                                 " but the ZDO is not here yet");
                return 0L;
            }

            var id = zdo.GetLong(ZDOVars.s_playerID, 0L);
            Log.Debug("Net", "peer " + peerUid + " uid=" + peer.m_uid + " name=" + peer.m_playerName +
                             " character=" + peer.m_characterID + " player=" + id);
            return id;
        }
    }
}
