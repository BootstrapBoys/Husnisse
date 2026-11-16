using BepInEx.Configuration;
using ConditionalConfigSync;
using UnityEngine;

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
        public static ConfigEntry<KeyboardShortcut> LedgerKey;
        public static ConfigEntry<bool> LockConfiguration;
        public static ConfigEntry<bool> CatchUpOnLoad;
        public static ConfigEntry<bool> FeedingRequired;
        public static ConfigEntry<bool> AllowOrders;
        public static ConfigEntry<float> BowlDrainMinutes;
        public static ConfigEntry<float> ItemsPerSecond;
        public static ConfigEntry<float> ReconcileInterval;
        public static ConfigEntry<float> ClaimRadius;
        public static ConfigEntry<float> LodDistance;
        public static ConfigEntry<float> MoodDecayPerHour;
        public static ConfigEntry<int> SulkMisfileCap;
        public static ConfigEntry<int> SulkIntervalMinutes;
        public static ConfigEntry<bool> SkipMobileContainers;
        public static ConfigEntry<bool> SkipDisplayPiles;
        public static ConfigEntry<bool> UnloadShips;
        public static ConfigEntry<bool> ExtendStationCoverage;
        public static ConfigEntry<bool> EnableStackSizes;
        public static ConfigEntry<int> MaxStackSize;
        public static ConfigEntry<bool> RespectWards;
        public static ConfigEntry<bool> RequireWardOwnership;
        public static ConfigEntry<bool> RequirementReadouts;
        public static ConfigEntry<bool> SplitRequirementCounts;
        public static ConfigEntry<bool> CraftSupply;
        public static ConfigEntry<bool> TendMachines;
        public static ConfigEntry<int> MachineFuelPercent;
        public static ConfigEntry<int> MachineInputPercent;
        public static ConfigEntry<int> CollectAt;
        public static ConfigEntry<bool> CollectOutput;
        public static ConfigEntry<bool> RefuelFires;
        public static ConfigEntry<bool> FeedAnimals;
        public static ConfigEntry<bool> ProtectValuableWood;

        private static ConfigSync _sync;

        public static void Bind(ConfigFile file, string pluginGuid)
        {
            Enabled = file.Bind("1. General", "Enabled", true,
                "Master switch. When false the nisse does nothing and the ledger says why.");

            DebugLogging = file.Bind("1. General", "Debug Logging", false,
                "Verbose diagnostics for claims, rounds and container scans.");

            WriteDiagnosticsFile = file.Bind("1. General", "Write Diagnostics File", true,
                "Write BepInEx/BSBLogs/Husnisse.log: session header, claim and container dumps at startup, on demand and at shutdown.");

            LedgerKey = file.Bind("1. General", "Ledger Key", new KeyboardShortcut(KeyCode.H),
                "Opens the claim ledger while the inventory is open. The ledger lists every container in the claim, " +
                "its rule, the unclassified ones, the pickup filter and the blacklist.");

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

            LodDistance = file.Bind("2. Nisse", "Animation LOD Distance", 30f,
                new ConfigDescription("A nisse farther than this from every player stops being animated; its work stays in the queue. Sampled twice a second, per peer.",
                    new AcceptableValueRange<float>(5f, 120f)));

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
                "Never file items into a cart or ship hold; goods would sail away. Ship unloading (below) is the opposite direction and is not affected.");

            SkipDisplayPiles = file.Bind("4. Storage", "Skip Display Piles", true,
                "Never treat DynamicStoragePiles or the Muji DynamicStorage display containers as storage.");

            UnloadShips = file.Bind("4. Storage", "Unload Ships", true,
                "A ship's hold inside a claim is emptied into the chests whose rules claim it, at the queue's rate and one visible trip at a time. " +
                "A stack nothing accepts stays aboard; a boat that leaves the claim mid-trip is not touched. Carts stay excluded.");

            ExtendStationCoverage = file.Bind("4. Storage", "Extend Station Coverage", true,
                "A workbench, forge, stonecutter or other station standing anywhere inside a claim supports building, structure repair and " +
                "dismantling throughout that claim. Crafting, item upgrades and item repair still require walking up to the station.");

            EnableStackSizes = file.Bind("4. Storage", "Enable Stack Sizes", true,
                "Apply one maximum stack size to every stackable item. Off leaves native limits — or DropMoreLoot's, when it is installed — in charge. Restart to take effect.");

            MaxStackSize = file.Bind("4. Storage", "Max Stack Size", 1000,
                new ConfigDescription("Maximum stack size for stackable items. Item weight is never changed, single-item equipment keeps its native limit, " +
                    "and a stack already larger than the new maximum is left alone until it is split. Restart to take effect.",
                    new AcceptableValueRange<int>(1, 10000)));

            RequirementReadouts = file.Bind("5. Contract", "Requirement Readouts", true,
                "Show what the claim can reach on crafting and build requirement rows, and let AAA Crafting, " +
                "AAABuildMenu and MyLittleUI read the claim instead of the pockets only. Off leaves every " +
                "crafting UI exactly vanilla. The contract itself (Count/CanAfford) keeps answering either way.");

            SplitRequirementCounts = file.Bind("5. Contract", "Split Requirement Counts", true,
                "Write a requirement's available amount as `42 [12+30]` — total, then what you carry and what " +
                "the claim holds. Off writes the total alone. Split by default so a number that is mostly " +
                "base stock is visible as such while this build is still read-only.");

            CraftSupply = file.Bind("5. Contract", "Craft from Claim", true,
                "A craft the pockets cannot pay for becomes a supply errand: the nisse fetches the missing " +
                "materials from the claim's containers, carries them to you, and hands them over; the craft " +
                "then runs as vanilla does, spending your pockets. Off leaves every craft pocket-only.");

            RespectWards = file.Bind("6. Ward", "Respect WardIsLove", true,
                "Ward permissions filter every container. A bowl inside a ward its owner cannot use refuses to settle.");

            RequireWardOwnership = file.Bind("6. Ward", "Require Ward Ownership", false,
                "When on, a nisse may only use containers in wards its own player placed. Off by default: a permitted tribe member's nisse may use the ward owner's chests.");

            TendMachines = file.Bind("7. Machines", "Tend Machines", true,
                "The nisse feeds and empties the base's machines: smelters, kilns, refineries, fermenters, " +
                "cooking stations and ovens take fuel and input and give back finished goods. Off leaves every " +
                "machine to the player and stops the machine work only.");

            MachineFuelPercent = file.Bind("7. Machines", "Fuel Cap Percent", 50,
                new ConfigDescription("How full the nisse keeps a fire or a furnace's fuel: a cap, not a target to " +
                    "exceed. A machine is topped up only when its fuel falls below this share of its own maximum, " +
                    "and never past it.",
                    new AcceptableValueRange<int>(1, 100)));

            MachineInputPercent = file.Bind("7. Machines", "Input Cap Percent", 50,
                new ConfigDescription("The same cap for a processor's ore queue and a cooking station's slots.",
                    new AcceptableValueRange<int>(1, 100)));

            CollectAt = file.Bind("7. Machines", "Collect At", 1,
                new ConfigDescription("Finished units a machine must hold before the nisse walks over to empty it. 1 " +
                    "collects as soon as anything is ready.",
                    new AcceptableValueRange<int>(1, 20)));

            CollectOutput = file.Bind("7. Machines", "Collect Output", true,
                "Carry finished goods out of machines to the chests whose rules claim them. Off leaves output where " +
                "the machine drops it.");

            RefuelFires = file.Bind("7. Machines", "Refuel Fires", true,
                "Keep fire pits, hearths, bonfires and standing torches burning with the fuel each one names.");

            FeedAnimals = file.Bind("7. Machines", "Feed Animals", true,
                "Lay a portion down for a hungry tameable creature so taming continues and a tame herd keeps " +
                "breeding.");

            ProtectValuableWood = file.Bind("7. Machines", "Protect Valuable Wood", true,
                "Never burn fine wood, core wood, elder bark, ancient bark or Yggdrasil wood as fuel, even if a " +
                "machine names one of them. A machine that asks for it is left alone and says so in the log.");

            _sync = new ConfigSync(pluginGuid);
            _sync.AddLockingConfigEntry(LockConfiguration);
        }
    }
}
