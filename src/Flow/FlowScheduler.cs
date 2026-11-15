using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Rounds;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// Server-side work pump. Refills each claim's queue on a cheap tick and starts one visible trip
    /// per batch. The token bucket bounds how much a trip may carry; walking sets how often it runs.
    /// </summary>
    public class FlowScheduler : MonoBehaviour
    {
        private static FlowScheduler _instance;

        private float _reconcile;
        private float _tokens;
        private float _elapsed;
        private bool _dumped;

        public static FlowScheduler Instance => _instance;

        public static void Attach(GameObject host)
        {
            if (_instance != null) return;
            _instance = host.AddComponent<FlowScheduler>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            if (!HusnissePlugin.Running) return;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (ZNetScene.instance == null) return;

            Net.OrderRpc.Register();
            Net.RuleRpc.Register();

            WorldPolicy.ApplyServerPolicy();

            if (!_dumped)
            {
                _dumped = true;
                _elapsed = 0f;
                TripRunner.Reset();
                DiagnosticsFile.Dump("world ready");
            }

            if (!HusnisseConfig.Enabled.Value) return;

            var dt = Time.deltaTime;
            _elapsed += dt;

            TickClaims(dt);
            Reconcile(dt);
            Drain(dt);
            TripRunner.Tick(dt);
        }

        private void Reconcile(float dt)
        {
            _reconcile += dt;
            var interval = Mathf.Max(1f, HusnisseConfig.ReconcileInterval.Value);
            if (_reconcile < interval) return;
            _reconcile = 0f;

            foreach (var claim in ClaimRegistry.All())
            {
                if (claim == null || claim.Bowl == null || claim.Suppressed) continue;

                Sulking.Step(claim);

                if (!claim.Bowl.IsFed)
                {
                    if (claim.Bowl.RequiresFeeding) NisseVoice.Hungry(claim);
                    continue;
                }

                var nisse = NisseFactory.EnsureNisse(claim.Bowl);
                if (nisse == null) continue;

                Refill(claim);
                TripRunner.PokeNext(claim);

                if (FlowQueue.Count(claim) == 0) TripRunner.GoHome(claim, nisse);
            }
        }

        /// <summary>
        /// One refill pass for a claim: ground drops inside it, then the deposit chests on its
        /// network. The claim's containers are gathered once and shared by both halves, so a pass
        /// scans the scene once however many chests the base has — this is also the unit the
        /// `husnisse_bench` benchmark times, because it is what a reconcile tick actually runs.
        /// </summary>
        public static void Refill(Claim claim)
        {
            if (claim == null) return;

            var containers = ContainerIndex.InClaim(claim);
            EnqueueDrops(claim, containers);
            EnqueueDeposits(claim, containers);
        }

        private static void EnqueueDrops(Claim claim, List<Container> containers)
        {
            // Nothing in the claim can take a drop, so do not even walk the world's drops: an empty
            // base with a floor of loot would otherwise scan every item on the map each tick.
            if (containers == null || containers.Count == 0) return;

            var drops = new List<ItemDrop>();
            foreach (var drop in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (drop == null) continue;
                if (!claim.Contains(drop.transform.position)) continue;

                var item = drop.m_itemData;
                if (item == null || item.m_shared == null) continue;
                drops.Add(drop);
            }

            if (drops.Count == 0) return;

            // One scan of the claim's network for the whole pile, and one routing question per
            // stack: a drop nothing accepts stays on the ground without being queued and retried
            // every tick, which is what a broken rule would otherwise look like in the log.
            var network = StorageRouter.NetworkCandidates(claim, containers);
            foreach (var drop in drops)
            {
                if (!StorageRouter.Decide(claim, network, drop.m_itemData, null, null).Found) continue;
                FlowQueue.EnqueueDrop(claim, drop);
            }
        }

        /// <summary>
        /// A deposit chest is a source, not a destination: a player throws anything into it and the
        /// nisse carries the contents to the chests whose rules claim them, at the queue's rate and
        /// one visible trip at a time. It is drained only while it is on the network — an
        /// off-network chest is not part of the claim's work at all — and a stack nothing accepts
        /// stays in it rather than being queued for ever.
        /// </summary>
        private static void EnqueueDeposits(Claim claim, List<Container> containers)
        {
            List<Container> network = null;

            foreach (var container in containers)
            {
                if (container == null) continue;
                if (ContainerNetwork.RuleOf(container).Kind != RuleKind.Deposit) continue;
                if (!ContainerNetwork.IsOnNetwork(container)) continue;
                if (ContainerNetwork.IsBlacklisted(container)) continue;

                var inventory = container.GetInventory();
                if (inventory == null) continue;

                for (var i = 0; i < inventory.NrOfItems(); i++)
                {
                    var item = inventory.GetItem(i);
                    if (item == null || item.m_shared == null) continue;

                    if (network == null) network = StorageRouter.NetworkCandidates(claim, containers);
                    if (!StorageRouter.Decide(claim, network, item, null, container).Found) continue;
                    FlowQueue.EnqueueContainer(claim, container, item);
                }
            }
        }

        private void Drain(float dt)
        {
            var rate = Mathf.Max(0.1f, HusnisseConfig.ItemsPerSecond.Value);
            var catchUp = HusnisseConfig.CatchUpOnLoad.Value && _elapsed < 60f && ZNet.instance.GetNrOfPlayers() > 0
                ? 2f
                : 1f;
            _tokens = Mathf.Min(_tokens + rate * catchUp * dt, Mathf.Max(1f, rate * catchUp));

            if (_tokens < 1f) return;

            foreach (var claim in ClaimRegistry.All())
            {
                if (claim == null || claim.Bowl == null || claim.Suppressed) continue;
                if (!claim.Bowl.IsFed) continue;
                if (TripRunner.Has(claim)) continue;

                var nisse = NisseFactory.EnsureNisse(claim.Bowl);
                if (nisse == null) continue;

                var budget = Mathf.Min(FloorTokens(), FlowQueue.Count(claim));
                if (budget <= 0) continue;

                DiagnosticsFile.Line("[Flow] drain tokens=" + _tokens.ToString("0.0") +
                                     " " + FlowQueue.Describe(claim) + " budget=" + budget);
                TripRunner.Start(claim, nisse, budget);
            }
        }

        /// <summary>How many stacks the nisse may load on the next trip.</summary>
        private int FloorTokens()
        {
            return Mathf.Clamp(Mathf.FloorToInt(_tokens), 0, TripRunner.MaxCarry);
        }

        /// <summary>Charged by a trip when its batch actually lands.</summary>
        public void Spend(int stacks)
        {
            if (stacks <= 0) return;
            _tokens = Mathf.Max(0f, _tokens - stacks);
        }

        private void TickClaims(float dt)
        {
            foreach (var claim in ClaimRegistry.All())
            {
                if (claim == null || claim.Bowl == null) continue;
                claim.Bowl.Tick(dt);
            }
        }
    }
}
