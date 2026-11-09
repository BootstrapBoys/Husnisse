using System.Collections.Generic;
using System.Reflection;
using Husnisse.Core;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>Spawns and finds the one nisse bound to a bowl. Server-side only.</summary>
    public static class NisseFactory
    {
        private static readonly Dictionary<ZDOID, NisseActor> Bound = new Dictionary<ZDOID, NisseActor>();
        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        public static NisseActor EnsureNisse(Bowl.BowlPiece bowl)
        {
            var existing = Find(bowl);
            if (existing != null) return existing;

            var view = bowl.GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null) return null;
            var bowlId = view.GetZDO().m_uid;
            return Spawn(bowl, bowlId);
        }

        /// <summary>Finds the bound nisse without spawning one; safe on clients.</summary>
        public static NisseActor Find(Bowl.BowlPiece bowl)
        {
            if (bowl == null) return null;

            var view = bowl.GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null) return null;
            var bowlId = view.GetZDO().m_uid;

            NisseActor cached;
            if (Bound.TryGetValue(bowlId, out cached) && cached != null) return cached;

            foreach (var actor in Object.FindObjectsByType<NisseActor>(FindObjectsSortMode.None))
            {
                if (actor == null || actor.BowlId != bowlId) continue;
                Bound[bowlId] = actor;
                return actor;
            }

            return null;
        }

        private static NisseActor Spawn(Bowl.BowlPiece bowl, ZDOID bowlId)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(NissePrefab.PrefabName) : null;
            if (prefab == null)
            {
                Log.Warn("nisse: prefab not registered yet");
                return null;
            }

            var position = bowl.transform.position + bowl.transform.forward * 1.5f + Vector3.up * 0.1f;
            var hash = prefab.name.GetStableHashCode();
            var zdo = ZDOMan.instance.CreateNewZDO(position, hash);
            zdo.SetPrefab(hash);
            zdo.Set(ZdoKeys.NisseBowl, bowlId);

            var create = typeof(ZNetScene).GetMethod("CreateObject", Priv);
            var go = create != null ? create.Invoke(ZNetScene.instance, new object[] { zdo }) as GameObject : null;
            var actor = go != null ? go.GetComponent<NisseActor>() : null;
            if (actor == null)
            {
                Log.Error("nisse: spawn failed");
                return null;
            }

            Bound[bowlId] = actor;
            actor.PlaySpawn();
            Log.Info("nisse settled at bowl " + bowlId);
            return actor;
        }

        /// <summary>A bowl without its spirit: the nisse is removed when its bowl is destroyed.</summary>
        public static void Despawn(ZDOID bowlId)
        {
            NisseActor found;
            if (!Bound.TryGetValue(bowlId, out found) || found == null)
            {
                foreach (var actor in Object.FindObjectsByType<NisseActor>(FindObjectsSortMode.None))
                {
                    if (actor != null && actor.BowlId == bowlId) found = actor;
                }
            }

            Bound.Remove(bowlId);
            if (found == null) return;

            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(found.gameObject);
            else Object.Destroy(found.gameObject);
            Log.Info("nisse left with bowl " + bowlId);
        }
    }
}
