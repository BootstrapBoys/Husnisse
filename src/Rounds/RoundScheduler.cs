using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Tasks;
using UnityEngine;

namespace Husnisse.Rounds
{
    /// <summary>
    /// Drives one bounded pass per interval, per claim. Server-only: all item movement is authoritative.
    /// A world load triggers one catch-up pass at double budget so a backlog is cleared once, not fifty times.
    /// </summary>
    public class RoundScheduler : MonoBehaviour
    {
        private static RoundScheduler _instance;

        private readonly List<IRoundTask> _tasks = new List<IRoundTask>
        {
            new CollectDropsTask(),
        };

        private float _timer;
        private float _warmup = 10f;
        private bool _catchUpDone;

        public static void Attach(GameObject host)
        {
            if (_instance != null) return;
            _instance = host.AddComponent<RoundScheduler>();
            DontDestroyOnLoad(host);
        }

        private void Update()
        {
            if (!HusnissePlugin.Running) return;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (ZNetScene.instance == null) return;

            WorldPolicy.ApplyServerPolicy();

            if (_warmup > 0f)
            {
                _warmup -= Time.deltaTime;
                if (_warmup > 0f) return;

                if (!_catchUpDone && HusnisseConfig.CatchUpOnLoad.Value)
                {
                    _catchUpDone = true;
                    Log.Info("catch-up pass after world load");
                    RunAll(2f);
                }
            }

            _timer += Time.deltaTime;
            var interval = Mathf.Max(5f, HusnisseConfig.RoundInterval.Value);
            if (_timer < interval) return;

            var hours = _timer / 3600f;
            _timer = 0f;
            RunAll(1f, hours);
        }

        private void RunAll(float multiplier, float hours = 0f)
        {
            var claims = ClaimRegistry.All();
            if (claims.Count == 0) return;

            foreach (var claim in claims)
            {
                if (claim == null || claim.Bowl == null) continue;
                if (claim.Suppressed) continue;

                if (hours > 0f) claim.Bowl.Decay(hours);

                if (!claim.Bowl.IsFed) continue;

                var nisse = NisseFactory.EnsureNisse(claim.Bowl);
                if (nisse == null) continue;

                var context = new RoundContext(
                    claim,
                    claim.Bowl,
                    nisse,
                    Scale(HusnisseConfig.CollectBudget.Value, multiplier),
                    Scale(HusnisseConfig.SortBudget.Value, multiplier),
                    Scale(HusnisseConfig.MachineBudget.Value, multiplier),
                    Scale(HusnisseConfig.RefuelBudget.Value, multiplier));

                foreach (var task in _tasks)
                {
                    if (!HusnisseConfig.Enabled.Value) break;
                    task.Run(context);
                }

                if (context.Collected > 0 || context.Sorted > 0)
                {
                    Log.Debug($"round {claim.BowlId}: collected={context.Collected} sorted={context.Sorted}");
                }
            }
        }

        private static int Scale(int value, float multiplier)
        {
            return Mathf.RoundToInt(value * multiplier);
        }
    }
}
