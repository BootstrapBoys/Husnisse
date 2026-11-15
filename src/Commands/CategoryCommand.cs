using System.Collections.Generic;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Commands
{
    /// <summary>
    /// `husnisse_categories` — what the eight presets actually cover, and what they miss.
    ///
    /// The presets are a table of item types plus the named materials Valheim's item type cannot
    /// separate (see CategoryMap). A table like that rots silently when the game adds items, so this
    /// command answers the only question that matters after an update: which items in *this* build
    /// does no preset know? It is also how the table was built in the first place.
    /// </summary>
    public static class CategoryCommand
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand(
                "husnisse_categories",
                "Preset coverage: [check | dump | list | <metals|wood|food|...>].",
                delegate(Terminal.ConsoleEventArgs args) { Run(args); });
        }

        private static void Run(Terminal.ConsoleEventArgs args)
        {
            var sub = args.Args.Length > 1 ? args.Args[1].ToLowerInvariant() : "list";

            if (sub == "check")
            {
                Check(args.Context);
                return;
            }

            if (sub == "dump")
            {
                Dump(args.Context);
                return;
            }

            var category = ContainerRule.CategoryFromToken(sub);
            if (category != ItemCategory.None)
            {
                List(args.Context, category);
                return;
            }

            if (sub != "list")
            {
                args.Context?.AddString("usage: husnisse_categories [check | dump | list | <category>]");
                return;
            }

            List(args.Context, ItemCategory.None);
        }

        /// <summary>One line per preset and the holes in the table, in a shape a scenario can assert.</summary>
        private static void Check(Terminal context)
        {
            var audit = CategoryMap.Audit();

            foreach (var category in ContainerRule.Presets)
            {
                var game = 0;
                foreach (var prefab in Items())
                {
                    var drop = prefab.GetComponent<ItemDrop>();
                    if (drop == null || drop.m_itemData == null) continue;
                    if (CategoryMap.Accepts(category, drop.m_itemData)) game++;
                }

                Line(context, "category " + ContainerRule.CategoryToken(category) +
                              " game=" + game + " named=" + Count(CategoryMap.NamesOf(category)));
            }

            Line(context, "categories items=" + audit.Items + " covered=" + audit.Covered +
                          " gaps=" + audit.Gaps.Count + " overlaps=" + audit.Overlaps.Count +
                          " gear=" + audit.Gear);

            foreach (var gap in audit.Gaps) Line(context, "gap " + gap);
            foreach (var overlap in audit.Overlaps) Line(context, "overlap " + overlap);
        }

        /// <summary>Every item this build has, with its type, satiety and the preset that claims it.</summary>
        private static void Dump(Terminal context)
        {
            foreach (var prefab in Items())
            {
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                Line(context, "item " + drop.m_itemData.m_shared.m_name + " " + CategoryMap.Describe(drop.m_itemData.m_shared));
            }
        }

        /// <summary>The items one preset accepts in this build, or every preset's when None.</summary>
        private static void List(Terminal context, ItemCategory category)
        {
            if (category == ItemCategory.None)
            {
                Check(context);
                return;
            }

            var names = new List<string>();
            foreach (var prefab in Items())
            {
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null) continue;
                if (!CategoryMap.Accepts(category, drop.m_itemData)) continue;
                names.Add(drop.m_itemData.m_shared.m_name);
            }

            names.Sort(System.StringComparer.OrdinalIgnoreCase);
            Line(context, "category " + ContainerRule.CategoryToken(category) + " count=" + names.Count);
            foreach (var name in names) Line(context, "  " + name);
        }

        private static IEnumerable<GameObject> Items()
        {
            var database = ObjectDB.instance;
            if (database == null || database.m_items == null) return new GameObject[0];
            return database.m_items;
        }

        private static int Count(IEnumerable<string> names)
        {
            var count = 0;
            foreach (var unused in names) count++;
            return count;
        }

        private static void Line(Terminal context, string text)
        {
            context?.AddString(text);
            Core.DiagnosticsFile.Line(text);
        }
    }
}
