using System;
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
                Log.Info("WardIsLove permissions bound");
            }
            catch (Exception ex)
            {
                Log.Warn("WardIsLove binding failed: " + ex.Message);
            }
        }

        /// <summary>
        /// True when the claim owner may touch this position. With no WardIsLove installed, or no
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
    }
}
