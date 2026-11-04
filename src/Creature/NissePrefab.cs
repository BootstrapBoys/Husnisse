using Husnisse.Core;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// Registers the creature. The body is a seam, not a commitment: set <see cref="BodyOverride"/> to
    /// a prefab (vanilla or from an asset bundle) before registration and the nisse wears it instead of
    /// the Greyling clone. Behaviour, faction, health and speeds are applied in code either way, so
    /// re-arting the mod never touches the AI or the save data — the registered name stays constant.
    /// </summary>
    public static class NissePrefab
    {
        public const string PrefabName = "husnisse_nisse";
        private const string BasePrefabName = "Greyling";

        /// <summary>Optional replacement body. Must carry Character, ZNetView, Rigidbody, a capsule
        /// collider and a ZSyncAnimation. Null means the Greyling clone.</summary>
        public static GameObject BodyOverride;

        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;

            var prefab = BodyOverride != null
                ? PrefabManager.Instance.CreateClonedPrefab(PrefabName, BodyOverride)
                : PrefabManager.Instance.CreateClonedPrefab(PrefabName, BasePrefabName);

            if (prefab == null)
            {
                Log.Error("nisse: no body prefab available (" +
                          (BodyOverride != null ? BodyOverride.name : BasePrefabName) + ")");
                return;
            }

            var hostile = prefab.GetComponent<MonsterAI>();
            if (hostile != null) Object.DestroyImmediate(hostile);

            var character = prefab.GetComponent<Character>();
            if (character == null)
            {
                Log.Error("nisse: body has no Character component");
                return;
            }

            character.m_name = "Nisse";
            character.m_faction = Character.Faction.PlayerSpawned;
            character.m_health = 100f;
            character.m_walkSpeed = 2f;
            character.m_runSpeed = 4f;

            prefab.AddComponent<NisseActor>();

            var config = new CreatureConfig { Name = "Nisse" };
            var custom = new CustomCreature(prefab, false, config);
            CreatureManager.Instance.AddCreature(custom);

            _registered = true;
            Log.Info("creature registered as " + PrefabName + " (body=" + prefab.name + ")");
        }
    }
}
