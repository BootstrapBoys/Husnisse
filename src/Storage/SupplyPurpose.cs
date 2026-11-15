using System;
using System.Collections.Generic;
using System.Text;

namespace Husnisse.Storage
{
    /// <summary>
    /// Why a container is being raided. A chest supplies four kinds of consumer, and the owner of a
    /// base may switch each one off at the chest: the metals chest a blueprint may spend from is not
    /// automatically the pile the kiln burns, and the food a cooking station takes is not the portion
    /// an animal gets. This is the four-way supply permission Quartermaster has per chest, split by
    /// purpose instead of by a single "supply" flag.
    ///
    /// The bits describe what a chest may **give**, never what it may receive: filing a stack into a
    /// chest is governed by its rule, and an Empty Pockets order still empties the player's pockets
    /// wherever they point.
    /// </summary>
    [Flags]
    public enum SupplyPurpose
    {
        None = 0,

        /// <summary>A craft's shortfall: the 0.5 supply errand to the bench.</summary>
        Crafting = 1,

        /// <summary>A processor's input: ore into a smelter, barley into a windmill, mash into a fermenter.</summary>
        Production = 2,

        /// <summary>A fire's fuel: wood or coal into a fireplace, kiln, smelter or oven.</summary>
        Fuel = 4,

        /// <summary>An animal's portion, laid down for a tameable creature.</summary>
        Feed = 8,

        All = Crafting | Production | Fuel | Feed,
    }

    /// <summary>
    /// The textual tokens the four purposes travel under: the ZDO rule string, the YAML rules file
    /// and the console all spell them the same way, so a value written by one can be read by the
    /// other without a second table.
    /// </summary>
    public static class SupplyPurposes
    {
        public const string AllToken = "all";
        public const string NoneToken = "none";

        public static readonly SupplyPurpose[] Order =
        {
            SupplyPurpose.Crafting,
            SupplyPurpose.Production,
            SupplyPurpose.Fuel,
            SupplyPurpose.Feed,
        };

        public static string Token(SupplyPurpose purpose)
        {
            switch (purpose)
            {
                case SupplyPurpose.Crafting: return "crafting";
                case SupplyPurpose.Production: return "production";
                case SupplyPurpose.Fuel: return "fuel";
                case SupplyPurpose.Feed: return "feed";
                default: return NoneToken;
            }
        }

        /// <summary>The one purpose a token names, or <see cref="SupplyPurpose.None"/>.</summary>
        public static SupplyPurpose FromToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return SupplyPurpose.None;

            switch (token.Trim().ToLowerInvariant())
            {
                case "crafting":
                case "craft":
                    return SupplyPurpose.Crafting;
                case "production":
                case "produce":
                case "process":
                    return SupplyPurpose.Production;
                case "fuel":
                    return SupplyPurpose.Fuel;
                case "feed":
                case "food":
                    return SupplyPurpose.Feed;
                case AllToken:
                    return SupplyPurpose.All;
                default:
                    return SupplyPurpose.None;
            }
        }

        /// <summary>
        /// Reads a comma-separated set. A leading <c>-</c> on a token subtracts from
        /// <see cref="SupplyPurpose.All"/>, so <c>-fuel</c> means "everything but fuel" — the way a
        /// person edits a list they did not want to spell out. An unreadable token is ignored rather
        /// than widening the permission: a typo must never hand out more than the owner asked for.
        /// </summary>
        public static SupplyPurpose Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return SupplyPurpose.All;

            var trimmed = text.Trim();
            if (string.Equals(trimmed, AllToken, StringComparison.OrdinalIgnoreCase)) return SupplyPurpose.All;
            if (string.Equals(trimmed, NoneToken, StringComparison.OrdinalIgnoreCase)) return SupplyPurpose.None;

            var allowed = text.IndexOf('-') >= 0 ? SupplyPurpose.All : SupplyPurpose.None;
            foreach (var raw in trimmed.Split(','))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;

                var subtract = token[0] == '-';
                if (subtract) token = token.Substring(1).Trim();

                var purpose = FromToken(token);
                if (purpose == SupplyPurpose.None) continue;

                allowed = subtract ? allowed & ~purpose : allowed | purpose;
            }
            return allowed;
        }

        /// <summary>The stable encoding for the rule string: <c>all</c>, <c>none</c> or <c>fuel,feed</c>.</summary>
        public static string Encode(SupplyPurpose allowed)
        {
            if (allowed == SupplyPurpose.All) return AllToken;
            if (allowed == SupplyPurpose.None) return NoneToken;

            var text = new StringBuilder();
            for (var i = 0; i < Order.Length; i++)
            {
                if ((allowed & Order[i]) == 0) continue;
                if (text.Length > 0) text.Append(',');
                text.Append(Token(Order[i]));
            }
            return text.ToString();
        }

        /// <summary>How a chest's permissions read in the ledger, the status dump and the log.</summary>
        public static string Describe(SupplyPurpose allowed)
        {
            if (allowed == SupplyPurpose.All) return AllToken;
            if (allowed == SupplyPurpose.None) return NoneToken;

            var parts = new List<string>();
            for (var i = 0; i < Order.Length; i++)
            {
                if ((allowed & Order[i]) != 0) parts.Add(Token(Order[i]));
            }
            return string.Join(",", parts.ToArray());
        }

        /// <summary>Human labels in the container window's order, with the token beside them.</summary>
        public static List<KeyValuePair<SupplyPurpose, string>> Editable()
        {
            var list = new List<KeyValuePair<SupplyPurpose, string>>();
            list.Add(new KeyValuePair<SupplyPurpose, string>(SupplyPurpose.Crafting, "$husnisse_supply_crafting"));
            list.Add(new KeyValuePair<SupplyPurpose, string>(SupplyPurpose.Production, "$husnisse_supply_production"));
            list.Add(new KeyValuePair<SupplyPurpose, string>(SupplyPurpose.Fuel, "$husnisse_supply_fuel"));
            list.Add(new KeyValuePair<SupplyPurpose, string>(SupplyPurpose.Feed, "$husnisse_supply_feed"));
            return list;
        }
    }
}
