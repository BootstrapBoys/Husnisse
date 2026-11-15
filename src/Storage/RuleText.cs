using Husnisse.Localization;

namespace Husnisse.Storage
{
    /// <summary>One place turns a rule into words, so the ledger, the container window and the
    /// nisse's teaching messages name a rule the same way.</summary>
    public static class RuleText
    {
        public static string CategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Metals: return Loc.T(Loc.CatMetals);
                case ItemCategory.Wood: return Loc.T(Loc.CatWood);
                case ItemCategory.Food: return Loc.T(Loc.CatFood);
                case ItemCategory.Trophies: return Loc.T(Loc.CatTrophies);
                case ItemCategory.Valuables: return Loc.T(Loc.CatValuables);
                case ItemCategory.Building: return Loc.T(Loc.CatBuilding);
                case ItemCategory.Consumables: return Loc.T(Loc.CatConsumables);
                case ItemCategory.Tools: return Loc.T(Loc.CatTools);
                default: return Loc.T(Loc.RuleNone);
            }
        }

        public static string Describe(ContainerRule rule)
        {
            if (rule == null) return Loc.T(Loc.RuleNone);

            switch (rule.Kind)
            {
                case RuleKind.Category:
                    return CategoryName(rule.Category);
                case RuleKind.Custom:
                    return Loc.T(Loc.RuleCustom) + " (" + (rule.Items != null ? rule.Items.Count : 0) + ")";
                case RuleKind.Overflow:
                    return Loc.T(Loc.RuleOverflow);
                case RuleKind.Deposit:
                    return Loc.T(Loc.RuleDeposit);
                default:
                    return Loc.T(Loc.RuleNone);
            }
        }
    }
}
