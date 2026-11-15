namespace Husnisse.Util
{
    /// <summary>
    /// Our own ZDO key names. Deliberately prefixed and unrelated to any other storage mod's keys:
    /// save-format compatibility is not a goal, and shared key names are how two mods corrupt one chest.
    /// </summary>
    public static class ZdoKeys
    {
        // Bowl
        public const string BowlInit = "husnisse_bowl_init";
        public const string BowlRadius = "husnisse_bowl_radius";
        public const string BowlSatiety = "husnisse_bowl_satiety";
        public const string BowlMood = "husnisse_bowl_mood";
        public const string BowlOwner = "husnisse_bowl_owner";
        public const string BowlNisse = "husnisse_bowl_nisse";
        public const string BowlFeeding = "husnisse_bowl_feeding";
        public const string BowlSulk = "husnisse_bowl_sulk";
        public const string BowlLastSulk = "husnisse_bowl_last_sulk";

        // Claim authority. The first four are the documented 0.2 data model; the last two carry the
        // effective answer so a client or the harness can read a claim without re-resolving it.
        public const string ClaimAuthority = "husnisse_claim_authority";
        public const string ClaimWard = "husnisse_claim_ward";
        public const string ClaimSnapshotRadius = "husnisse_claim_snapshot_radius";
        public const string ClaimSnapshotOwner = "husnisse_claim_snapshot_owner";
        public const string ClaimRadius = "husnisse_claim_radius";
        public const string ClaimOwner = "husnisse_claim_owner";

        /// <summary>Per-claim pickup filter (0.3.1): the item types the nisse collects off the floor.
        /// Absent means all eight presets.</summary>
        public const string ClaimPickup = "husnisse_pickup";

        // Nisse
        public const string NisseBowl = "husnisse_nisse_bowl";
        public const string MemoryPrefix = "husnisse_mem_";

        // Container role
        public const string ContainerRole = "husnisse_role";

        // Container network (0.3). `husnisse_net` is the opt-in, `husnisse_rule` the whole rule as
        // one versioned string (see ContainerRule), `husnisse_blacklist` an explicit per-container
        // "never show a button". Prefab defaults (carts, ships, display piles) are not stored here.
        public const string ContainerNetwork = "husnisse_net";
        public const string ContainerRule = "husnisse_rule";
        public const string ContainerBlacklist = "husnisse_blacklist";
    }
}
