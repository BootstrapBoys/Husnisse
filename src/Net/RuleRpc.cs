using System;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Net
{
    /// <summary>What a rule request asks the server to write.</summary>
    public enum RuleOp : byte
    {
        SetNetwork = 1,
        SetRule = 2,
    }

    /// <summary>
    /// Client-to-server channel for container rules. A rule is world state: the server validates the
    /// request and writes the container's ZDO, and every peer (including the one that asked) reads the
    /// result back from the ZDO. The client never writes the ZDO itself, so two players cannot hold
    /// two different rules and a headless server has no UI path to no-op.
    /// </summary>
    public static class RuleRpc
    {
        private const string Method = "Husnisse_Rule";
        private static bool _registered;

        public static void Register()
        {
            if (_registered || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(Method, Handle);
            _registered = true;
            Log.Info("rule RPC registered");
        }

        public static void SendNetwork(ZDOID bowlId, ZDOID containerId, long playerId, bool on)
        {
            Send(RuleOp.SetNetwork, bowlId, containerId, playerId, on ? "1" : "0");
        }

        public static void SendRule(ZDOID bowlId, ZDOID containerId, long playerId, ContainerRule rule)
        {
            Send(RuleOp.SetRule, bowlId, containerId, playerId, (rule ?? ContainerRule.None()).Encode());
        }

        private static void Send(RuleOp op, ZDOID bowlId, ZDOID containerId, long playerId, string payload)
        {
            if (ZNet.instance == null || bowlId == ZDOID.None || containerId == ZDOID.None) return;

            Register();

            var package = new ZPackage();
            package.Write((byte)op);
            package.Write(bowlId);
            package.Write(containerId);
            package.Write(playerId);
            package.Write(payload ?? string.Empty);

            // A package is a stream. The local (host / single-player) path re-enters the handler, so
            // it has to read from the start; the routed path serializes the whole array either way.
            package.SetPos(0);

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
                package.SetPos(0);
                var op = (RuleOp)package.ReadByte();
                var bowlId = package.ReadZDOID();
                var containerId = package.ReadZDOID();
                var claimed = package.ReadLong();
                var payload = package.ReadString();

                var claim = ClaimRegistry.Find(bowlId);
                if (claim == null)
                {
                    Log.Debug("Rules", "refused: no claim " + bowlId);
                    return;
                }

                var container = ContainerIndex.ById(claim, containerId);
                if (container == null)
                {
                    Log.Debug("Rules", "refused: container " + containerId + " is not in claim " + bowlId);
                    return;
                }

                var who = sender == 0L ? claimed : PeerIdentity.PlayerId(sender);
                if (sender != 0L && who == 0L)
                {
                    Log.Debug("Rules", "refused: sender " + sender + " has no character id yet");
                    return;
                }

                if (!claim.Allows(who))
                {
                    Log.Debug("Rules", "refused: player " + who + " does not hold claim " + bowlId);
                    return;
                }

                var name = Utils.GetPrefabName(container.gameObject);
                if (op == RuleOp.SetNetwork)
                {
                    var on = payload == "1";
                    if (!ContainerNetwork.WriteNetwork(container, on, "player " + who)) return;
                    Log.Debug("Rules", "network " + (on ? "on" : "off") + " " + name +
                                       " claim=" + bowlId + " player=" + who);
                    return;
                }

                if (op == RuleOp.SetRule)
                {
                    var rule = ContainerRule.Decode(payload);
                    if (rule.Kind == RuleKind.None && payload != ContainerRule.None().Encode())
                    {
                        Log.Debug("Rules", "refused: rule payload not understood: " + payload);
                        return;
                    }

                    if (!ContainerNetwork.WriteRule(container, rule, "player " + who)) return;
                    Log.Debug("Rules", "rule " + name + " " + rule.Describe() +
                                       " claim=" + bowlId + " player=" + who);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("rule RPC failed: " + ex.Message);
            }
        }
    }
}
