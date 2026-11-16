using System.Collections.Generic;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Storage
{
    /// <summary>
    /// Configurable maximum stack sizes — the one Quartermaster surface that is a number rather than
    /// a behaviour, and the one place this mod writes a stack size at all.
    ///
    /// It is a fixed ceiling, not a multiplier: every stackable item gets the same maximum and
    /// single-item equipment keeps its native rule. Item weight is never touched. The write lands on
    /// each item prefab's <c>SharedData</c>, the object every stack of that item already points at,
    /// once, after the vanilla prefabs exist. A stack saved above the new ceiling is left exactly as
    /// it is: vanilla never splits on load, so lowering the setting cannot delete an item.
    ///
    /// When DropMoreLoot is installed it owns stack sizes (README, "Ownership hands over, it does not
    /// stack"), so nothing is written and the diagnostics say whose number is in play. That check is
    /// on the plugin being present, not on its own toggle: the policy belongs to the mod that owns it.
    /// </summary>
    public static class StackSizes
    {
        private static readonly Dictionary<string, int> Originals = new Dictionary<string, int>();
        private static string _policy = "native";
        private static int _maximum;
        private static bool _applied;

        /// <summary>
        /// Applies the ceiling once, at the first moment the item database is populated.
        ///
        /// `ObjectDB.m_items` is empty inside Jotunn's `OnVanillaPrefabsAvailable`: the event is
        /// raised from the database object's Awake, before the game copies the item list into it.
        /// So content registration only arms this, and the actual write happens from the
        /// `ObjectDB.CopyOtherDB` postfix (the copy that fills the list) with a per-frame retry from
        /// the flow pump as a fallback. Whichever comes first wins; the flag keeps it once.
        /// </summary>
        public static void Ensure()
        {
            if (_applied) return;

            var database = ObjectDB.instance;
            if (database == null || database.m_items == null || database.m_items.Count == 0) return;

            _applied = true;
            Apply();
        }

        /// <summary>husnisse, dropmoreloot, disabled or native — what the diagnostics print.</summary>
        public static string Policy
        {
            get { return _policy; }
        }

        /// <summary>The ceiling in force when this mod is the owner, or 0.</summary>
        public static int Maximum
        {
            get { return _maximum; }
        }

        /// <summary>How many stackable item prefabs were changed.</summary>
        public static int Count
        {
            get { return Originals.Count; }
        }

        /// <summary>
        /// Applies the configured ceiling once, after vanilla prefabs are available. Called from
        /// content registration; a client and a server each apply it to their own copy, and
        /// ConditionalConfigSync keeps the setting itself server-authoritative when the host locks
        /// the configuration.
        /// </summary>
        public static void Apply()
        {
            Originals.Clear();
            _maximum = 0;

            if (!HusnisseConfig.EnableStackSizes.Value)
            {
                _policy = "disabled";
                Log.Info("[Stacks] disabled by configuration; native limits stand");
                return;
            }

            if (DropMoreLootCompat.Installed)
            {
                _policy = "dropmoreloot";
                Log.Info("[Stacks] DropMoreLoot owns stack sizes (" + DropMoreLootCompat.Describe() +
                         "); Husnisse writes none");
                return;
            }

            var database = ObjectDB.instance;
            if (database == null || database.m_items == null)
            {
                _policy = "unavailable";
                Log.Warn("[Stacks] no ObjectDB; stack sizes were not applied");
                return;
            }

            var maximum = Mathf.Max(1, HusnisseConfig.MaxStackSize.Value);

            foreach (var prefab in database.m_items)
            {
                if (prefab == null) continue;

                var drop = prefab.GetComponent<ItemDrop>();
                var shared = drop != null && drop.m_itemData != null ? drop.m_itemData.m_shared : null;
                if (shared == null) continue;

                // Single-item equipment (weapons, armour, tools) is one per stack natively and stays
                // that way: raising it would let a player carry fifty swords in one slot.
                if (shared.m_maxStackSize <= 1) continue;

                var name = string.IsNullOrEmpty(shared.m_name) ? prefab.name : shared.m_name;
                if (!Originals.ContainsKey(name)) Originals[name] = shared.m_maxStackSize;

                shared.m_maxStackSize = maximum;
            }

            _policy = "husnisse";
            _maximum = maximum;

            Log.Info("[Stacks] maximum stack size " + maximum + " applied to " + Originals.Count +
                     " stackable item(s); item weight unchanged");
        }

        /// <summary>One line for `husnisse_status`: the policy in force, its ceiling and the breadth.</summary>
        public static string Describe()
        {
            switch (_policy)
            {
                case "husnisse":
                    return "stacks=husnisse max=" + _maximum + " items=" + Originals.Count;

                case "dropmoreloot":
                    return "stacks=dropmoreloot";

                case "disabled":
                    return "stacks=disabled";

                case "unavailable":
                    return "stacks=unavailable";

                default:
                    return "stacks=native";
            }
        }
    }
}
