using System;
using System.Collections.Generic;
using System.Text;

namespace Husnisse.Storage
{
    /// <summary>
    /// The eight survival presets expanded into the items they accept.
    ///
    /// Four of them are pure item types: food and mead split a single Valve type on satiety, trophies
    /// and tools are their own type. The other four cannot be: Valheim files ore, timber, cut stone
    /// and treasure all under <c>ItemType.Material</c>, so metals, wood, valuables and building
    /// materials are named. The names are the *shared* names (<c>$item_coal</c>), which is what a
    /// container stores and what a custom list stores, so a rule means the same thing in every
    /// language and an EpicLoot variant shares its base item's home.
    ///
    /// A table like this is a liability if it drifts from the game, so it is auditable at runtime:
    /// <c>husnisse_categories check</c> lists every item the table does not classify and every item
    /// two presets both claim. Weapons, armour, ammunition and gear are deliberately unclassified —
    /// the presets are for a base's logistics, and a player who wants a weapons chest writes a
    /// custom list. 0.3.1 makes these presets editable from YAML; until then this file is the table.
    /// </summary>
    public static class CategoryMap
    {
        /// <summary>
        /// The items the item type cannot separate. Keep each name in exactly one set: the audit
        /// command reports an overlap, and <see cref="Of"/> would answer with the earlier preset.
        ///
        /// The names were read off a dedicated server's ObjectDB (`husnisse_categories dump`) rather
        /// than typed from memory, and `husnisse_categories check` re-reads it: the list covers the
        /// families a base actually stores, and what is missing is printed rather than guessed at.
        /// </summary>
        private static readonly Dictionary<ItemCategory, HashSet<string>> Named =
            new Dictionary<ItemCategory, HashSet<string>>
            {
                // Ore, scrap, bars, nails and the smelter's fuel.
                { ItemCategory.Metals, Set(
                    "$item_blackmetal", "$item_blackmetalscrap", "$item_bronze", "$item_bronzenails",
                    "$item_bronzescrap", "$item_chain", "$item_coal", "$item_copper",
                    "$item_copperore", "$item_copperscrap", "$item_flametal", "$item_flametal_old",
                    "$item_flametalore", "$item_flametalore_old", "$item_gold", "$item_goldore",
                    "$item_iron", "$item_ironnails", "$item_ironore", "$item_ironscrap",
                    "$item_moltencore", "$item_silver", "$item_silverore", "$item_tin",
                    "$item_tinore") },

                // Timber and what comes off a tree.
                { ItemCategory.Wood, Set(
                    "$item_blackwood", "$item_elderbark", "$item_finewood", "$item_frostwood",
                    "$item_guck", "$item_resin", "$item_root", "$item_roundlog",
                    "$item_sap", "$item_wood", "$item_yggdrasilwood") },

                // Cut stone and the materials that build with it.
                { ItemCategory.Building, Set(
                    "$item_barrelrings", "$item_blackmarble", "$item_ceramicplate", "$item_charredbone",
                    "$item_crystal", "$item_grausten", "$item_stone", "$item_stonerock",
                    "$item_tar") },

                // Treasure: what a player sells or keeps, not what a forge eats.
                { ItemCategory.Valuables, Set(
                    "$item_amber", "$item_amberpearl", "$item_ancientcoin", "$item_ancientgemstone_black",
                    "$item_ancientgemstone_green", "$item_ancientgemstone_orange",
                    "$item_ancientgemstone_purple", "$item_coins", "$item_crownjewel",
                    "$item_gemstone_blue", "$item_gemstone_green",
                    "$item_gemstone_red", "$item_pungentpebbles", "$item_ruby",
                    "$item_silvernecklace") },

                // Ingredients, crops and seeds: the pantry, not only the cooked meal. Anything with
                // satiety is food by type already; these are the things that become a meal.
                { ItemCategory.Food, Set(
                    "$item_ancientseed", "$item_barley", "$item_barleyflour", "$item_beechseeds",
                    "$item_birchseeds", "$item_carrotseeds", "$item_dandelion", "$item_feastashlands",
                    "$item_feastblackforest", "$item_feastdeepnorth", "$item_feastmeadows",
                    "$item_feastmistlands", "$item_feastmountains", "$item_feastoceans",
                    "$item_feastplains", "$item_feastswamps", "$item_fircone", "$item_fircone_big",
                    "$item_flax", "$item_oakseeds", "$item_oatflour", "$item_oatseeds",
                    "$item_onionseeds", "$item_pinecone", "$item_poteitrseeds", "$item_spiceashlands",
                    "$item_spicedeepnorth", "$item_spiceforests", "$item_spicemistlands",
                    "$item_spicemountains", "$item_spiceoceans", "$item_spiceplains",
                    "$item_thistle", "$item_turnipseeds", "$item_vineberryseeds",
                    "$item_vinegreenseeds") },

                // Mead bases and fireworks: consumable-shaped, but the game files them as materials.
                { ItemCategory.Consumables, Set(
                    "$item_barleywinebase", "$item_fireworkrocket_blue", "$item_fireworkrocket_cyan",
                    "$item_fireworkrocket_green", "$item_fireworkrocket_purple",
                    "$item_fireworkrocket_red", "$item_fireworkrocket_white",
                    "$item_fireworkrocket_yellow", "$item_meadbasebugrepellent",
                    "$item_meadbasebzerker", "$item_meadbaseeitr", "$item_meadbaseeitr_lingering",
                    "$item_meadbasefrostresist", "$item_meadbasehasty", "$item_meadbasehealth",
                    "$item_meadbasehealth_lingering", "$item_meadbasehealth_major",
                    "$item_meadbasehealth_medium", "$item_meadbaselightfoot",
                    "$item_meadbasepoisonresist", "$item_meadbasestamina",
                    "$item_meadbasestamina_lingering", "$item_meadbasestamina_medium",
                    "$item_meadbasestrength", "$item_meadbaseswimmer", "$item_meadbasetamer",
                    "$item_meadbasetasty") },

                // The tools a base keeps in one chest. Valheim files axes and pickaxes as weapons,
                // so they are named here: a player looking for their spare axe looks in Tools.
                { ItemCategory.Tools, Set(
                    "$item_axe2h_gold", "$item_axe2h_gold_bloodlightning", "$item_axe2h_gold_frostfire",
                    "$item_axe_berzerkr", "$item_axe_berzerkr_blood", "$item_axe_berzerkr_lightning",
                    "$item_axe_berzerkr_nature", "$item_axe_blackmetal", "$item_axe_bronze",
                    "$item_axe_early", "$item_axe_flint", "$item_axe_gold",
                    "$item_axe_gold_bloodlightning", "$item_axe_gold_frostfire",
                    "$item_axe_gold_uncooked", "$item_axe_iron", "$item_axe_jotunbane",
                    "$item_axe_stone", "$item_axe_wood", "$item_axehead1", "$item_axehead2",
                    "$item_fishingbait", "$item_fishingrod", "$item_pickaxe_antler",
                    "$item_pickaxe_blackmetal", "$item_pickaxe_bronze", "$item_pickaxe_iron",
                    "$item_pickaxe_stone") },
            };

        private static HashSet<string> Set(params string[] names)
        {
            return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        }

        private static ItemDrop.ItemData.ItemType TypeOf(ItemDrop.ItemData item)
        {
            return item.m_shared.m_itemType;
        }

        /// <summary>Whether one preset accepts one stack. Cheap enough for a per-tick scan.</summary>
        public static bool Accepts(ItemCategory category, ItemDrop.ItemData item)
        {
            if (category == ItemCategory.None || item == null || item.m_shared == null) return false;

            var shared = item.m_shared;
            switch (category)
            {
                case ItemCategory.Food:
                    // Anything with satiety is food, whatever type the game files it under:Valheim
                    // marks most raw meat and every uncooked meal as Material, not Consumable.
                    return shared.m_food > 0f;

                case ItemCategory.Consumables:
                    return shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && shared.m_food <= 0f;

                case ItemCategory.Trophies:
                    return shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;

                case ItemCategory.Tools:
                    return shared.m_itemType == ItemDrop.ItemData.ItemType.Tool || InSet(category, shared.m_name);

                default:
                    return InSet(category, shared.m_name);
            }
        }

        private static bool InSet(ItemCategory category, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            HashSet<string> names;
            return Named.TryGetValue(category, out names) && names.Contains(name);
        }

        /// <summary>
        /// The one preset a stack belongs to, or None. Order is <see cref="ContainerRule.Presets"/>,
        /// so the answer is stable for the audit and for a suggestion.
        /// </summary>
        public static ItemCategory Of(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null) return ItemCategory.None;
            foreach (var category in ContainerRule.Presets)
            {
                if (Accepts(category, item)) return category;
            }
            return ItemCategory.None;
        }

        /// <summary>
        /// The category most of a container's contents belong to, for the first-toggle suggestion.
        /// Counted in stacks, not items: a chest of ten coal and one bar is a metals chest either
        /// way, and a stack of 100 arrows would otherwise outvote everything. Ties go to the earlier
        /// preset so the suggestion never flickers between two answers.
        /// </summary>
        public static ItemCategory Dominant(IEnumerable<ItemDrop.ItemData> items)
        {
            if (items == null) return ItemCategory.None;

            var counts = new Dictionary<ItemCategory, int>();
            var best = ItemCategory.None;
            var bestCount = 0;

            foreach (var item in items)
            {
                var category = Of(item);
                if (category == ItemCategory.None) continue;

                int count;
                counts.TryGetValue(category, out count);
                counts[category] = ++count;

                if (count > bestCount)
                {
                    bestCount = count;
                    best = category;
                }
            }

            return bestCount > 0 ? best : ItemCategory.None;
        }

        /// <summary>The names a preset carries by name, for the ledger and for diagnostics.</summary>
        public static IEnumerable<string> NamesOf(ItemCategory category)
        {
            HashSet<string> names;
            return Named.TryGetValue(category, out names) ? (IEnumerable<string>)names : new string[0];
        }

        /// <summary>
        /// Everything the table gets wrong, in one answer. Read by `husnisse_categories check`,
        /// which is how the table stays honest as the game adds items:
        ///
        /// - <b>Gaps</b> are items of a kind a preset is supposed to cover (materials, consumables,
        ///   trophies, tools, fish, misc) that no preset classifies — a real hole in the table.
        /// - <b>Overlaps</b> are items two presets both claim — always a mistake, because the answer
        ///   then depends on preset order.
        /// - <b>Gear</b> is weapons, armour, ammunition and the rest, counted but not listed: the eight
        ///   presets are a base's logistics, and gear needs a custom list or the player's back.
        /// </summary>
        public struct ItemAudit
        {
            public int Items;
            public int Covered;
            public int Gear;
            public List<string> Gaps;
            public List<string> Overlaps;
        }

        /// <summary>The item types a preset is expected to have an answer for.</summary>
        private static bool StorageWorthy(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.Material:
                case ItemDrop.ItemData.ItemType.Consumable:
                case ItemDrop.ItemData.ItemType.Trophy:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Misc:
                case ItemDrop.ItemData.ItemType.Fish:
                    return true;
                default:
                    return false;
            }
        }

        public static ItemAudit Audit()
        {
            var audit = new ItemAudit { Gaps = new List<string>(), Overlaps = new List<string>() };

            var database = ObjectDB.instance;
            if (database == null) return audit;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prefab in database.m_items)
            {
                if (prefab == null) continue;
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;

                var shared = drop.m_itemData.m_shared;
                if (!seen.Add(shared.m_name + "|" + shared.m_itemType)) continue;
                audit.Items++;

                var matches = 0;
                var names = new StringBuilder();
                foreach (var category in ContainerRule.Presets)
                {
                    if (!Accepts(category, drop.m_itemData)) continue;
                    matches++;
                    if (names.Length > 0) names.Append(',');
                    names.Append(ContainerRule.CategoryToken(category));
                }

                if (matches > 1) audit.Overlaps.Add(shared.m_name + " (" + names + ")");
                else if (matches == 1) audit.Covered++;
                else if (StorageWorthy(shared.m_itemType))
                    audit.Gaps.Add(shared.m_name + " type=" + shared.m_itemType + " food=" + shared.m_food.ToString("0.#"));
                else audit.Gear++;
            }

            return audit;
        }

        /// <summary>The audit line for one shared item, so a dump can be read without a client.</summary>
        public static string Describe(ItemDrop.ItemData.SharedData shared)
        {
            if (shared == null) return "none";
            var category = Of(new ItemDrop.ItemData { m_shared = shared });
            return "type=" + shared.m_itemType +
                   " food=" + shared.m_food.ToString("0.#") +
                   " cat=" + (category == ItemCategory.None ? "unclassified" : ContainerRule.CategoryToken(category));
        }
    }
}
