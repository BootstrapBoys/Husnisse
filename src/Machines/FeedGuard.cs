using System.Collections.Generic;
using Husnisse.Core;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Machines
{
    /// <summary>
    /// The drops the nisse has set down on purpose. A portion for an animal is real food on the
    /// ground, and the claim's own pickup pass would otherwise carry it straight back to a chest
    /// before the creature reached it — the nisse would be feeding the chest, not the beast.
    ///
    /// A reservation is a drop's instance id plus an expiry, and the same fact is written to the
    /// drop's ZDO so a server restart inside the window still leaves the meal alone. Anything that
    /// is not reserved is picked up exactly as before.
    /// </summary>
    public static class FeedGuard
    {
        /// <summary>Long enough for a slow creature to walk to its food, short enough that a lost
        /// portion is collectable again within one play session.</summary>
        public const float Lifetime = 120f;

        private static readonly Dictionary<int, float> Reserved = new Dictionary<int, float>();

        public static void Reserve(ItemDrop drop)
        {
            if (drop == null) return;

            Reserved[drop.GetInstanceID()] = Time.time + Lifetime;

            var view = drop.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            if (zdo != null) zdo.Set(ZdoKeys.AnimalPortion, true);

            Log.Debug("Machines", "portion reserved at " + Machine.Where(drop.transform.position));
        }

        public static bool IsReserved(ItemDrop drop)
        {
            if (drop == null) return false;

            var id = drop.GetInstanceID();
            float expiry;
            if (Reserved.TryGetValue(id, out expiry))
            {
                if (expiry > Time.time) return true;
                Reserved.Remove(id);
            }

            var view = drop.GetComponent<ZNetView>();
            var zdo = view != null ? view.GetZDO() : null;
            return zdo != null && zdo.GetBool(ZdoKeys.AnimalPortion, false);
        }

        /// <summary>Called when the drop is gone: a destroyed portion must not leave its id behind.</summary>
        public static void Forget(ItemDrop drop)
        {
            if (drop != null) Reserved.Remove(drop.GetInstanceID());
        }

        public static int Count
        {
            get
            {
                Prune();
                return Reserved.Count;
            }
        }

        private static void Prune()
        {
            if (Reserved.Count == 0) return;

            var now = Time.time;
            List<int> stale = null;
            foreach (var pair in Reserved)
            {
                if (pair.Value > now) continue;
                if (stale == null) stale = new List<int>();
                stale.Add(pair.Key);
            }

            if (stale == null) return;
            for (var i = 0; i < stale.Count; i++) Reserved.Remove(stale[i]);
        }

        public static void ForgetAll()
        {
            Reserved.Clear();
        }
    }
}
