using BepInEx.Bootstrap;

namespace Husnisse.Compat
{
    /// <summary>
    /// Startup refusals. A storage mod that runs beside another storage mod on the same chests is how
    /// items vanish, so the guard is a hard stop with a readable reason rather than a warning.
    /// </summary>
    public static class PluginGuards
    {
        public const string QuartermasterGuid = "local.valheim.quartermaster";
        public const string IntegrationsGuid = "com.bootstrapboys.quartermaster_integrations";

        public const string EpicLootGuid = "randyknapp.mods.epicloot";
        public const string EpicLootContainerBridgeGuid = "KlownKiller.EpicLootContainerBridge";
        public const string AaaCraftingGuid = "Azumatt.AzuAntiArthriticCrafting";
        public const string AaaBuildMenuGuid = "Azumatt.AAABuildMenu";
        public const string PlanBuildGuid = "marcopogo.PlanBuild";
        public const string MluGuid = "shudnal.MyLittleUI";
        public const string ExtraSlotsGuid = "shudnal.ExtraSlots";
        public const string AdventureBackpacksGuid = "vapok.mods.adventurebackpacks";

        public static bool QuartermasterInstalled()
        {
            return Installed(QuartermasterGuid);
        }

        /// <summary>
        /// Quartermaster_Integrations answers the same storage questions Husnisse does. While it is
        /// loaded it owns the consumer integrations, so Husnisse registers none and reports
        /// `provider=qi` instead of counting the same chest twice.
        /// </summary>
        public static bool IntegrationsInstalled()
        {
            return Installed(IntegrationsGuid);
        }

        public static bool Installed(string guid)
        {
            return !string.IsNullOrEmpty(guid) && Chainloader.PluginInfos.ContainsKey(guid);
        }

        /// <summary>One line for diagnostics: which consumer mods are in this install.</summary>
        public static string Describe()
        {
            return "quartermaster=" + (QuartermasterInstalled() ? "installed" : "absent") +
                   " integrations=" + (IntegrationsInstalled() ? "installed" : "absent") +
                   " epicloot=" + (Installed(EpicLootGuid) ? "installed" : "absent") +
                   " epicloot_bridge=" + (Installed(EpicLootContainerBridgeGuid) ? "installed" : "absent") +
                   " aaacrafting=" + (Installed(AaaCraftingGuid) ? "installed" : "absent") +
                   " aaabuildmenu=" + (Installed(AaaBuildMenuGuid) ? "installed" : "absent") +
                   " planbuild=" + (Installed(PlanBuildGuid) ? "installed" : "absent") +
                   " mylittleui=" + (Installed(MluGuid) ? "installed" : "absent") +
                   " extraslots=" + (Installed(ExtraSlotsGuid) ? "installed" : "absent") +
                   " adventurebackpacks=" + (Installed(AdventureBackpacksGuid) ? "installed" : "absent");
        }

        public static string RefusalReason()
        {
            if (QuartermasterInstalled())
            {
                return "Quartermaster is installed. Two storage systems managing the same chests " +
                       "will lose items, so Husnisse refuses to run. Remove Quartermaster and restart.";
            }

            return null;
        }
    }
}
