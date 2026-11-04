using UnityEngine;

namespace Husnisse.Config
{
    /// <summary>
    /// Save-format version, written into the world as a global key. When 0.2 changes memory or 0.5
    /// imports Quartermaster, a save can say which code wrote it instead of being guessed at.
    /// </summary>
    public static class SaveSchema
    {
        public const int Version = 1;
        private const string Prefix = "husnisse_schema_";

        public static void Publish()
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return;
            zone.SetGlobalKey(Prefix + Version);
        }

        public static int Read()
        {
            var zone = ZoneSystem.instance;
            if (zone == null) return 0;
            for (var v = Version; v >= 1; v--)
            {
                if (zone.GetGlobalKey(Prefix + v)) return v;
            }
            return 0;
        }
    }
}
