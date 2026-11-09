using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Memory;
using Husnisse.Rounds;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// Server-side work pump. Refills each claim's queue on a cheap tick and drains it at the
    /// configured items/second. Replaces the old round scheduler.
    /// </summary>
    public class FlowScheduler : MonoBehaviour
    {
        private static FlowScheduler _instance;

        private float _reconcile;
        private float _tokens;
        private float _elapsed;
        private bool _dumped;

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

            WorldPolicy.ApplyServerPolicy();

            if (!_dumped)
            {
                _dumped = true;
                _elapsed = 0f;
                DiagnosticsFile.Dump("world ready");
            }

            if (!HusnisseConfig.Enabled.Value) return;

            var dt = Time.deltaTime;
            _elapsed += dt;

            TickClaims(dt);
            Reconcile(dt);
            Drain(dt);
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

            var guard = 0;
            while (_tokens >= 1f && guard++ < 500)
            {
                var worked = false;

                foreach (var claim in ClaimRegistry.All())
                {
                    if (_tokens < 1f) break;
                    if (claim == null || claim.Bowl == null || claim.Suppressed) continue;
                    if (!claim.Bowl.IsFed) continue;

                    var nisse = NisseFactory.EnsureNisse(claim.Bowl);
                    if (nisse == null) continue;

                    FlowTicket ticket;
                    if (!FlowQueue.TryDequeue(claim, out ticket))
                    {
                        continue;
                    }

                    _tokens -= 1f;
                    worked = true;

                    if (!ticket.IsValid()) continue;
                    Execute(claim, nisse, ticket);
                }

                if (!worked) break;
            }
        }

        private static void Execute(Claim claim, NisseActor nisse, FlowTicket ticket)
        {
            var item = ticket.Item;
            var memory = MemoryOf(nisse);
            var target = StorageRouter.FindTarget(claim, item.m_shared.m_name, memory, ticket.ExcludeContainer());
            if (target == null) return;

            var inventory = target.GetInventory();
            if (inventory == null || !inventory.CanAddItem(item)) return;

            if (!inventory.AddItem(item.Clone())) return;

            var source = ticket.SourceInventory();
            if (ticket.Source == FlowSource.Ground)
            {
                if (ticket.Drop != null) ZNetScene.instance.Destroy(ticket.Drop.gameObject);
            }
            else if (source == null || !source.RemoveItem(item, ticket.Amount))
            {
                Log.Warn("flow: removed from source failed for " + item.m_shared.m_name);
            }

            StorageRouter.Learn(target, item.m_shared.m_name, memory);
            nisse.WarpTo(target.transform.position + Vector3.up * 0.1f);
            Log.Debug("Flow", "filed " + item.m_shared.m_name + " x" + ticket.Amount + " -> " + target.name);
        }

        private void TickClaims(float dt)
        {
            foreach (var claim in ClaimRegistry.All())
            {
                if (claim == null || claim.Bowl == null) continue;
                claim.Bowl.Tick(dt);
            }
        }

        private static ItemMemory MemoryOf(NisseActor nisse)
        {
            var view = nisse != null ? nisse.GetComponent<ZNetView>() : null;
            return view != null && view.GetZDO() != null ? new ItemMemory(view.GetZDO()) : null;
        }
    }
}
