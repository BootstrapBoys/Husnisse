using HarmonyLib;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Contract.Integrations;
using Husnisse.Core;

namespace Husnisse.Contract
{
    /// <summary>
    /// Binds the read-only consumer integrations once, after config and after the optional plugins
    /// have had their say (they are declared soft dependencies, so load order is deterministic).
    ///
    /// Two rules decide the shape:
    ///  - Quartermaster_Integrations answers the same questions. While it is loaded it owns them, so
    ///    Husnisse registers nothing and reports `provider=qi` — two providers would double every
    ///    number, and a user should be able to remove one package at a time.
    ///  - Every integration is optional and absent-safe. Binding logs one line per surface, so a
    ///    support log says which number came from where without a debug switch.
    /// </summary>
    public static class IntegrationHost
    {
        private static bool _rows;
        private static bool _aaa;
        private static bool _buildMenu;

        public static string State { get; private set; } = "not-bound";

        public static bool ReadoutsRegistered
        {
            get { return _rows || _aaa || _buildMenu; }
        }

        public static void Bind(Harmony harmony)
        {
            State = "off";

            if (HusnisseConfig.Enabled == null || !HusnisseConfig.Enabled.Value)
            {
                // A disabled mod patches nothing: AAA's batch ceiling and the requirement row are
                // other mods' surfaces, and a disabled Husnisse must leave them exactly as it found
                // them.
                return;
            }

            if (HusnisseConfig.RequirementReadouts == null || !HusnisseConfig.RequirementReadouts.Value)
            {
                Log.Info("[Contract] requirement readouts are off by configuration; the contract still answers " +
                         "Count and CanAfford, and no crafting UI is touched.");
                return;
            }

            if (PluginGuards.IntegrationsInstalled())
            {
                State = "qi";
                Log.Info("[Contract] Quartermaster_Integrations is loaded and owns the consumer integrations; " +
                         "Husnisse registers none, so one chest is never counted twice. provider=qi. " +
                         "Remove the integrations package to let Husnisse answer instead.");
                return;
            }

            // MyLittleUI's own pocket-only row number goes first, so the row below has one writer.
            MluiReadout.Suppress();

            _rows = RequirementRowReadout.Bind(harmony);
            _aaa = AaaCraftingReadout.Bind(harmony);
            _buildMenu = AaaBuildMenuReadout.Bind(harmony);

            // These two are detected and explained rather than registered: both spend through the
            // list they read, and a read-only entry in those lists loses items (see their docs).
            EpicLootProvider.Bind();
            PlanBuildBridge.Bind();

            State = ReadoutsRegistered ? "native" : "none";
            Log.Info("[Contract] readouts " + Describe());
        }

        /// <summary>One line for `husnisse_status` and the startup log.</summary>
        public static string Describe()
        {
            return "state=" + State +
                   " rows=" + (_rows ? "pocket+claim" : "vanilla") +
                   " aaacrafting=" + AaaCraftingReadout.State +
                   " aaabuildmenu=" + AaaBuildMenuReadout.State +
                   " mylittleui=" + MluiReadout.State +
                   " epicloot=" + EpicLootProvider.State +
                   " planbuild=" + PlanBuildBridge.State;
        }
    }
}
