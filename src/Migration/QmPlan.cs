using System.Collections.Generic;
using System.Text;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Migration
{
    /// <summary>What one container's migration would do, and what it cannot carry over.</summary>
    public sealed class QmPlan
    {
        public QmContainer Source;
        public ContainerRule Rule = ContainerRule.None();
        public bool Network = true;
        public readonly List<string> Notes = new List<string>();
        public readonly List<string> Unmapped = new List<string>();

        /// <summary>True when there is something to write: a role, a list, a permission or a priority.</summary>
        public bool HasRule
        {
            get
            {
                if (Rule.Kind != RuleKind.None) return true;
                if (Rule.Supply != SupplyPurpose.All) return true;
                return Rule.Priority != 0;
            }
        }

        /// <summary>
        /// Maps one Quartermaster record onto a Husnisse rule.
        ///
        /// The two mods agree about the four supply permissions and about the two roles, so those map
        /// exactly. They disagree about item lists — Quartermaster remembers prefab names, Husnisse
        /// stores the game's shared item names, which is what makes an EpicLoot enchant variant share
        /// a home with its base item — so the list is translated through the item prefabs. They also
        /// disagree about scope: Quartermaster learns by watching and groups chests into named bases,
        /// while Husnisse is taught and its unit is the claim. Those become notes, not state.
        /// </summary>
        public static QmPlan For(QmContainer source)
        {
            var plan = new QmPlan { Source = source };
            var record = source.Record;
            if (record == null) return plan;

            if (record.Deposit)
            {
                plan.Rule.Kind = RuleKind.Deposit;
            }
            else if (record.Overflow)
            {
                plan.Rule.Kind = RuleKind.Overflow;
            }
            else
            {
                var items = MapItems(record, plan);
                if (items.Count > 0)
                {
                    plan.Rule.Kind = RuleKind.Custom;
                    plan.Rule.Items = items;
                }
            }

            plan.Rule.Supply = MapSupply(record, plan);

            if (record.Preferred)
            {
                plan.Rule.Priority = 5;
                plan.Notes.Add("preferred → priority 5");
            }

            // Notes for what has no equivalent, so the report is a decision the operator can read
            // rather than a silent loss.
            if (!record.Learn) plan.Notes.Add("learning was off for this chest");
            if (!record.AutoAssign) plan.Notes.Add("auto-assign was off");
            if (!string.IsNullOrEmpty(record.AutoCategories)) plan.Notes.Add("auto categories '" + record.AutoCategories + "' ignored");
            if (!string.IsNullOrEmpty(record.BaseName) || !string.IsNullOrEmpty(record.Group))
                plan.Notes.Add("base '" + (string.IsNullOrEmpty(record.BaseName) ? record.Group : record.BaseName) + "' — Husnisse groups by claim");
            if (record.PickupRange > 0f) plan.Notes.Add("pickup range " + record.PickupRange.ToString("0.#") + " m — the claim's own pickup filter is used");
            if (record.FeedingRange > 0f || record.FeedUntamed)
                plan.Notes.Add("feeding range/untamed settings — the claim's machines and animals decide");
            if (record.AutoSort) plan.Notes.Add("auto-sort on open ignored");
            if (!record.AcceptStorage) plan.Notes.Add("did not accept storage");

            return plan;
        }

        private static SupplyPurpose MapSupply(QmRecord record, QmPlan plan)
        {
            var supply = SupplyPurpose.All;

            if (!record.CraftingSupply) supply &= ~SupplyPurpose.Crafting;
            if (!record.ProcessingSupply) supply &= ~SupplyPurpose.Production;
            if (!record.FuelSupply) supply &= ~SupplyPurpose.Fuel;
            if (!record.LivestockFeed) supply &= ~SupplyPurpose.Feed;

            if (supply != SupplyPurpose.All)
            {
                plan.Notes.Add("supply restricted to " + (supply == SupplyPurpose.None ? "nothing" : SupplyPurposes.Encode(supply)));
            }

            return supply;
        }

        /// <summary>
        /// Quartermaster's remembered prefab names as the game's shared item names. An item whose
        /// prefab is not in this world (another mod's, or Quartermaster's own) cannot be translated and
        /// is reported by name instead of being guessed at.
        /// </summary>
        private static List<string> MapItems(QmRecord record, QmPlan plan)
        {
            var items = new List<string>();
            var forgotten = new HashSet<string>(record.Forgotten ?? new string[0]);

            foreach (var prefabName in record.Remembered ?? new string[0])
            {
                if (string.IsNullOrEmpty(prefabName)) continue;

                if (forgotten.Contains(prefabName))
                {
                    plan.Notes.Add("'" + prefabName + "' was both remembered and forgotten; left out");
                    continue;
                }

                var shared = SharedName(prefabName);
                if (shared == null)
                {
                    plan.Unmapped.Add(prefabName);
                    continue;
                }

                if (!items.Contains(shared)) items.Add(shared);
            }

            foreach (var name in forgotten)
            {
                if (record.Remembered == null) continue;
                if (System.Array.IndexOf(record.Remembered, name) >= 0) continue;
                plan.Notes.Add("'" + name + "' was forgotten, so it was never filed here; nothing to map");
            }

            return items;
        }

        /// <summary>Prefab name to the shared name the rule stores, or null when this world lacks it.</summary>
        public static string SharedName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;

            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop != null && drop.m_itemData != null && drop.m_itemData.m_shared != null)
            {
                return drop.m_itemData.m_shared.m_name;
            }

            // ObjectDB keeps every item prefab, including ones a mod registered after the scene was
            // built. Its name field is the same prefab name the scene uses.
            var database = ObjectDB.instance;
            if (database != null && database.m_items != null)
            {
                foreach (var item in database.m_items)
                {
                    if (item == null) continue;
                    if (!string.Equals(item.name, prefabName, System.StringComparison.OrdinalIgnoreCase)) continue;

                    var itemDrop = item.GetComponent<ItemDrop>();
                    if (itemDrop == null || itemDrop.m_itemData == null || itemDrop.m_itemData.m_shared == null) continue;
                    return itemDrop.m_itemData.m_shared.m_name;
                }
            }

            return null;
        }

        /// <summary>The report line for one container.</summary>
        public string Describe(int index)
        {
            var at = Source.Position;
            var text = new StringBuilder();
            text.Append("migrate container=").Append(index)
                .Append(" prefab=").Append(Source.Prefab)
                .Append(" at=(").Append(at.x.ToString("0.#")).Append(',').Append(at.z.ToString("0.#")).Append(')')
                .Append(" qm=").Append(QmToken(Source.Record))
                .Append(" rule=").Append(Rule.Describe())
                .Append(Rule.SupplyDescribe())
                .Append(" items=").Append(Rule.Items != null ? Rule.Items.Count : 0)
                .Append(" existing='").Append(string.IsNullOrEmpty(Source.Existing) ? "none" : Source.Existing).Append('\'')
                .Append(" live=").Append(Source.Live ? "yes" : "no")
                .Append(" save_items=").Append(Source.Items != null ? Source.Items.Length : 0);

            if (Unmapped.Count > 0) text.Append(" unmapped=").Append(string.Join(",", Unmapped.ToArray()));
            return text.ToString();
        }

        private static string QmToken(QmRecord record)
        {
            if (record == null) return "none";
            if (record.Deposit) return "deposit";
            if (record.Overflow) return "overflow";
            if (record.Remembered != null && record.Remembered.Length > 0) return "list:" + record.Remembered.Length;
            return "none";
        }
    }
}
