namespace Husnisse.Util
{
    /// <summary>
    /// Our own ZDO key names. Deliberately prefixed and unrelated to any other storage mod's keys:
    /// save-format compatibility is not a goal, and shared key names are how two mods corrupt one chest.
    /// </summary>
    public static class ZdoKeys
    {
        // Bowl
        public const string BowlRadius = "husnisse_bowl_radius";
        public const string BowlSatiety = "husnisse_bowl_satiety";
        public const string BowlMood = "husnisse_bowl_mood";
        public const string BowlOwner = "husnisse_bowl_owner";
        public const string BowlNisse = "husnisse_bowl_nisse";
        public const string BowlFeeding = "husnisse_bowl_feeding";

        // Nisse
        public const string NisseBowl = "husnisse_nisse_bowl";
        public const string MemoryPrefix = "husnisse_mem_";

        // Container role
        public const string ContainerRole = "husnisse_role";
    }
}
