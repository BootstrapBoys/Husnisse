using Husnisse.Config;
using Husnisse.Core;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// Animation level of detail. A nisse farther than `2. Nisse / Animation LOD Distance` from every
    /// viewer stops being animated on this peer — its Animator is switched off and set to cull
    /// completely — while its work continues in the queue and the trips keep running. The body is a
    /// runtime clone of a game creature, so the only honest knob is the Animator itself: no custom
    /// animation is played here and nothing else in the trip depends on the pose.
    ///
    /// Every peer decides for itself, because animation is a view and each peer has its own copy of the
    /// body; the server has no players of its own and uses the peers' reference positions. The effect is
    /// measured per peer, so a dedicated server with nobody connected animates nothing at all, and a
    /// client animates the nisses near it.
    /// </summary>
    public class NisseLod : MonoBehaviour
    {
        /// <summary>How often the distance is sampled. Twice a second is far below walking speed.</summary>
        private const float SampleSeconds = 0.5f;

        private Animator _animator;
        private float _nextSample;
        private bool _far;
        private bool _applied;

        /// <summary>Whether this body is beyond the LOD distance right now.</summary>
        public bool Far => _far;

        /// <summary>Metres to the nearest viewer; infinity when nobody is looking.</summary>
        public float Nearest { get; private set; } = float.PositiveInfinity;

        private void Awake()
        {
            _animator = ResolveAnimator();
        }

        private Animator ResolveAnimator()
        {
            var animator = GetComponent<Animator>();
            if (animator != null) return animator;

            // The body is a clone of a game creature: its Animator may sit on the visual child
            // rather than on the root the Character lives on.
            return GetComponentInChildren<Animator>(true);
        }

        private void Update()
        {
            if (Time.time < _nextSample) return;
            _nextSample = Time.time + SampleSeconds;

            int viewers;
            Apply(Viewers.Nearest(transform.position, out viewers));
        }

        /// <summary>
        /// Puts the body in the mode that distance asks for. Separate from <see cref="Update"/> so a
        /// probe can drive both ends without a player in the world — the distance source and the
        /// effect are proven separately, and neither needs a client.
        /// </summary>
        public void Apply(float nearestViewer)
        {
            Nearest = nearestViewer;
            var far = nearestViewer > HusnisseConfig.LodDistance.Value;

            if (_applied && far == _far) return;
            _applied = true;
            _far = far;

            if (_animator == null) _animator = ResolveAnimator();
            if (_animator == null)
            {
                Log.Debug("Creature", "lod " + (far ? "far" : "near") + " but the body has no Animator");
                return;
            }

            _animator.enabled = !far;
            _animator.cullingMode = far ? AnimatorCullingMode.CullCompletely : AnimatorCullingMode.AlwaysAnimate;

            Log.Debug("Creature", "lod " + (far ? "far" : "near") + " nearest=" +
                                  (float.IsInfinity(nearestViewer) ? "none" : nearestViewer.ToString("0.0") + "m") +
                                  " animator=" + _animator.enabled);
        }
    }
}
