using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Localization;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Husnisse.Bowl
{
    /// <summary>
    /// Registers the bowl as a build piece. v1 has no art: the cauldron clone gives a bowl-shaped
    /// prop for free, and the cooking components are stripped so the only interaction is ours.
    /// </summary>
    public static class BowlPrefab
    {
        public const string PrefabName = "husnisse_bowl";
        private const string BasePrefabName = "piece_cauldron";

        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;

            var prefab = PrefabManager.Instance.CreateClonedPrefab(PrefabName, BasePrefabName);
            if (prefab == null)
            {
                Log.Error("bowl: base prefab not found: " + BasePrefabName);
                return;
            }

            // The cauldron is a cooking station; none of that belongs to a porridge bowl.
            Strip<CookingStation>(prefab);
            Strip<CraftingStation>(prefab);

            var container = prefab.GetComponent<Container>();
            if (container == null)
            {
                container = prefab.AddComponent<Container>();
            }

            container.m_name = Loc.T(Loc.BowlName);
            container.m_width = 4;
            container.m_height = 1;
            container.m_privacy = Container.PrivacySetting.Private;
            container.m_checkGuardStone = false;
            container.m_autoDestroyEmpty = false;
            container.m_defaultItems = new DropTable();

            var piece = prefab.GetComponent<Piece>();
            if (piece == null)
            {
                Log.Error("bowl: cloned prefab has no Piece component");
                return;
            }

            piece.m_name = Loc.T(Loc.BowlName);
            piece.m_description = Loc.T(Loc.BowlDesc);
            prefab.AddComponent<BowlPiece>();

            var config = new PieceConfig
            {
                Name = Loc.T(Loc.BowlName),
                Description = Loc.T(Loc.BowlDesc),
                PieceTable = "Hammer",
                Category = "Misc",
                Enabled = true,
            };
            config.AddRequirement("Wood", 5, true);

            // The cauldron demands a forge; a porridge bowl belongs anywhere in the house.
            piece.m_craftingStation = null;

            var custom = new CustomPiece(prefab, false, config);
            PieceManager.Instance.AddPiece(custom);

            Log.Info("bowl fields: category=" + piece.m_category +
                     " station=" + (piece.m_craftingStation != null ? piece.m_craftingStation.name : "none") +
                     " resources=" + (piece.m_resources == null ? 0 : piece.m_resources.Length) +
                     " enabled=" + piece.m_enabled +
                     " table=" + config.PieceTable);

            _registered = true;
            Log.Info("bowl registered as " + PrefabName);
        }

        private static void Strip<T>(GameObject prefab) where T : Component
        {
            var component = prefab.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component);
        }
    }
}
