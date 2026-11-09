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
        public static ConfigEntry<bool> WriteDiagnosticsFile;
        public static ConfigEntry<bool> LockConfiguration;
        public static ConfigEntry<bool> CatchUpOnLoad;
        public static ConfigEntry<bool> FeedingRequired;
        public static ConfigEntry<bool> AllowOrders;
        public static ConfigEntry<float> BowlDrainMinutes;
        public static ConfigEntry<float> ItemsPerSecond;
        public static ConfigEntry<float> ReconcileInterval;
        public static ConfigEntry<float> ClaimRadius;
        public static ConfigEntry<float> MoodDecayPerHour;
        public static ConfigEntry<int> SulkMisfileCap;
        public static ConfigEntry<int> SulkIntervalMinutes;
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

            WriteDiagnosticsFile = file.Bind("1. General", "Write Diagnostics File", true,
                "Write BepInEx/BSBLogs/Husnisse.log: session header, claim and container dumps at startup, on demand and at shutdown.");

            LockConfiguration = file.Bind("1. General", "Lock Configuration", true,
                "Server-authoritative settings: when the host locks this, clients use the host's values.");

            CatchUpOnLoad = file.Bind("2. Nisse", "Catch-up Pass on Load", true,
                "Run one bounded pass when a world loads, so a backlog is worked through once rather than fifty rounds.");

            FeedingRequired = file.Bind("2. Nisse", "Feeding Required", true,
                "When on, every nisse must be fed and nobody can switch it off. When off, feeding is opt-in per bowl: " +
                "a new bowl asks for nothing and its owner can turn the care loop on with Alt-interact. The host writes " +
                "this into the world on load; clients cannot change it and a change takes effect on the next world load.");

            AllowOrders = file.Bind("2. Nisse", "Allow Orders", true,
                "Empty Pockets (interact with the nisse) and Tidy (Alt-interact a chest inside a claim).");

            BowlDrainMinutes = file.Bind("2. Nisse", "Bowl Drain Minutes", 20f,
                new ConfigDescription("Minutes between eating one item from the bowl. Only drains while satiety is below 100.",
                    new AcceptableValueRange<float>(1f, 240f)));

            ClaimRadius = file.Bind("2. Nisse", "Claim Radius", 20f,
                new ConfigDescription("Radius a nisse claims around its bowl, in metres.", new AcceptableValueRange<float>(5f, 50f)));

            MoodDecayPerHour = file.Bind("2. Nisse", "Mood Decay Per Hour", 20f,
                new ConfigDescription("Mood lost per hour of neglect while a player is online.", new AcceptableValueRange<float>(0f, 100f)));

            ItemsPerSecond = file.Bind("3. Flow", "Items Per Second", 5f,
                new ConfigDescription("Throughput of the nisse's queue. Presets: 2, 5 or 10 items per second.",
                    new AcceptableValueList<float>(2f, 5f, 10f)));

            ReconcileInterval = file.Bind("3. Flow", "Reconcile Interval", 5f,
                new ConfigDescription("Seconds between queue refills from ground drops and container work.",
                    new AcceptableValueRange<float>(1f, 30f)));

            SulkMisfileCap = file.Bind("4. Storage", "Sulk Misfile Cap", 3,
                new ConfigDescription("Maximum stacks a sulking nisse may hide. Nothing is ever deleted.", new AcceptableValueRange<int>(0, 20)));

            SulkIntervalMinutes = file.Bind("4. Storage", "Sulk Interval Minutes", 60,
                new ConfigDescription("Minutes between misfilings once a nisse is sulking.", new AcceptableValueRange<int>(1, 1440)));

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
