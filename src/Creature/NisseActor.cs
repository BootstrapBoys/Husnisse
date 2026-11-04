using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Creature
{
    /// <summary>
    /// The nisse itself. It has no combat behaviour and no autonomous movement: 0.1 moves it by
    /// fading between the bowl and the piece it is working on. Walking is 0.1.1.
    /// </summary>
    public class NisseActor : BaseAI
    {
        private ZDO _zdo;

        public ZDOID BowlId { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (m_nview != null && m_nview.IsValid()) _zdo = m_nview.GetZDO();
            BowlId = _zdo != null ? _zdo.GetZDOID(ZdoKeys.NisseBowl) : ZDOID.None;
        }

        /// <summary>No hunting, no wandering, no fleeing. The round scheduler decides what it does.</summary>
        public override bool UpdateAI(float dt)
        {
            return false;
        }

        public void WarpTo(Vector3 position)
        {
            transform.position = position;
            if (m_body != null) m_body.position = position;
        }

        public void PlaySpawn()
        {
            if (m_animator != null) m_animator.SetTrigger("spawn");
        }

        /// <summary>Idle charm pose, used by the 0.1.1 animation pass.</summary>
        public void PlayIdle()
        {
            if (m_animator != null) m_animator.SetTrigger("idle");
        }
    }
}
