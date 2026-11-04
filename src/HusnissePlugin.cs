using BepInEx;
using Husnisse.Config;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Bowl;
using Husnisse.Creature;
using Husnisse.Rounds;
using Jotunn.Managers;

namespace Husnisse
{
    /// <summary>
    /// Entry point. Wires the components together, refuses to run beside Quartermaster, and registers
    /// content only once Jotunn says the vanilla prefabs are available.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("shudnal.ConditionalConfigSync", BepInDependency.DependencyFlags.HardDependency)]
    public class HusnissePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.bootstrapboys.husnisse";
        public const string PluginName = "Husnisse";
        public const string PluginVersion = "0.1.0";

        internal static bool Running;

        private static bool _contentRegistered;

        private void Awake()
        {
            Log.Init(Logger);
            Log.Info(PluginName + " " + PluginVersion + " starting");

            var refusal = PluginGuards.RefusalReason();
            if (refusal != null)
            {
                Log.Error(refusal);
                return;
            }

            HusnisseConfig.Bind(Config, PluginGUID);
            Log.DebugEnabled = HusnisseConfig.DebugLogging.Value;

            if (!HusnisseConfig.Enabled.Value)
            {
                Log.Warn("disabled by configuration");
                return;
            }

            PrefabManager.OnVanillaPrefabsAvailable += RegisterContent;
            RoundScheduler.Attach(gameObject);
            Running = true;

            Log.Info("core ready; waiting for vanilla prefabs");
        }

        private static void RegisterContent()
        {
            if (_contentRegistered) return;
            _contentRegistered = true;
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterContent;

            BowlPrefab.Register();
            NissePrefab.Register();

            Log.Info("content registered");
        }
    }
}
