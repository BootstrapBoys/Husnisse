using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Config
{
    /// <summary>
    /// Whether feeding is forced is world state, not client config. The host's config value seeds a
    /// global key on world load; global keys are replicated to every peer and saved with the world, so
    /// there is no client-local copy that can disagree with what the server actually simulates.
    ///
    /// Key present → the server forces every nisse to be fed.
    /// Key absent  → each player decides at their own bowl.
    /// </summary>
    public static class WorldPolicy
    {
        private const string RequiredKey = "husnisse_feeding_required";

        private static bool _applied;

        /// <summary>The policy every peer reads.</summary>
        public static bool FeedingRequired
        {
            get
            {
                var zone = ZoneSystem.instance;
                return zone != null && zone.GetGlobalKey(RequiredKey);
            }
        }

        /// <summary>Server only: publish the host's configured policy into the world. Runs once per session.</summary>
        public static void ApplyServerPolicy()
        {
            if (_applied) return;
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;

            var zone = ZoneSystem.instance;
            if (zone == null) return;

            _applied = true;

            if (HusnisseConfig.FeedingRequired.Value) zone.SetGlobalKey(RequiredKey);
            else zone.RemoveGlobalKey(RequiredKey);

            Log.Info("world feeding required=" + FeedingRequired);
            SaveSchema.Publish();
        }

        /// <summary>Live policy change, for the admin command when it lands.</summary>
        public static void Force(bool required)
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return;

            if (required) zone.SetGlobalKey(RequiredKey);
            else zone.RemoveGlobalKey(RequiredKey);
        }
    }
}
