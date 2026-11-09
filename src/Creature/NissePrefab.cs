using Husnisse.Core;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// Registers the creature. A Greyling clone is the cheapest humanoid in the game; its hostile AI is
    /// removed and replaced with <see cref="NisseActor"/>. For custom art, set <see cref="BodyOverride"/>
    /// to a prefab before registration (or change <see cref="BasePrefabName"/>); the registered prefab
    /// name stays constant, so save data survives the swap.
    /// </summary>
    public static class NissePrefab
    {
        public const string PrefabName = "husnisse_nisse";
        private const string BasePrefabName = "Greyling";

        /// <summary>Optional replacement body, used by the custom-model work. Must carry Character,
        /// ZNetView, Rigidbody, a capsule collider and a ZSyncAnimation. Null means the Greyling clone.</summary>
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
            character.m_faction = Character.Faction.TrainingDummy;
            character.m_health = 1000f;
            character.m_walkSpeed = 2f;
            character.m_runSpeed = 4f;

            prefab.AddComponent<NisseActor>();
            prefab.AddComponent<NisseHands>();

            var config = new CreatureConfig { Name = "Nisse" };
            var custom = new CustomCreature(prefab, false, config);
            CreatureManager.Instance.AddCreature(custom);

            _registered = true;
            Log.Info("creature registered as " + PrefabName + " (body=" + prefab.name + ")");
        }
    }
}
