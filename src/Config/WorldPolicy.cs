using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Config
{
    /// <summary>
    /// Feeding policy as world state, not client config. The host's config value seeds a global key on
    /// world load; global keys are replicated to every peer and saved with the world, so there is no
    /// client-local copy that can disagree with what the server actually simulates.
    /// </summary>
    public static class WorldPolicy
    {
        private const string RequiredKey = "husnisse_feeding_required";
        private const string OptionalKey = "husnisse_feeding_optional";
        private const string DisabledKey = "husnisse_feeding_disabled";

        private static bool _applied;

        /// <summary>The policy every peer reads. Missing keys mean Required.</summary>
        public static FeedingMode Feeding
        {
            get
            {
                var zone = ZoneSystem.instance;
                if (zone == null) return FeedingMode.Required;
                if (zone.GetGlobalKey(DisabledKey)) return FeedingMode.Disabled;
                if (zone.GetGlobalKey(OptionalKey)) return FeedingMode.Optional;
                return FeedingMode.Required;
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

            var mode = HusnisseConfig.FeedingPolicy.Value;
            var key = mode == FeedingMode.Disabled
                ? DisabledKey
                : mode == FeedingMode.Optional ? OptionalKey : RequiredKey;

            zone.RemoveGlobalKey(RequiredKey);
            zone.RemoveGlobalKey(OptionalKey);
            zone.RemoveGlobalKey(DisabledKey);
            zone.SetGlobalKey(key);

            Log.Info("world feeding policy set to " + mode);
        }

        /// <summary>Used by the server's own console or a future admin command.</summary>
        public static void Force(FeedingMode mode)
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return;
            zone.RemoveGlobalKey(RequiredKey);
            zone.RemoveGlobalKey(OptionalKey);
            zone.RemoveGlobalKey(DisabledKey);
            zone.SetGlobalKey(mode == FeedingMode.Disabled
                ? DisabledKey
                : mode == FeedingMode.Optional ? OptionalKey : RequiredKey);
        }
    }
}
