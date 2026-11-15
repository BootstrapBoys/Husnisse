using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Contract.Integrations
{
    /// <summary>
    /// PlanBuild's blueprint cost, paid from the claim.
    ///
    /// PlanBuild asks one question — "which inventories belong to this player?" — through
    /// `PlanPiece.GetInventories`, and uses that list for both halves of its work: the colour of a
    /// blueprint's cost and the materials it takes when the plan is fed. The take half of its
    /// per-inventory contract is `void`, so an entry in that list which can count but not remove
    /// does not fail a placement; it makes the blueprint free. 0.4 therefore refused to add one, and
    /// 0.5 does not add one either: a claim chest cannot be debited by a client's synchronous call.
    ///
    /// What it does instead is the shape the crafting window gets: when the plan asks what the
    /// player has, the shortfall is read from the plan's own piece, and if the claim can cover it the
    /// nisse is sent for it. The inventory list is left exactly as PlanBuild built it — the player's
    /// own — so the plan's clicks pay from the pockets, one unit at a time, and the plan completes
    /// when the load has arrived. Nothing is added to a list whose spend we could not honour.
    /// </summary>
    public static class PlanBuildSupply
    {
        /// <summary>One ask per plan per this many seconds: GetInventories runs on every hover.</summary>
        private const float AskInterval = 8f;

        private static bool _ready;
        private static FieldInfo _originalPiece;
        private static readonly Dictionary<int, float> Asked = new Dictionary<int, float>();

        public static string State { get; private set; } = "absent";

        public static bool IsReady
        {
            get { return _ready && Enabled(); }
        }

        public static void Bind(Harmony harmony)
        {
            State = "absent";

            if (!PluginGuards.Installed(PluginGuards.PlanBuildGuid)) return;

            if (!StorageContract.CanConsume)
            {
                State = "off";
                Log.Info("[Contract] PlanBuild is installed; craft from the claim is off, so plan costs stay " +
                         "pocket-only.");
                return;
            }

            try
            {
                var info = Chainloader.PluginInfos[PluginGuards.PlanBuildGuid];
                var assembly = info.Instance.GetType().Assembly;
                var planPiece = assembly.GetType("PlanBuild.Plans.PlanPiece") ?? FindPlanPiece(assembly);

                var getInventories = planPiece == null
                    ? null
                    : AccessTools.Method(planPiece, "GetInventories", new[] { typeof(Humanoid) });

                _originalPiece = planPiece == null ? null : AccessTools.Field(planPiece, "originalPiece");

                if (getInventories == null || _originalPiece == null)
                {
                    State = "types-missing";
                    Log.Warn("[Contract] PlanBuild is installed but PlanPiece.GetInventories/originalPiece was not " +
                             "found; plan costs stay pocket-only.");
                    return;
                }

                harmony.Patch(getInventories,
                    prefix: new HarmonyMethod(typeof(PlanBuildSupply), nameof(GetInventoriesPrefix)));

                _ready = true;
                State = "bound";
                Log.Info("[Contract] PlanBuild costs fetch from the claim: a plan whose materials the pockets lack " +
                         "asks the nisse for the shortfall, and the plan's own clicks pay from the pockets.");
            }
            catch (Exception ex)
            {
                State = "error";
                Log.Warn("[Contract] PlanBuild supply binding failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Namespace-independent fallback: PlanBuild's type layout has moved before, so match on the
        /// shape (a PlanPiece type carrying an IInventory interface and an originalPiece field).
        /// </summary>
        private static Type FindPlanPiece(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            foreach (var type in types)
            {
                if (type != null &&
                    type.Name == "PlanPiece" &&
                    type.GetNestedType("IInventory", BindingFlags.Public) != null &&
                    type.GetField("originalPiece") != null)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Runs before PlanBuild builds its list. Only the local player's plan pieces are considered,
        /// and the result is never touched: the claim supplies materials to the player, not entries to
        /// a list whose take half we could not honour.
        /// </summary>
        public static void GetInventoriesPrefix(object __instance, object[] __args)
        {
            if (!IsReady || __instance == null || __args == null || __args.Length == 0) return;

            var player = __args[0] as Player;
            if (player == null || !ReferenceEquals(player, Player.m_localPlayer)) return;

            try
            {
                var piece = _originalPiece.GetValue(__instance) as Piece;
                var resources = piece != null ? piece.m_resources : null;
                if (resources == null || resources.Length == 0) return;

                var items = SupplyService.Shortfall(resources, 1, 1, player);
                if (items.Count == 0) return;

                var claim = SupplyService.ClaimFor(player);
                if (claim == null || !SupplyService.CanCover(player, items)) return;

                var id = __instance.GetHashCode();
                float asked;
                if (Asked.TryGetValue(id, out asked) && Time.time - asked < AskInterval) return;

                if (Asked.Count > 64) Asked.Clear();   // a new plan pile, not a leak
                Asked[id] = Time.time;

                long token;
                var answer = SupplyService.Request(player, claim, items, out token);
                if (!answer.HasValue || answer.Value == Net.FetchResult.Accepted)
                {
                    player.Message(MessageHud.MessageType.Center,
                                   Localization.Loc.T(Localization.Loc.SupplyFetching));
                    Log.Debug("Contract", "[PlanBuild] plan cost fetch asked: " + items.Count +
                                          " item type(s) token=" + token);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Contract", "[PlanBuild] plan cost probe failed: " + ex.Message);
            }
        }

        private static bool Enabled()
        {
            return HusnisseConfig.CraftSupply != null && HusnisseConfig.CraftSupply.Value;
        }
    }
}
