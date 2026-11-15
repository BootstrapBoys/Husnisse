using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Compat
{
    /// <summary>One enabled WardIsLove ward, as the claim layer needs it.</summary>
    public struct WardInfo
    {
        public ZDOID Id;
        public Vector3 Center;
        public float Radius;
        public long OwnerId;

        public bool Contains(Vector3 point)
        {
            var dx = point.x - Center.x;
            var dz = point.z - Center.z;
            return dx * dx + dz * dz < Radius * Radius;
        }
    }

    /// <summary>
    /// Ward authority, reached by reflection so WardIsLove stays a soft dependency.
    /// Ward geometry comes from WardMonoscript.m_wardInstances (the list WardIsLove itself keeps;
    /// private areas are replaced by Thorward, so PrivateArea.m_allAreas does not see them), with the
    /// radius from WardMonoscriptExt.GetWardRadius, the owner from the ward's creator, and permits from
    /// WardMonoscript.permissionsManager.IsPermitted. CustomCheck.CheckAccess is deliberately not used:
    /// it stops at the first ward containing the point, which is not necessarily the ward the bowl chose.
    /// </summary>
    public static class WardIsLoveCompat
    {
        public const string PluginGuid = "Azumatt.WardIsLove";

        private static Type _wardType;
        private static FieldInfo _wardInstances;
        private static FieldInfo _permissionsField;
        private static MethodInfo _isEnabled;
        private static MethodInfo _getWardRadius;
        private static MethodInfo _permits;
        private static FieldInfo _nviewField;
        private static bool _bound;
        private static float _lastAttempt = -10f;
        private static float _lastWarn;
        private static readonly System.Collections.Generic.List<WardInfo> _cache = new System.Collections.Generic.List<WardInfo>();
        private static int _cacheFrame = -1;

        public static bool Installed
        {
            get { return Chainloader.PluginInfos.ContainsKey(PluginGuid); }
        }

        /// <summary>True once the ward API is reachable. A false here means claims fall back to native.</summary>
        public static bool Bound
        {
            get { return _bound; }
        }

        /// <summary>Binds once, and keeps trying if WardIsLove has not finished loading yet.</summary>
        private static bool Resolve()
        {
            if (_bound) return true;
            if (Time.realtimeSinceStartup - _lastAttempt < 1f) return false;
            _lastAttempt = Time.realtimeSinceStartup;

            try
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out info) || info == null || info.Instance == null)
                {
                    return false;
                }

                var assembly = info.Instance.GetType().Assembly;
                var type = assembly.GetType("WardIsLove.Util.WardMonoscript");
                var extension = assembly.GetType("WardIsLove.Extensions.WardMonoscriptExt");
                var permissionsType = assembly.GetType("WardIsLove.WardManagers.PermissionsManager");

                var instances = type == null
                    ? null
                    : type.GetField("m_wardInstances", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                var enabled = type == null ? null : type.GetMethod("IsEnabled", Type.EmptyTypes);
                var radius = type == null || extension == null
                    ? null
                    : extension.GetMethod("GetWardRadius", BindingFlags.Public | BindingFlags.Static, null, new[] { type }, null);
                var permits = permissionsType == null
                    ? null
                    : permissionsType.GetMethod("IsPermitted", new[] { typeof(long) });

                if (instances == null || enabled == null || radius == null || permits == null)
                {
                    WarnOnce("WardIsLove found but its ward API is incomplete; wards will not govern claims.");
                    return false;
                }

                _wardType = type;
                _wardInstances = instances;
                _isEnabled = enabled;
                _getWardRadius = radius;
                _permits = permits;
                _nviewField = type.GetField("m_nview", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _permissionsField = type.GetField("permissionsManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _bound = true;

                Log.Info("WardIsLove ward API bound");
                Log.Debug("Ward", "bound m_wardInstances, IsEnabled, GetWardRadius, PermissionsManager");
                return true;
            }
            catch (Exception ex)
            {
                WarnOnce("WardIsLove binding failed: " + ex.Message);
                return false;
            }
        }

        private static void WarnOnce(string message)
        {
            if (Time.realtimeSinceStartup - _lastWarn <= 60f) return;
            _lastWarn = Time.realtimeSinceStartup;
            Log.Warn(message);
        }

        /// <summary>How many enabled wards exist in this process. Diagnostics only.</summary>
        public static int Count
        {
            get { return All().Count; }
        }

        /// <summary>Every enabled ward containing the point, in WardIsLove's own list order.</summary>
        public static void Containing(Vector3 point, System.Collections.Generic.List<WardInfo> into)
        {
            into.Clear();
            var wards = All();
            for (var i = 0; i < wards.Count; i++)
            {
                if (wards[i].Contains(point)) into.Add(wards[i]);
            }
        }

        /// <summary>
        /// True when the player may use this particular ward: they placed it, or its permission list
        /// says so. A ward that has vanished permits nobody.
        /// </summary>
        public static bool Permits(ZDOID wardId, long playerId)
        {
            if (wardId.IsNone()) return false;
            if (!Resolve()) return false;

            foreach (var ward in All())
            {
                if (!ward.Id.Equals(wardId)) continue;
                if (ward.OwnerId == playerId) return true;

                var component = ComponentFor(wardId);
                if (component == null) return false;

                try
                {
                    var manager = _permissionsField != null ? _permissionsField.GetValue(component) : null;
                    return manager != null && (bool)_permits.Invoke(manager, new object[] { playerId });
                }
                catch (Exception ex)
                {
                    Log.Debug("Ward", "permit check failed for " + wardId + ": " + ex.Message);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// True when a position sits inside a ward someone else owns. Ground beats ownership: a nisse
        /// never touches a container inside a ward it does not own, so two nisses never share a chest.
        /// </summary>
        public static bool BlocksOwner(long ownerId, Vector3 point)
        {
            if (!Resolve()) return false;

            foreach (var ward in All())
            {
                if (!ward.Contains(point)) continue;
                if (ward.OwnerId != ownerId) return true;
            }

            return false;
        }

        // ---- internals ---------------------------------------------------------------------------

        private static System.Collections.Generic.List<WardInfo> All()
        {
            if (Time.frameCount == _cacheFrame) return _cache;

            _cacheFrame = Time.frameCount;
            _cache.Clear();

            if (!Resolve()) return _cache;

            var wards = WardObjects();
            if (wards == null) return _cache;

            foreach (var entry in wards)
            {
                var component = entry as Component;
                if (component == null) continue;

                bool enabled;
                try
                {
                    enabled = (bool)_isEnabled.Invoke(component, null);
                }
                catch
                {
                    continue;
                }

                if (!enabled) continue;

                var zdo = ZdoOf(component);
                if (zdo == null) continue;

                float radius;
                try
                {
                    radius = Convert.ToSingle(_getWardRadius.Invoke(null, new object[] { component }));
                }
                catch
                {
                    continue;
                }

                if (radius <= 0f) continue;

                _cache.Add(new WardInfo
                {
                    Id = zdo.m_uid,
                    Center = component.transform.position,
                    Radius = radius,
                    OwnerId = zdo.GetLong("creator", 0L),
                });
            }

            return _cache;
        }

        private static IEnumerable WardObjects()
        {
            if (_wardInstances == null) return null;
            try
            {
                return _wardInstances.GetValue(null) as IEnumerable;
            }
            catch
            {
                return null;
            }
        }

        private static ZNetView ViewOf(object ward)
        {
            if (_nviewField == null) return null;
            try
            {
                return _nviewField.GetValue(ward) as ZNetView;
            }
            catch
            {
                return null;
            }
        }

        private static ZDO ZdoOf(object ward)
        {
            var view = ViewOf(ward);
            return view != null ? view.GetZDO() : null;
        }

        private static Component ComponentFor(ZDOID wardId)
        {
            var wards = WardObjects();
            if (wards == null) return null;

            foreach (var entry in wards)
            {
                var component = entry as Component;
                if (component == null) continue;
                var zdo = ZdoOf(component);
                if (zdo != null && zdo.m_uid.Equals(wardId)) return component;
            }

            return null;
        }
    }
}
