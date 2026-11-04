using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Compat
{
    /// <summary>
    /// Ward permissions, reached by reflection so WardIsLove stays a soft dependency.
    /// The seam is WardIsLove.Util.CustomCheck.CheckAccess(playerID, point, radius, flash): true when
    /// that player may use the area. flash:false keeps the ward shield from pulsing on a background scan.
    /// </summary>
    public static class WardIsLoveCompat
    {
        public const string PluginGuid = "Azumatt.WardIsLove";

        private static Func<long, Vector3, float, bool, bool> _checkAccess;
        private static Type _wardType;
        private static MethodInfo _getWardRadius;
        private static bool _resolved;
        private static float _lastWarn;

        public static bool Installed
        {
            get { return Chainloader.PluginInfos.ContainsKey(PluginGuid); }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out info) || info == null || info.Instance == null)
                {
                    return;
                }

                var assembly = info.Instance.GetType().Assembly;
                var type = assembly.GetType("WardIsLove.Util.CustomCheck");
                var method = type == null
                    ? null
                    : type.GetMethod("CheckAccess", BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(long), typeof(Vector3), typeof(float), typeof(bool) }, null);

                if (method == null || method.ReturnType != typeof(bool))
                {
                    Log.Warn("WardIsLove found but CustomCheck.CheckAccess(long, Vector3, float, bool) is missing; " +
                             "ward permissions will not filter containers.");
                    return;
                }

                _checkAccess = (Func<long, Vector3, float, bool, bool>)Delegate.CreateDelegate(
                    typeof(Func<long, Vector3, float, bool, bool>), method);

                _wardType = assembly.GetType("WardIsLove.Util.WardMonoscript");
                var extension = assembly.GetType("WardIsLove.Extensions.WardMonoscriptExt");
                _getWardRadius = _wardType == null || extension == null
                    ? null
                    : extension.GetMethod("GetWardRadius", BindingFlags.Public | BindingFlags.Static, null,
                        new[] { _wardType }, null);

                Log.Info("WardIsLove permissions bound");
                Log.Debug("Ward", "bound CheckAccess and ward owner lookup");
            }
            catch (Exception ex)
            {
                Log.Warn("WardIsLove binding failed: " + ex.Message);
            }
        }

        /// <summary>True when the claim owner may touch this position. With no WardIsLove installed, or no
        /// known owner, everything is permitted — the mod must not invent restrictions.
        /// </summary>
        public static bool IsPermitted(long playerId, Vector3 point)
        {
            if (playerId == 0L) return true;

            Resolve();
            if (_checkAccess == null) return true;

            try
            {
                return _checkAccess(playerId, point, 0f, false);
            }
            catch (Exception ex)
            {
                if (Time.time - _lastWarn > 60f)
                {
                    _lastWarn = Time.time;
                    Log.Warn("ward check failed; position treated as permitted: " + ex.Message);
                }
                return true;
            }
        }

        /// <summary>
        /// Finds the ward covering a point and reports the player who placed it. False when there is no
        /// ward there, or when WardIsLove is absent.
        /// </summary>
        public static bool TryGetWardOwner(Vector3 point, out long ownerId)
        {
            ownerId = 0L;
            Resolve();
            if (_wardType == null) return false;

            var field = _wardType.GetField("m_wardInstances", BindingFlags.Public | BindingFlags.Static);
            var wards = field != null ? field.GetValue(null) as IList : null;
            if (wards == null) return false;

            foreach (var ward in wards)
            {
                var mono = ward as MonoBehaviour;
                if (mono == null) continue;

                var radius = RadiusOf(ward);
                var dx = point.x - mono.transform.position.x;
                var dz = point.z - mono.transform.position.z;
                if (dx * dx + dz * dz > radius * radius) continue;

                ownerId = OwnerOf(ward);
                return true;
            }

            return false;
        }

        private static float RadiusOf(object ward)
        {
            try
            {
                if (_getWardRadius != null) return Convert.ToSingle(_getWardRadius.Invoke(null, new[] { ward }));
            }
            catch
            {
                // fall through to the static maximum
            }

            var field = _wardType.GetField("m_maxShieldRadius", BindingFlags.Public | BindingFlags.Instance);
            return field != null ? Convert.ToSingle(field.GetValue(ward)) : 0f;
        }

        private static long OwnerOf(object ward)
        {
            var field = _wardType.GetField("m_piece", BindingFlags.Public | BindingFlags.Instance);
            var piece = field != null ? field.GetValue(ward) as Piece : null;
            return piece != null ? piece.GetCreator() : 0L;
        }
    }
}
