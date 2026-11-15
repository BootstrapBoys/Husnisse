using System;
using System.Collections.Generic;
using System.Text;
using Husnisse.Core;

namespace Husnisse.Storage
{
    /// <summary>What a container accepts. Overflow and Deposit are roles, not lists.</summary>
    public enum RuleKind : byte
    {
        None = 0,
        Category = 1,
        Custom = 2,
        Overflow = 3,
        Deposit = 4,
    }

    /// <summary>
    /// The survival presets the UI offers. Each one expands to Valheim item types in the routing
    /// layer (0.3b); this file only carries the choice, so the encoding stays stable when the
    /// expansion changes.
    /// </summary>
    public enum ItemCategory : byte
    {
        None = 0,
        Metals = 1,
        Wood = 2,
        Food = 3,
        Trophies = 4,
        Valuables = 5,
        Building = 6,
        Consumables = 7,
        Tools = 8,
    }

    /// <summary>
    /// One container's rule, stored as a single versioned string in its ZDO.
    ///
    /// One key rather than several because the fields are one decision: a half-written rule (a
    /// category with no priority, a custom list with no owner) would be readable by the router and
    /// wrong. The server writes the whole value in one Set, so a peer never sees the halves.
    ///
    /// Format: <c>v1;k=cat;c=metals;p=1;i=</c>. Unknown fields and unknown versions decode to
    /// None, so a future encoder can add fields without an older build inventing a rule.
    /// </summary>
    public sealed class ContainerRule
    {
        public const string Version = "v1";

        public RuleKind Kind = RuleKind.None;
        public ItemCategory Category = ItemCategory.None;

        /// <summary>Higher fills first among containers that accept the same item. 0 is the default.</summary>
        public int Priority;

        /// <summary>Prefab item names for a custom list. Stored without stacks, as the UI shows them.</summary>
        public List<string> Items = new List<string>();

        public bool IsNone
        {
            get { return Kind == RuleKind.None; }
        }

        public static ContainerRule None()
        {
            return new ContainerRule();
        }

        public ContainerRule Clone()
        {
            var copy = new ContainerRule { Kind = Kind, Category = Category, Priority = Priority };
            if (Items != null) copy.Items.AddRange(Items);
            return copy;
        }

        /// <summary>The token the ledger and diagnostics print: cat:metals, custom:3, overflow, deposit, none.</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case RuleKind.Category:
                    return "cat:" + CategoryToken(Category);
                case RuleKind.Custom:
                    return "custom:" + (Items != null ? Items.Count : 0);
                case RuleKind.Overflow:
                    return "overflow";
                case RuleKind.Deposit:
                    return "deposit";
                default:
                    return "none";
            }
        }

        /// <summary>
        /// The round-trippable shape: what <see cref="ParseShorthand"/> reads back, so an export can
        /// be imported. Unlike <see cref="Describe"/> (display: `custom:3`), a custom list is spelled
        /// out, and the priority rides as `@n`.
        /// </summary>
        public string ToShorthand()
        {
            var text = new StringBuilder();
            switch (Kind)
            {
                case RuleKind.Category:
                    text.Append("cat:").Append(CategoryToken(Category));
                    break;
                case RuleKind.Custom:
                    text.Append("custom:");
                    for (var i = 0; i < (Items != null ? Items.Count : 0); i++)
                    {
                        if (i > 0) text.Append(',');
                        text.Append(Items[i]);
                    }
                    break;
                case RuleKind.Overflow:
                    text.Append("overflow");
                    break;
                case RuleKind.Deposit:
                    text.Append("deposit");
                    break;
                default:
                    return "none";
            }

            if (Priority != 0) text.Append('@').Append(Priority);
            return text.ToString();
        }

        public bool SameAs(ContainerRule other)
        {
            if (other == null) return false;
            if (Kind != other.Kind || Category != other.Category || Priority != other.Priority) return false;
            var mine = Items ?? new List<string>();
            var theirs = other.Items ?? new List<string>();
            if (mine.Count != theirs.Count) return false;
            for (var i = 0; i < mine.Count; i++)
            {
                if (!string.Equals(mine[i], theirs[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }

        public string Encode()
        {
            if (Kind == RuleKind.None) return Version + ";k=none";

            var text = new StringBuilder(Version);
            text.Append(";k=").Append(KindToken(Kind));
            if (Kind == RuleKind.Category) text.Append(";c=").Append(CategoryToken(Category));
            if (Priority != 0) text.Append(";p=").Append(Priority);
            if (Kind == RuleKind.Custom && Items != null && Items.Count > 0)
            {
                text.Append(";i=");
                for (var i = 0; i < Items.Count; i++)
                {
                    if (i > 0) text.Append(',');
                    text.Append(Uri.EscapeDataString(Items[i] ?? string.Empty));
                }
            }
            return text.ToString();
        }

        /// <summary>Never throws: a malformed or newer value is an empty rule, not a broken base.</summary>
        public static ContainerRule Decode(string encoded)
        {
            var rule = new ContainerRule();
            if (string.IsNullOrEmpty(encoded)) return rule;

            string[] parts;
            try
            {
                parts = encoded.Split(';');
            }
            catch (Exception)
            {
                return rule;
            }

            if (parts.Length == 0 || !string.Equals(parts[0], Version, StringComparison.Ordinal))
            {
                Log.Debug("Rules", "rule encoding not understood, treating as none: " + encoded);
                return rule;
            }

            for (var i = 1; i < parts.Length; i++)
            {
                var pair = parts[i];
                if (string.IsNullOrEmpty(pair)) continue;
                var split = pair.IndexOf('=');
                if (split <= 0) continue;
                var key = pair.Substring(0, split);
                var value = pair.Substring(split + 1);

                if (key == "k")
                {
                    rule.Kind = KindFromToken(value);
                }
                else if (key == "c")
                {
                    rule.Category = CategoryFromToken(value);
                }
                else if (key == "p")
                {
                    int priority;
                    if (int.TryParse(value, out priority)) rule.Priority = priority;
                }
                else if (key == "i")
                {
                    rule.Items.Clear();
                    if (value.Length > 0)
                    {
                        foreach (var escaped in value.Split(','))
                        {
                            try
                            {
                                rule.Items.Add(Uri.UnescapeDataString(escaped));
                            }
                            catch (Exception)
                            {
                                // A half-escaped name is dropped, not guessed at.
                            }
                        }
                    }
                }
            }

            if (rule.Kind == RuleKind.Category && rule.Category == ItemCategory.None) rule.Kind = RuleKind.None;
            rule.Sanitise();
            if (rule.Kind == RuleKind.Custom && rule.Items.Count == 0) rule.Kind = RuleKind.None;
            return rule;
        }

        /// <summary>
        /// The console/YAML-friendly shorthand the admin command and scenario scripts type:
        /// <c>none</c>, <c>overflow</c>, <c>deposit</c>, <c>cat:metals</c>, <c>custom:Coal,Wood</c>,
        /// each optionally followed by <c>@priority</c> (<c>cat:metals@2</c>).
        /// Returns null when the token is not a rule at all, so a typo writes nothing.
        /// </summary>
        public static ContainerRule ParseShorthand(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            token = token.Trim();

            var priority = 0;
            var at = token.LastIndexOf('@');
            if (at > 0 && at < token.Length - 1)
            {
                if (!int.TryParse(token.Substring(at + 1), out priority)) return null;
                token = token.Substring(0, at).Trim();
            }

            var rule = ParseKind(token);
            if (rule != null) rule.Priority = priority;
            return rule;
        }

        private static ContainerRule ParseKind(string token)
        {
            if (string.Equals(token, "none", StringComparison.OrdinalIgnoreCase)) return None();
            if (string.Equals(token, "overflow", StringComparison.OrdinalIgnoreCase))
                return new ContainerRule { Kind = RuleKind.Overflow };
            if (string.Equals(token, "deposit", StringComparison.OrdinalIgnoreCase))
                return new ContainerRule { Kind = RuleKind.Deposit };

            if (token.StartsWith("cat:", StringComparison.OrdinalIgnoreCase))
            {
                var category = CategoryFromToken(token.Substring(4).Trim().ToLowerInvariant());
                return category == ItemCategory.None ? null : new ContainerRule { Kind = RuleKind.Category, Category = category };
            }

            if (token.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
            {
                var rule = new ContainerRule { Kind = RuleKind.Custom };
                foreach (var name in token.Substring(7).Split(','))
                {
                    if (name.Trim().Length > 0) rule.Items.Add(name.Trim());
                }
                rule.Sanitise();
                return rule.Items.Count > 0 ? rule : null;
            }

            return null;
        }

        /// <summary>
        /// What a saved list may cost the ZDO: a chest holds 32 stacks, and a list captured from
        /// contents is at most that, so 64 names is generous; past that a client is writing noise.
        /// Duplicates collapse, blank names go, and a name longer than any real item is truncated
        /// rather than stored. Applied on decode as well as on capture, because the value arrives
        /// from the network.
        /// </summary>
        public const int MaxItems = 64;
        private const int MaxNameLength = 64;

        public void Sanitise()
        {
            if (Items == null)
            {
                Items = new List<string>();
                return;
            }

            var kept = new List<string>(Math.Min(Items.Count, MaxItems));
            foreach (var raw in Items)
            {
                if (kept.Count >= MaxItems) break;
                var name = (raw ?? string.Empty).Trim();
                if (name.Length == 0) continue;
                if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
                if (kept.Contains(name)) continue;
                kept.Add(name);
            }

            Items = kept;
        }


        public static string KindToken(RuleKind kind)
        {
            switch (kind)
            {
                case RuleKind.Category: return "cat";
                case RuleKind.Custom: return "custom";
                case RuleKind.Overflow: return "overflow";
                case RuleKind.Deposit: return "deposit";
                default: return "none";
            }
        }

        public static RuleKind KindFromToken(string token)
        {
            switch (token)
            {
                case "cat": return RuleKind.Category;
                case "custom": return RuleKind.Custom;
                case "overflow": return RuleKind.Overflow;
                case "deposit": return RuleKind.Deposit;
                default: return RuleKind.None;
            }
        }

        public static string CategoryToken(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Metals: return "metals";
                case ItemCategory.Wood: return "wood";
                case ItemCategory.Food: return "food";
                case ItemCategory.Trophies: return "trophies";
                case ItemCategory.Valuables: return "valuables";
                case ItemCategory.Building: return "building";
                case ItemCategory.Consumables: return "consumables";
                case ItemCategory.Tools: return "tools";
                default: return "none";
            }
        }

        public static ItemCategory CategoryFromToken(string token)
        {
            switch (token)
            {
                case "metals": return ItemCategory.Metals;
                case "wood": return ItemCategory.Wood;
                case "food": return ItemCategory.Food;
                case "trophies": return ItemCategory.Trophies;
                case "valuables": return ItemCategory.Valuables;
                case "building": return ItemCategory.Building;
                case "consumables": return ItemCategory.Consumables;
                case "tools": return ItemCategory.Tools;
                default: return ItemCategory.None;
            }
        }

        /// <summary>The eight presets in UI order. Kept here so the UI and the router agree.</summary>
        public static readonly ItemCategory[] Presets =
        {
            ItemCategory.Metals,
            ItemCategory.Wood,
            ItemCategory.Food,
            ItemCategory.Trophies,
            ItemCategory.Valuables,
            ItemCategory.Building,
            ItemCategory.Consumables,
            ItemCategory.Tools,
        };
    }
}
