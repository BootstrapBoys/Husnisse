using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Claims
{
    /// <summary>
    /// The world-space ring that shows a claim's ground. It is a plain visual: no collider, no ZDO,
    /// nothing replicated. One ring exists at a time and the caller decides what it means — the
    /// placement ghost (green placeable, red refused) and, from 0.3.1, the ledger row the player is
    /// looking at. Keeping it a single shared object is what makes those two surfaces show the same
    /// thing.
    /// </summary>
    public static class ClaimRing
    {
        private const int Segments = 72;
        private const float SampleInterval = 0.15f;
        private const float Width = 0.3f;

        private static LineRenderer _line;
        private static int _mask;
        private static bool _maskBuilt;

        private static Vector3 _lastCenter;
        private static float _lastRadius = -1f;
        private static Color _lastColor;
        private static float _lastBuild = -10f;
        private static bool _logged;

        public static bool Visible
        {
            get { return _line != null && _line.enabled && _line.gameObject.activeSelf; }
        }

        /// <summary>Draws the ring around <paramref name="center"/> with a radius in metres.</summary>
        public static void Show(Vector3 center, float radius, Color color)
        {
            if (radius <= 0.1f) { Hide(); return; }
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;

            EnsureLine();
            if (_line == null) return;

            if (!_line.gameObject.activeSelf) _line.gameObject.SetActive(true);

            var stable = (center - _lastCenter).sqrMagnitude < 0.0625f &&
                         Mathf.Abs(radius - _lastRadius) < 0.05f &&
                         color == _lastColor &&
                         Time.unscaledTime - _lastBuild < SampleInterval;
            if (stable) return;

            _lastCenter = center;
            _lastRadius = radius;
            _lastColor = color;
            _lastBuild = Time.unscaledTime;

            _line.startColor = color;
            _line.endColor = color;
            if (_line.material != null) _line.material.color = color;

            var fallbackY = center.y;
            var step = Mathf.PI * 2f / Segments;
            _line.positionCount = Segments;
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * step;
                var point = new Vector3(center.x + Mathf.Cos(angle) * radius, fallbackY,
                                        center.z + Mathf.Sin(angle) * radius);
                point.y = GroundY(point, fallbackY);
                _line.SetPosition(i, point);

                if (!_logged && i == 0)
                {
                    _logged = true;
                    Log.Info("claim ring: center=" + center.x.ToString("0.#") + "," + center.z.ToString("0.#") +
                             " r=" + radius.ToString("0.#") + " first=" + point.ToString("F1") +
                             " width=" + Width + " active=" + _line.gameObject.activeSelf +
                             " shader=" + (_line.material != null && _line.material.shader != null ? _line.material.shader.name : "none"));
                }
            }
        }

        public static void Hide()
        {
            if (_line == null) return;
            if (_line.gameObject.activeSelf) _line.gameObject.SetActive(false);
            _lastRadius = -1f;
        }

        /// <summary>
        /// Shows the ring for a few seconds and then hides it. Used by `husnisse_preview`, where
        /// there is no per-frame caller to hide it again; the ledger will want the same behaviour.
        /// </summary>
        public static void ShowFor(Vector3 center, float radius, Color color, float seconds)
        {
            Show(center, radius, color);
            if (_line == null) return;

            var host = _line.gameObject;
            var timer = host.GetComponent<ClaimRingTimer>();
            if (timer == null) timer = host.AddComponent<ClaimRingTimer>();
            timer.HideAt = Time.unscaledTime + Mathf.Max(1f, seconds);
        }

        private sealed class ClaimRingTimer : MonoBehaviour
        {
            public float HideAt;

            private void Update()
            {
                if (Time.unscaledTime < HideAt) return;
                ClaimRing.Hide();
                Destroy(this);
            }
        }

        private static void EnsureLine()
        {
            if (_line != null) return;

            var existing = GameObject.Find("husnisse_claim_ring");
            var host = existing != null ? existing : new GameObject("husnisse_claim_ring");
            _line = host.GetComponent<LineRenderer>();
            if (_line == null) _line = host.AddComponent<LineRenderer>();

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Log.Warn("claim ring: no usable shader; the range preview will not draw");
                Object.Destroy(host);
                _line = null;
                return;
            }

            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = 0;
            _line.startWidth = Width;
            _line.endWidth = Width;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.generateLightingData = false;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Stretch;
            _line.material = new Material(shader) { name = "husnisse_claim_ring" };
            _line.enabled = true;
        }

        /// <summary>
        /// The ground under one ring vertex. The heightmap answers for open terrain; a short ray
        /// catches floors, stairs and other pieces so the ring lies on the base the player built
        /// instead of disappearing underneath it. When the ray misses, the caller's own height (the
        /// placement ghost, or the player asking for a preview) is the honest answer: a heightmap
        /// millimetres below a built platform would hide the whole ring.
        /// </summary>
        private static float GroundY(Vector3 point, float fallbackY)
        {
            if (!_maskBuilt)
            {
                _mask = LayerMask.GetMask("terrain", "terrain_solid", "static_solid", "Default", "piece", "piece_solid");
                _maskBuilt = true;
            }

            var y = fallbackY;
            if (_mask != 0)
            {
                var start = new Vector3(point.x, fallbackY + 12f, point.z);
                RaycastHit hit;
                if (Physics.Raycast(start, Vector3.down, out hit, 48f, _mask, QueryTriggerInteraction.Ignore))
                {
                    y = hit.point.y;
                }
            }

            return Mathf.Max(y, fallbackY - 0.02f) + 0.1f;
        }
    }
}
