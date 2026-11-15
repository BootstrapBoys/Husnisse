using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Claims
{
    /// <summary>
    /// Keeps claims live when wards come and go. WardIsLove raises no event we can subscribe to
    /// without reaching into its internals, so this re-resolves on a slow timer: two seconds is
    /// invisible next to the nisse's own walking, and the pass is a few claims against a few wards.
    /// Runs on server and client alike; only the ZDO owner writes the answer.
    /// </summary>
    public class ClaimMonitor : MonoBehaviour
    {
        private const float Interval = 2f;
        private float _timer;

        public static ClaimMonitor Attach(GameObject host)
        {
            if (host == null) return null;
            var existing = host.GetComponent<ClaimMonitor>();
            return existing != null ? existing : host.AddComponent<ClaimMonitor>();
        }

        private void Update()
        {
            if (!HusnissePlugin.Running) return;
            if (ZNetScene.instance == null) return;

            _timer += Time.deltaTime;
            if (_timer < Interval) return;
            _timer = 0f;

            // Presets are world state: a client that connected after the key was published has to
            // apply the table before it draws a suggestion, and this is the one pass every peer runs.
            Storage.PresetStore.Ensure();

            var changed = ClaimRegistry.RevalidateAll();
            if (changed > 0)
            {
                Log.Debug("Claims", "ward pass: " + changed + " claim(s) changed");
            }
        }
    }
}
