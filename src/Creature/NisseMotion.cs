using System.Collections.Generic;
using Husnisse.Core;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Creature
{
    public enum TravelState
    {
        Idle,
        Moving,
        Waiting,
        Arrived,
        Unreachable,
    }

    /// <summary>
    /// Path following and the threshold fade, owned by the server and played on every peer.
    ///
    /// A leg is planned with <see cref="Pathfinding.GetPath"/> asking for a full path: a partial
    /// path is exactly the doorway case, and stopping at the threshold is the failure this exists
    /// to avoid. A failed plan is retried once after <see cref="RetryDelay"/> (the tile rebuild is
    /// lazy), then the leg reports <see cref="TravelState.Unreachable"/> and the trip fades across.
    /// </summary>
    public class NisseMotion : MonoBehaviour
    {
        public const string FadeRpc = "Husnisse_Fade";

        /// <summary>How long a cold tile gets to finish its rebuild before the retry.</summary>
        private const float RetryDelay = 2.5f;
        private const float CornerReach = 0.45f;
        private const float StuckSeconds = 3f;
        private const float StuckDistance = 0.35f;
        private const float FadeSeconds = 0.45f;
        private const float SameFloor = 3f;
        private const float FallenBelow = 15f;

        private NisseActor _actor;
        private Character _character;
        private ZNetView _nview;
        private NisseFade _fade;

        private readonly List<Vector3> _path = new List<Vector3>();
        private int _corner;
        private Vector3 _goal;
        private float _arrive;
        private float _retryAt;
        private int _attempt;
        private float _stuckTimer;
        private Vector3 _stuckFrom;

        private float _fadeTimer;
        private Vector3 _fadeTarget;
        private bool _fadeSnapped;

        public TravelState State { get; private set; } = TravelState.Idle;

        /// <summary>How many plans this leg needed; 2 means the retry carried it.</summary>
        public int Attempts => _attempt;

        /// <summary>Legs that ended in a fade, for the diagnostics line.</summary>
        public int Fades { get; private set; }

        public bool IsFading => _fadeTimer > 0f;
        public bool IsTravelling => State == TravelState.Moving || State == TravelState.Waiting;
        public Vector3 Goal => _goal;
        public float Remaining => Horizontal(transform.position, _goal);

        private bool IsOwner => _nview != null && _nview.IsValid() && _nview.IsOwner();

        private void Awake()
        {
            _actor = GetComponent<NisseActor>();
            _character = GetComponent<Character>();
            _nview = GetComponent<ZNetView>();
            _fade = GetComponent<NisseFade>();

            if (_nview != null)
            {
                _nview.Register<Vector3>(FadeRpc, RPC_Fade);
            }
        }

        /// <summary>Starts a new leg. Safe to call again with the same point (it is ignored).</summary>
        public void Travel(Vector3 destination, float arrive = 0.9f)
        {
            if (!IsOwner || IsFading) return;
            if (State == TravelState.Moving && Horizontal(_goal, destination) < 0.5f) return;

            _goal = destination;
            _arrive = Mathf.Max(0.3f, arrive);
            _attempt = 0;
            State = TravelState.Moving;
            Plan();
        }

        public void Stop()
        {
            if (IsOwner && _character != null) _character.SetMoveDir(Vector3.zero);
            State = TravelState.Idle;
            _path.Clear();
            _corner = 0;
        }

        private bool Plan()
        {
            _attempt++;
            _path.Clear();
            _corner = 0;

            if (Pathfinding.instance == null)
            {
                State = TravelState.Unreachable;
                return false;
            }

            // Full path only: a partial path stops at the threshold, which is the fade case.
            if (Pathfinding.instance.GetPath(transform.position, _goal, _path,
                    Pathfinding.AgentType.Humanoid, requireFullPath: true, cleanup: true)
                && _path.Count > 0)
            {
                _stuckFrom = transform.position;
                _stuckTimer = 0f;
                State = TravelState.Moving;
                Log.Debug("Creature", "leg -> " + _goal.ToString("F1") + " from " + transform.position.ToString("F1") +
                                      " corners=" + _path.Count + " attempt=" + _attempt);
                return true;
            }

            // A navmesh that exists under the destination but cannot be reached is a wall, a doorway
            // or another floor: no retry can help, so fade at once. A destination with no navmesh at
            // all is a cold tile and gets the one retry.
            if (GoalOnNavMesh())
            {
                State = TravelState.Unreachable;
                Log.Debug("Creature", "leg blocked -> " + _goal.ToString("F1"));
                return false;
            }

            if (_attempt <= 1)
            {
                // The first request pokes the tile; it is built at most once per interval.
                _retryAt = Time.time + RetryDelay;
                State = TravelState.Waiting;
                Log.Debug("Creature", "leg waiting for navmesh -> " + _goal.ToString("F1"));
                return false;
            }

            State = TravelState.Unreachable;
            Log.Debug("Creature", "leg unreachable -> " + _goal.ToString("F1"));
            return false;
        }

        /// <summary>
        /// True when the destination itself carries navmesh. That separates "the path is blocked"
        /// from "this tile has not been baked yet", which look identical through GetPath: the
        /// threshold case measured on the rig ends 5.42 m short of a destination that is on the mesh.
        /// </summary>
        private bool GoalOnNavMesh()
        {
            UnityEngine.AI.NavMeshHit hit;
            return UnityEngine.AI.NavMesh.SamplePosition(_goal, out hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas);
        }

        private void Update()
        {
            if (IsFading) TickFade();
            if (IsFading) return; // a fading body does not also walk its old path
            if (!IsOwner) return;

            if (State == TravelState.Waiting)
            {
                if (Time.time >= _retryAt) Plan();
                return;
            }

            if (State != TravelState.Moving) return;

            if (_character == null)
            {
                State = TravelState.Unreachable;
                return;
            }

            // A body below its own path is falling out of the world, not walking. Recover it rather
            // than let it "arrive" from a kilometre down, which horizontal distance would report.
            if (transform.position.y < _goal.y - FallenBelow)
            {
                Log.Warn("nisse fell below its path; returning it to " + _goal.ToString("F1"));
                if (_actor != null) _actor.WarpTo(_goal);
                else transform.position = _goal;
                Stop();
                State = TravelState.Arrived;
                return;
            }

            if (Reached(transform.position, _goal, _arrive))
            {
                Stop();
                State = TravelState.Arrived;
                return;
            }

            while (_corner < _path.Count && Reached(transform.position, _path[_corner], CornerReach))
            {
                _corner++;
            }

            if (_corner >= _path.Count)
            {
                // Ran out of corners without arriving. Once is a stale path; twice is a body that
                // cannot get closer, and re-planning every frame would be a storm.
                if (Reached(transform.position, _goal, _arrive + 1f))
                {
                    Stop();
                    State = TravelState.Arrived;
                    return;
                }

                if (_attempt >= 2)
                {
                    StopMovingOnly();
                    State = TravelState.Unreachable;
                    Log.Debug("Creature", "leg exhausted at " + transform.position.ToString("F1") +
                                          " short of " + _goal.ToString("F1"));
                    return;
                }

                Plan();
                return;
            }

            var step = _path[_corner] - transform.position;
            step.y = 0f;
            if (step.sqrMagnitude < 0.0001f)
            {
                _corner++;
                return;
            }

            _character.SetWalk(true);
            _character.SetMoveDir(step.normalized);
            _character.SetLookDir(step.normalized, 0.25f);

            _stuckTimer += Time.deltaTime;
            if (_stuckTimer < StuckSeconds) return;

            if (Reached(transform.position, _stuckFrom, StuckDistance))
            {
                Log.Debug("Creature", "leg stuck at " + transform.position.ToString("F1"));
                StopMovingOnly();
                if (Plan()) return;
            }

            _stuckTimer = 0f;
            _stuckFrom = transform.position;
        }

        private void StopMovingOnly()
        {
            if (_character != null) _character.SetMoveDir(Vector3.zero);
        }

        /// <summary>Fades the body out, moves it, and fades it back in on every peer.</summary>
        public void FadeAcross(Vector3 destination)
        {
            if (IsFading) return;

            Fades++;
            Log.Debug("Creature", "fade across to " + destination.ToString("F1"));

            BeginFade(destination);
            if (IsOwner && _nview != null) BroadcastFade(destination);
        }

        /// <summary>
        /// The fade has to play on every peer, not just where it was ordered. InvokeRPC(method)
        /// asks the ZDO owner, which on a dedicated server is the server itself: the fade ran
        /// where nobody can see it and a client only saw the body snap to its new place. Address
        /// every peer instead.
        /// </summary>
        private void BroadcastFade(Vector3 destination)
        {
            if (ZNet.instance == null) return;

            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (peer == null || peer.m_rpc == null) continue;
                _nview.InvokeRPC(peer.m_uid, FadeRpc, new object[] { destination });
            }
        }

        private void BeginFade(Vector3 destination)
        {
            if (IsFading) return;

            StopMovingOnly();
            _fadeTarget = destination;
            _fadeSnapped = false;
            _fadeTimer = FadeSeconds * 2f;
            State = TravelState.Moving;

            if (_fade != null) _fade.FadeOut(FadeSeconds);
        }

        private void TickFade()
        {
            _fadeTimer -= Time.deltaTime;

            if (!_fadeSnapped && _fadeTimer <= FadeSeconds)
            {
                _fadeSnapped = true;
                if (_actor != null) _actor.WarpTo(_fadeTarget);
                else transform.position = _fadeTarget;
                if (_fade != null) _fade.FadeIn(FadeSeconds);
            }

            if (_fadeTimer > 0f) return;

            _fadeTimer = 0f;
            State = TravelState.Arrived;
        }

        private void RPC_Fade(long sender, Vector3 destination)
        {
            BeginFade(destination);
        }

        private static bool Reached(Vector3 a, Vector3 b, float horizontal)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz <= horizontal * horizontal && Mathf.Abs(a.y - b.y) <= SameFloor;
        }

        private static float Horizontal(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
