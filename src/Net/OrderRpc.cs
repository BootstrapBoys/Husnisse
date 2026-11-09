using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Orders;
using UnityEngine;

namespace Husnisse.Net
{
    /// <summary>
    /// Client-to-server order channel. The client never moves an item: it sends a request, the
    /// server validates it and puts the work on the claim's queue.
    /// </summary>
    public static class OrderRpc
    {
        private const string Method = "Husnisse_Order";
        private static bool _registered;

        public static void Register()
        {
            if (_registered || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(Method, Handle);
            _registered = true;
            Log.Info("order RPC registered");
        }

        public static void Send(OrderKind kind, ZDOID bowlId, ZDOID containerId, long playerId)
        {
            if (ZNet.instance == null) return;

            Register();

            var package = new ZPackage();
            package.Write((byte)kind);
            package.Write(bowlId);
            package.Write(containerId);
            package.Write(playerId);

            if (ZNet.instance.IsServer())
            {
                Handle(0L, package);
                return;
            }

            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(Method, package);
        }

        private static void Handle(long sender, ZPackage package)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (package == null) return;

            try
            {
                var kind = (OrderKind)package.ReadByte();
                var bowlId = package.ReadZDOID();
                var containerId = package.ReadZDOID();
                var playerId = package.ReadLong();

                if (sender != 0L)
                {
                    var peer = ZNet.instance.GetPeer(sender);
                    if (peer == null || peer.m_playerID != playerId) return;
                }

                OrderService.ExecuteOnServer(playerId, bowlId, kind, containerId);
            }
            catch (System.Exception ex)
            {
                Log.Warn("order RPC failed: " + ex.Message);
            }
        }
    }
}
