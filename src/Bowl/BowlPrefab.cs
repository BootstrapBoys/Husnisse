using Husnisse.Config;
using Husnisse.Core;
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
            Strip<Container>(prefab);

            var piece = prefab.GetComponent<Piece>();
            if (piece == null)
            {
                Log.Error("bowl: cloned prefab has no Piece component");
                return;
            }

            piece.m_name = "Porridge Bowl";
            piece.m_description = "A nisse's home. Keep it fed and it keeps your base.";
            prefab.AddComponent<BowlPiece>();

            var config = new PieceConfig
            {
                Name = "Porridge Bowl",
                Description = "A nisse's home. Keep it fed and it keeps your base.",
                PieceTable = "Hammer",
                Category = "Misc",
                Enabled = true,
            };
            config.AddRequirement("Wood", 5, true);

            var custom = new CustomPiece(prefab, false, config);
            PieceManager.Instance.AddPiece(custom);

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
