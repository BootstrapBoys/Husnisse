using System.Collections.Generic;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// The spirit's disappearance. Fades every renderer of the creature and shrinks the visual a
    /// little, so stepping through a wall reads as the nisse leaving rather than as a teleport.
    ///
    /// The shader property is discovered at runtime: the body is a runtime clone and its material
    /// set is the game's, not ours to assume. Preference order is an explicit alpha float, the
    /// cutout threshold, then colour alpha; when the body exposes none of them the renderers are
    /// simply hidden at the end of the fade, which still hides the move but not the pop.
    /// </summary>
    public class NisseFade : MonoBehaviour
    {
        private enum Mode
        {
            None,
            Alpha,
            Cutoff,
            Color,
            Toggle,
        }

        private static readonly string[] AlphaProperties = { "_Alpha", "_Fade", "_Opacity" };
        private static readonly string[] CutoffProperties = { "_Cutoff" };
        private static readonly string[] ColorProperties = { "_Color", "_TintColor" };

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private Transform _visual;
        private Vector3 _visualScale = Vector3.one;
        private Mode _mode = Mode.None;
        private string _propertyName;
        private int _propertyId;
        private float _originalFloat = 1f;
        private Color _originalColor = Color.white;
        private bool _resolved;

        private float _alpha = 1f;
        private float _from = 1f;
        private float _to = 1f;
        private float _duration;
        private float _elapsed;

        /// <summary>True while a fade is running; the trip waits for it before moving on.</summary>
        public bool Busy { get; private set; }

        public float Alpha => _alpha;

        private void Awake()
        {
            Resolve();
        }

        /// <summary>Finds the renderers and the one property this body can fade on.</summary>
        public void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            _block = new MaterialPropertyBlock();

            var character = GetComponent<Character>();
            var visual = character != null ? character.GetVisual() : null;
            _visual = visual != null ? visual.transform : transform;
            _visualScale = _visual.localScale;

            _renderers.Clear();
            _renderers.AddRange(_visual.GetComponentsInChildren<Renderer>(true));

            foreach (var renderer in _renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                var material = renderer.sharedMaterial;

                if (TryFloatProperty(material, AlphaProperties, Mode.Alpha)) break;
                if (TryFloatProperty(material, CutoffProperties, Mode.Cutoff)) break;
                if (TryColorProperty(material, ColorProperties)) break;
            }

            if (_mode == Mode.None)
            {
                _mode = Mode.Toggle;
                Log.Warn("fade: no alpha property on this body; hiding it outright between steps");
            }
            else
            {
                Log.Info("fade: property=" + _propertyName + " mode=" + _mode + " renderers=" + _renderers.Count +
                         " shader=" + FirstShaderName());
            }
        }

        private bool TryFloatProperty(Material material, string[] names, Mode mode)
        {
            foreach (var name in names)
            {
                if (!material.HasProperty(name)) continue;
                _mode = mode;
                _propertyName = name;
                _propertyId = Shader.PropertyToID(name);
                _originalFloat = material.GetFloat(_propertyId);
                return true;
            }
            return false;
        }

        private bool TryColorProperty(Material material, string[] names)
        {
            foreach (var name in names)
            {
                if (!material.HasProperty(name)) continue;
                _mode = Mode.Color;
                _propertyName = name;
                _propertyId = Shader.PropertyToID(name);
                _originalColor = material.GetColor(_propertyId);
                return true;
            }
            return false;
        }

        private string FirstShaderName()
        {
            foreach (var renderer in _renderers)
            {
                if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null)
                    return renderer.sharedMaterial.shader.name;
            }
            return "none";
        }

        /// <summary>Fade out over the given time; the visual is hidden when the fade finishes.</summary>
        public void FadeOut(float seconds)
        {
            FadeTo(0f, seconds);
        }

        /// <summary>Show the visual again and fade it back in.</summary>
        public void FadeIn(float seconds)
        {
            SetRenderersEnabled(true);
            FadeTo(1f, seconds);
        }

        public void SetVisible(bool visible)
        {
            _alpha = visible ? 1f : 0f;
            _from = _alpha;
            _to = _alpha;
            _duration = 0f;
            _elapsed = 0f;
            Busy = false;
            SetRenderersEnabled(visible);
            Apply(_alpha);
        }

        private void FadeTo(float target, float seconds)
        {
            _from = _alpha;
            _to = Mathf.Clamp01(target);
            _duration = Mathf.Max(0.05f, seconds);
            _elapsed = 0f;
            Busy = true;
        }

        private void Update()
        {
            if (!Busy) return;

            _elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_elapsed / _duration);
            _alpha = Mathf.Lerp(_from, _to, t * t * (3f - 2f * t)); // smoothstep
            Apply(_alpha);

            if (t < 1f) return;

            Busy = false;
            _alpha = _to;
            if (_alpha <= 0.001f) SetRenderersEnabled(false);
        }

        private void Apply(float alpha)
        {
            if (_visual != null)
            {
                // A small shrink towards the ground sells the departure even on an opaque shader.
                _visual.localScale = _visualScale * Mathf.Lerp(0.15f, 1f, alpha);
            }

            if (_mode == Mode.Toggle) return;

            foreach (var renderer in _renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                renderer.GetPropertyBlock(_block);

                switch (_mode)
                {
                    case Mode.Alpha:
                        _block.SetFloat(_propertyId, Mathf.Lerp(0f, _originalFloat, alpha));
                        break;
                    case Mode.Cutoff:
                        // Cutoff clips fragments below the threshold, so raising it dissolves the body.
                        _block.SetFloat(_propertyId, Mathf.Lerp(1f, _originalFloat, alpha));
                        break;
                    case Mode.Color:
                        var color = _originalColor;
                        color.a *= alpha;
                        _block.SetColor(_propertyId, color);
                        break;
                }

                renderer.SetPropertyBlock(_block);
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            foreach (var renderer in _renderers)
            {
                if (renderer != null) renderer.enabled = enabled;
            }
        }
    }
}
