using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// The nisse itself. It has no combat behaviour and no autonomous movement: the flow scheduler
    /// decides what it does and <see cref="NisseMotion"/> walks it there. The Greyling controller
    /// carries forward/sideway/turn speed floats, an <c>idle</c> int and jump/attack/stagger/throw
    /// triggers — measured on the rig, so nothing here sets a parameter the rig does not have.
    /// </summary>
    public class NisseActor : BaseAI
    {
        private ZDO _zdo;
        private Character _character;

        public ZDOID BowlId { get; private set; }

        /// <summary>The body's walker and fade; null only on a prefab that never got the component.</summary>
        public NisseMotion Motion { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (m_nview != null && m_nview.IsValid()) _zdo = m_nview.GetZDO();
            BowlId = _zdo != null ? _zdo.GetZDOID(ZdoKeys.NisseBowl) : ZDOID.None;
            _character = GetComponent<Character>();
            Motion = GetComponent<NisseMotion>();
        }

        /// <summary>No hunting, no wandering, no fleeing. The flow scheduler decides what it does.</summary>
        public override bool UpdateAI(float dt)
        {
            return false;
        }

        public void WarpTo(Vector3 position)
        {
            transform.position = position;
            if (m_body != null) m_body.position = position;
            if (_character != null) _character.SetVelocity(Vector3.zero);
        }

        /// <summary>Faces a point without moving, for the transfer beat at a container.</summary>
        public void FaceTowards(Vector3 point)
        {
            if (_character == null) return;
            var direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) _character.SetLookDir(direction.normalized, 0.2f);
        }

        /// <summary>The Greyling controller has no spawn trigger; the fade-in is the arrival.</summary>
        public void PlaySpawn()
        {
            PlayIdle();
        }

        /// <summary>Idle charm pose, used by the animation pass.</summary>
        public void PlayIdle()
        {
            if (m_animator == null) return;
            if (m_animator.HasParameter("idle", AnimatorControllerParameterType.Int)) m_animator.SetInt("idle", 0);
        }
    }
}
