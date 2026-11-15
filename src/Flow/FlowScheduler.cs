using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Rounds;
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

                EnqueueDrops(claim);
                TripRunner.PokeNext(claim);

                if (FlowQueue.Count(claim) == 0) TripRunner.GoHome(claim, nisse);
            }
        }

        private static void EnqueueDrops(Claim claim)
        {
            foreach (var drop in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (drop == null) continue;
                if (!claim.Contains(drop.transform.position)) continue;

                var item = drop.m_itemData;
                if (item == null || item.m_shared == null) continue;

                FlowQueue.EnqueueDrop(claim, drop);
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
