using BepInEx;
using HarmonyLib;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Contract;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Bowl;
using Husnisse.Creature;
using Husnisse.Localization;
using Husnisse.Flow;
using Husnisse.Net;
using Jotunn.Managers;
using UnityEngine;

namespace Husnisse
{
    /// <summary>
    /// Entry point. Wires the components together, refuses to run beside Quartermaster, and registers
    /// content only once Jotunn says the vanilla prefabs are available.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("_shudnal.ConditionalConfigSync", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.ValheimModding.YamlDotNetDetector", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AzuAntiArthriticCrafting", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Azumatt.AAABuildMenu", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("marcopogo.PlanBuild", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("shudnal.MyLittleUI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("shudnal.ExtraSlots", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("vapok.mods.adventurebackpacks", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.bootstrapboys.quartermaster_integrations", BepInDependency.DependencyFlags.SoftDependency)]
    public class HusnissePlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.bootstrapboys.husnisse";
        public const string PluginName = "Husnisse";
        public const string PluginVersion = "0.4.0";

        internal static bool Running;

        private static bool _contentRegistered;

        private void Awake()
        {
            Log.Init(Logger);
            DiagnosticsFile.Init(PluginName, PluginVersion);
            Log.Info(PluginName + " " + PluginVersion + " starting");

            var refusal = PluginGuards.RefusalReason();
            if (refusal != null)
            {
                Log.Error(refusal);
                return;
            }

            HusnisseConfig.Bind(Config, PluginGUID);
            Log.DebugEnabled = HusnisseConfig.DebugLogging.Value;

            Loc.Initialize();

            var harmony = new Harmony(PluginGUID);
            harmony.PatchAll();

            // Read-only consumer integrations (0.4). They bind after config and after the optional
            // plugins have loaded — the soft dependencies above make that order deterministic — and
            // every one of them is absent-safe.
            IntegrationHost.Bind(harmony);

            RegisterStatusCommand();
            RegisterPreviewCommand();
            RegisterPrefabCommand();
            RegisterContractCommand();
            Commands.RuleCommand.Register();
            Commands.CategoryCommand.Register();
            Commands.BenchCommand.Register();
            Commands.LedgerCommand.Register();
            Commands.RulesCommand.Register();

            if (!HusnisseConfig.Enabled.Value)
            {
                Log.Warn("disabled by configuration");
                return;
            }

            PrefabManager.OnVanillaPrefabsAvailable += RegisterContent;
            FlowScheduler.Attach(gameObject);
            ClaimMonitor.Attach(gameObject);
            Running = true;

            Log.Info("core ready; waiting for vanilla prefabs");
        }

        private static void RegisterStatusCommand()
        {
            new Terminal.ConsoleCommand(
                "husnisse_status",
                "Print Husnisse claims, bowls and container counts.",
                delegate(Terminal.ConsoleEventArgs args)
                {
                    foreach (var line in Diagnostics.Lines())
                    {
                        args.Context?.AddString(line);
                        DiagnosticsFile.Line(line);
                    }
                });
        }

        private static void RegisterPreviewCommand()
        {
            new Terminal.ConsoleCommand(
                "husnisse_preview",
                "Preview the claim a bowl would get: husnisse_preview [x z] [radius] [player].",
                delegate(Terminal.ConsoleEventArgs args)
                {
                    var player = Player.m_localPlayer;
                    var playerId = player != null ? player.GetPlayerID() : 0L;
                    var position = player != null ? player.transform.position : Vector3.zero;

                    var index = 1;
                    float x, z;
                    if (args.Length >= 3 && float.TryParse(args[1], out x) && float.TryParse(args[2], out z))
                    {
                        position = new Vector3(x, position.y, z);
                        index = 3;
                    }

                    var radius = 0f;
                    if (args.Length > index) float.TryParse(args[index], out radius);
                    if (args.Length > index + 1) long.TryParse(args[index + 1], out playerId);

                    var preview = ClaimPreview.Evaluate(position, playerId, radius);
                    args.Context?.AddString(preview.ToString());
                    DiagnosticsFile.Line(preview.ToString());

                    if (player != null && ZNet.instance != null && !ZNet.instance.IsDedicated())
                    {
                        ClaimRing.ShowFor(preview.Center, preview.Radius,
                            preview.Placeable ? new Color(0.55f, 0.95f, 0.55f, 0.85f)
                                              : new Color(0.95f, 0.35f, 0.30f, 0.90f), 8f);
                    }
                });
        }

        private static void RegisterPrefabCommand()
        {
            new Terminal.ConsoleCommand(
                "husnisse_prefabs",
                "Write every loaded container prefab to BepInEx/BSBLogs/Husnisse-prefabs.txt.",
                delegate(Terminal.ConsoleEventArgs args)
                {
                    var path = PrefabExport.Dump();
                    args.Context?.AddString(path != null ? "wrote " + path : "no ZNetScene yet");
                });
        }

        /// <summary>
        /// The contract, asked by hand. Same lines `husnisse_status` and the harness read, so a player
        /// and a gate can never see two different answers.
        /// </summary>
        private static void RegisterContractCommand()
        {
            new Terminal.ConsoleCommand(
                "husnisse_contract",
                "Print the storage contract's answer: husnisse_contract [item] — scope, sources and the count.",
                delegate(Terminal.ConsoleEventArgs args)
                {
                    var item = args.Length > 1 ? args[1] : null;
                    foreach (var line in StorageContract.Report(item))
                    {
                        args.Context?.AddString(line);
                        DiagnosticsFile.Line(line);
                    }
                });
        }

        private void OnApplicationQuit()
        {
            if (Running) DiagnosticsFile.Dump("shutdown");
        }

        private static void RegisterContent()
        {
            if (_contentRegistered) return;
            _contentRegistered = true;
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterContent;

            BowlPrefab.Register();
            NissePrefab.Register();
            OrderRpc.Register();
            RuleRpc.Register();

            Log.Info("content registered");
        }
    }
}
