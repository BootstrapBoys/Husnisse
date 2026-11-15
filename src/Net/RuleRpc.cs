using System;
using Husnisse.Claims;
using Husnisse.Compat;
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
    ///
    /// The request is addressed by the container's <b>position</b>, not by its ZDO id: a ZDOID's user
    /// component is a per-peer key table entry, and a client can hold a bowl the server has already
    /// dropped (a stale object from an earlier world, a second bowl inside the claim). The server
    /// resolves the container and the claim from its own world, so a client cannot name a container
    /// that is not really there or a claim that does not hold it.
    /// </summary>
    public static class RuleRpc
    {
        private const string Method = "Husnisse_Rule";

        /// <summary>
        /// The server's answer to the peer that asked. It carries the value it accepted, and the peer
        /// applies it to its own ZDO copy when it owns that copy (which is the usual case: opening a
        /// container makes the client the ZDO owner, and a ZDO's owner is the only writer whose data
        /// the other peers accept). Without this the server's write lands in the server's copy and the
        /// client that has the chest open never sees it.
        /// </summary>
        private const string AckMethod = "Husnisse_RuleAck";

        private static bool _registered;

        /// <summary>Containers are point objects; 1 m is generous and still one container.</summary>
        private const float MatchRadius = 1.0f;

        public static void Register()
        {
            if (_registered || ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.Register<ZPackage>(Method, Handle);
            ZRoutedRpc.instance.Register<ZPackage>(AckMethod, HandleAck);
            _registered = true;
            Log.Info("rule RPC registered");
        }

        public static void SendNetwork(Vector3 containerPosition, long playerId, bool on)
        {
            Send(RuleOp.SetNetwork, containerPosition, playerId, on ? "1" : "0");
        }

        public static void SendRule(Vector3 containerPosition, long playerId, ContainerRule rule)
        {
            Send(RuleOp.SetRule, containerPosition, playerId, (rule ?? ContainerRule.None()).Encode());
        }

        private static void Send(RuleOp op, Vector3 containerPosition, long playerId, string payload)
        {
            if (ZNet.instance == null) return;

            Register();

            var package = new ZPackage();
            package.Write((byte)op);
            package.Write(containerPosition);
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
                var position = package.ReadVector3();
                var claimed = package.ReadLong();
                var payload = package.ReadString();

                var container = ContainerAt(position);
                if (container == null)
                {
                    Log.Debug("Rules", "refused: no container at " + position);
                    return;
                }

                var claim = ClaimRegistry.FindFor(container.transform.position);
                if (claim == null)
                {
                    Log.Debug("Rules", "refused: container " + container.name + " at " + position +
                                       " is outside every claim");
                    return;
                }

                if (!ContainerFilter.CanUse(claim, container))
                {
                    Log.Debug("Rules", "refused: container " + container.name + " is not usable in claim " + claim.BowlId);
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
                    Log.Debug("Rules", "refused: player " + who + " does not hold claim " + claim.BowlId);
                    return;
                }

                var name = Utils.GetPrefabName(container.gameObject);
                if (op == RuleOp.SetNetwork)
                {
                    var on = payload == "1";
                    if (!ContainerNetwork.WriteNetwork(container, on, "player " + who)) return;
                    Log.Debug("Rules", "network " + (on ? "on" : "off") + " " + name +
                                       " claim=" + claim.BowlId + " player=" + who);
                    Ack(sender, op, position, payload);
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
                                       " claim=" + claim.BowlId + " player=" + who);
                    Ack(sender, op, position, payload);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("rule RPC failed: " + ex.Message);
            }
        }

        /// <summary>Tells the peer that asked which value the server accepted.</summary>
        private static void Ack(long sender, RuleOp op, Vector3 position, string payload)
        {
            if (sender == 0L || ZRoutedRpc.instance == null) return;

            var package = new ZPackage();
            package.Write((byte)op);
            package.Write(position);
            package.Write(payload ?? string.Empty);
            package.SetPos(0);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, AckMethod, package);
        }

        /// <summary>
        /// The asking peer applies the accepted value to its local copy. Only the ZDO's owner may
        /// write it: that is the copy the other peers accept, and the client that has the container
        /// open is the owner. A peer that no longer owns it leaves the job to the owner.
        /// </summary>
        private static void HandleAck(long sender, ZPackage package)
        {
            if (ZNet.instance == null || ZNet.instance.IsServer()) return;
            if (package == null) return;

            try
            {
                package.SetPos(0);
                var op = (RuleOp)package.ReadByte();
                var position = package.ReadVector3();
                var payload = package.ReadString();

                var container = ContainerAt(position);
                if (container == null) return;

                var view = container.GetComponent<ZNetView>();
                if (view == null || !view.IsOwner()) return;

                if (op == RuleOp.SetNetwork)
                {
                    ContainerNetwork.WriteNetwork(container, payload == "1", "server ack");
                }
                else if (op == RuleOp.SetRule)
                {
                    ContainerNetwork.WriteRule(container, ContainerRule.Decode(payload), "server ack");
                }
                Log.Debug("Rules", "ack applied " + payload + " (local owner)");
            }
            catch (Exception ex)
            {
                Log.Warn("rule ack failed: " + ex.Message);
            }
        }

        /// <summary>One live container near a point; the bowl is never a rule target.</summary>
        private static Container ContainerAt(Vector3 point)
        {
            Container best = null;
            var bestDistance = MatchRadius;
            foreach (var container in UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
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
