using BepInEx.Configuration;
using ConditionalConfigSync;

namespace Husnisse.Config
{
    /// <summary>
    /// All settings in one place, grouped into the numbered sections the family uses so BepInEx's
    /// alphabetical save order cannot scramble them.
    /// </summary>
    public static class HusnisseConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> DebugLogging;
        public static ConfigEntry<bool> LockConfiguration;
        public static ConfigEntry<bool> CatchUpOnLoad;
        public static ConfigEntry<bool> RequireFeeding;
        public static ConfigEntry<float> RoundInterval;
        public static ConfigEntry<float> ClaimRadius;
        public static ConfigEntry<float> MoodDecayPerHour;
        public static ConfigEntry<int> CollectBudget;
        public static ConfigEntry<int> SortBudget;
        public static ConfigEntry<int> MachineBudget;
        public static ConfigEntry<int> RefuelBudget;
        public static ConfigEntry<int> SulkMisfileCap;
        public static ConfigEntry<bool> SkipMobileContainers;
        public static ConfigEntry<bool> SkipDisplayPiles;
        public static ConfigEntry<bool> RespectWards;
        public static ConfigEntry<bool> RequireWardOwnership;

        private static ConfigSync _sync;

        public static void Bind(ConfigFile file, string pluginGuid)
        {
            Enabled = file.Bind("1. General", "Enabled", true,
                "Master switch. When false the nisse does nothing and the ledger says why.");

            DebugLogging = file.Bind("1. General", "Debug Logging", false,
                "Verbose diagnostics for claims, rounds and container scans.");

            LockConfiguration = file.Bind("1. General", "Lock Configuration", true,
                "Server-authoritative settings: when the host locks this, clients use the host's values.");

            CatchUpOnLoad = file.Bind("2. Nisse", "Catch-up Pass on Load", true,
                "Run one bounded pass when a world loads, so a backlog is worked through once rather than fifty rounds.");

            RequireFeeding = file.Bind("2. Nisse", "Require Feeding", true,
                "When off, the nisse works without being fed: satiety, mood decay and sulking are all disabled, and the bowl is decoration.");

            RoundInterval = file.Bind("2. Nisse", "Round Interval", 30f,
                new ConfigDescription("Seconds between rounds.", new AcceptableValueRange<float>(5f, 600f)));

            ClaimRadius = file.Bind("2. Nisse", "Claim Radius", 20f,
                new ConfigDescription("Radius a nisse claims around its bowl, in metres.", new AcceptableValueRange<float>(5f, 50f)));

            MoodDecayPerHour = file.Bind("2. Nisse", "Mood Decay Per Hour", 20f,
                new ConfigDescription("Mood lost per hour of neglect while a player is online.", new AcceptableValueRange<float>(0f, 100f)));

            CollectBudget = file.Bind("3. Rounds", "Collect Budget", 20,
                new ConfigDescription("Ground items a nisse may pick up per round.", new AcceptableValueRange<int>(0, 200)));

            SortBudget = file.Bind("3. Rounds", "Sort Budget", 40,
                new ConfigDescription("Item stacks a nisse may file per round.", new AcceptableValueRange<int>(0, 400)));

            MachineBudget = file.Bind("3. Rounds", "Machine Budget", 10,
                new ConfigDescription("Machine actions per round. Unused until 0.4.", new AcceptableValueRange<int>(0, 100)));

            RefuelBudget = file.Bind("3. Rounds", "Refuel Budget", 10,
                new ConfigDescription("Fires and troughs serviced per round. Unused until 0.4.", new AcceptableValueRange<int>(0, 100)));

            SulkMisfileCap = file.Bind("4. Storage", "Sulk Misfile Cap", 3,
                new ConfigDescription("Maximum stacks a sulking nisse may hide. Nothing is ever deleted.", new AcceptableValueRange<int>(0, 20)));

            SkipMobileContainers = file.Bind("4. Storage", "Skip Carts and Ships", true,
                "Never file items into a cart or ship hold; goods would sail away.");

            SkipDisplayPiles = file.Bind("4. Storage", "Skip Display Piles", true,
                "Never treat DynamicStoragePiles or the Muji DynamicStorage display containers as storage.");

            RespectWards = file.Bind("6. Ward", "Respect WardIsLove", true,
                "Ward permissions filter every container. A bowl inside a ward its owner cannot use refuses to settle.");

            RequireWardOwnership = file.Bind("6. Ward", "Require Ward Ownership", false,
                "When on, a nisse may only use containers in wards its own player placed. Off by default: a permitted tribe member's nisse may use the ward owner's chests.");

            _sync = new ConfigSync(pluginGuid);
            _sync.AddLockingConfigEntry(LockConfiguration);
        }
    }
}
