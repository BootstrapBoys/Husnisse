using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Localization;
using Husnisse.Memory;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// One visible piece of work: collect a batch, carry it to the chest that owns it, play the
    /// transfer, walk home. Items move only while the nisse stands at the container, so the
    /// creature is always the visible cause. A leg that cannot be walked is faded across and
    /// reported once per trip, never silently skipped.
    /// </summary>
    public sealed class Trip
    {
        private enum Phase
        {
            Pickup,
            Loading,
            ToTarget,
            Transfer,
            Home,
            Done,
            Aborted,
        }

        private const float LoadSeconds = 0.35f;
        private const float TransferSeconds = 0.8f;

        private readonly Claim _claim;
        private readonly NisseActor _nisse;
        private readonly NisseMotion _motion;
        private readonly List<FlowTicket> _pending = new List<FlowTicket>();
        private readonly List<Vector3> _pickups = new List<Vector3>();
        private readonly HashSet<FlowSource> _sources = new HashSet<FlowSource>();
        private Container _target;
        private Vector3 _targetPoint;
        private Vector3 _homePoint;
        private string _why = string.Empty;
        private Phase _phase;
        private int _pickupIndex;
        private float _timer;
        private bool _reported;
        private bool _legSet;
        private Vector3 _legPoint;
        private float _legArrive;

        public int Carried => _pending.Count;
        public int Landed { get; private set; }
        public bool Finished => _phase == Phase.Done || _phase == Phase.Aborted;
        public bool Aborted => _phase == Phase.Aborted;

        /// <summary>Whether this exact stack has been taken and not yet put down.</summary>
        public bool Carries(ItemDrop.ItemData item)
        {
            if (item == null) return false;

            foreach (var ticket in _pending)
            {
                if (ticket != null && ReferenceEquals(ticket.Item, item)) return true;
            }
            return false;
        }

        private Trip(Claim claim, NisseActor nisse)
        {
            _claim = claim;
            _nisse = nisse;
            _motion = nisse != null ? nisse.GetComponent<NisseMotion>() : null;
        }

        /// <summary>
        /// Takes up to <paramref name="budget"/> tickets that share one destination chest, all from
        /// the task type the queue's rotation picked for this trip. The rotation decides which
        /// source is served; the batch is then filled from that source alone, so fairness costs the
        /// trip nothing — the other task type waits one trip, not one stack. Tickets in the lane
        /// that route elsewhere go back to its head.
        /// </summary>
        public static Trip Start(Claim claim, NisseActor nisse, int budget)
        {
            if (claim == null || nisse == null || budget <= 0) return null;

            var trip = new Trip(claim, nisse);
            var memory = MemoryOf(nisse);

            FlowTicket first;
            if (!FlowQueue.TryDequeue(claim, out first)) return null;

            // The rotation has already moved past this lane, so the next trip starts at the next one.
            var lane = first != null ? first.Source : FlowSource.Ground;

            for (var taken = 0; taken < budget; taken++)
            {
                var ticket = first;
                first = null;

                if (taken > 0 && !FlowQueue.TryDequeueFrom(claim, lane, out ticket)) break;
                if (ticket == null) break;

                if (!ticket.IsValid())
                {
                    continue;
                }

                var decision = StorageRouter.Decide(claim, ticket.Item, memory, ticket.ExcludeContainer());
                var target = decision.Container;
                if (target == null)
                {
                    // Nothing accepts it. A ground drop is re-queued by the next scan, and a stack
                    // from a chest stays where it is: the ticket is dropped, never silently landed
                    // in a chest whose rule said otherwise.
                    Log.Debug("Flow", "trip: nowhere to put " + ticket.Item.m_shared.m_name + " (" + decision.Why + ")");
                    DiagnosticsFile.Line("[Flow] unmatched " + ticket.Item.m_shared.m_name +
                                          " why=" + decision.Why + " claim=" + claim.BowlId);
                    continue;
                }

                if (trip._target == null)
                {
                    trip._target = target;
                    trip._targetPoint = StandPoint(target.transform.position, nisse.transform.position);
                    trip._why = decision.Why;
                }
                else if (trip._target != target)
                {
                    FlowQueue.EnqueueFront(ticket);
                    break;
                }

                trip._pending.Add(ticket);
                trip._sources.Add(ticket.Source);
                trip.AddPickup(ticket);
            }

            if (trip._pending.Count == 0) return null;

            trip._homePoint = HomePoint(claim, nisse);
            trip._phase = trip._pickups.Count > 0 ? Phase.Pickup : Phase.ToTarget;
            trip.OrderPickups();
            Log.Debug("Flow", "trip start claim=" + claim.BowlId + " stacks=" + trip._pending.Count +
                              " pickups=" + trip._pickups.Count + " target=" + trip._target.name +
                              " at=" + Where(trip._target.transform.position) + " why=" + trip._why +
                              " src=" + trip.SourceToken() +
                              " leg_m=" + Vector3.Distance(nisse.transform.position, trip._targetPoint).ToString("0.0"));
            DiagnosticsFile.Line("[Flow] trip stacks=" + trip._pending.Count + " target=" + trip._target.name +
                                 " at=" + Where(trip._target.transform.position) + " why=" + trip._why +
                                 " src=" + trip.SourceToken() +
                                 " claim=" + claim.BowlId);
            return trip;
        }

        private static string Where(Vector3 point)
        {
            return "(" + point.x.ToString("0.#") + "," + point.z.ToString("0.#") + ")";
        }

        private void AddPickup(FlowTicket ticket)
        {
            var point = Vector3.zero;
            if (ticket.Source == FlowSource.Ground)
            {
                if (ticket.Drop == null) return;
                point = ticket.Drop.transform.position;
            }
            else if (ticket.Source == FlowSource.Container)
            {
                if (ticket.Container == null) return;
                point = StandPoint(ticket.Container.transform.position, _nisse.transform.position);
            }
            else
            {
                return; // pockets are handed over where the nisse already stands
            }

            foreach (var existing in _pickups)
            {
                if (Vector3.Distance(existing, point) < 0.75f) return;
            }

            _pickups.Add(point);
        }

        /// <summary>Nearest-first, so a sweep reads as one errand instead of pacing the room.</summary>
        private void OrderPickups()
        {
            if (_pickups.Count < 2 || _nisse == null) return;

            var remaining = new List<Vector3>(_pickups);
            var ordered = new List<Vector3>(_pickups.Count);
            var from = _nisse.transform.position;

            while (remaining.Count > 0)
            {
                var best = 0;
                var bestDistance = Vector3.Distance(from, remaining[0]);
                for (var i = 1; i < remaining.Count; i++)
                {
                    var distance = Vector3.Distance(from, remaining[i]);
                    if (distance >= bestDistance) continue;
                    best = i;
                    bestDistance = distance;
                }

                var next = remaining[best];
                ordered.Add(next);
                remaining.RemoveAt(best);
                from = next;
            }

            _pickups.Clear();
            _pickups.AddRange(ordered);
        }

        /// <summary>Called every frame on the server by the trip runner.</summary>
        public void Tick(float dt)
        {
            if (Finished) return;

            if (_claim.Bowl == null || _nisse == null)
            {
                Abort();
                return;
            }

            switch (_phase)
            {
                case Phase.Pickup:
                    if (MoveTo(_pickups[_pickupIndex], 1.2f))
                    {
                        _phase = Phase.Loading;
                        _timer = LoadSeconds;
                    }
                    break;

                case Phase.Loading:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    _pickupIndex++;
                    _phase = _pickupIndex < _pickups.Count ? Phase.Pickup : Phase.ToTarget;
                    break;

                case Phase.ToTarget:
                    if (MoveTo(_targetPoint, 1.1f))
                    {
                        if (_target != null) _nisse.FaceTowards(_target.transform.position);
                        _phase = Phase.Transfer;
                        _timer = TransferSeconds;
                    }
                    break;

                case Phase.Transfer:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    Land();
                    _phase = Phase.Home;
                    break;

                case Phase.Home:
                    if (MoveTo(_homePoint, 0.9f)) _phase = Phase.Done;
                    break;
            }
        }

        /// <summary>
        /// Walks a leg and reports whether the nisse stands there. On an unreachable leg the body
        /// is faded across once, the trip says so, and the leg counts as done.
        /// </summary>
        private bool MoveTo(Vector3 point, float arrive)
        {
            if (_motion == null) return true;

            if (_motion.IsFading) return false;

            if (!_legSet || Vector3.Distance(_legPoint, point) > 0.5f || _legArrive != arrive)
            {
                _legPoint = point;
                _legArrive = arrive;
                _legSet = true;
                _motion.Travel(point, arrive);
                return false;
            }

            switch (_motion.State)
            {
                case TravelState.Arrived:
                    return true;

                case TravelState.Unreachable:
                    if (!_reported)
                    {
                        _reported = true;
                        NisseVoice.Say(_claim, Loc.T(Loc.VoiceUnreachable), NisseVoice.Kind.Event);
                    }
                    _motion.FadeAcross(point);
                    return false;

                default:
                    return false;
            }
        }

        private void Land()
        {
            if (_target == null) return;

            var inventory = _target.GetInventory();
            if (inventory == null) return;

            var memory = MemoryOf(_nisse);
            var moved = 0;

            // The target's ZDO may belong to whoever last opened this chest — on a server that is a
            // player, because opening a container hands it to the opener. Container.Save only fires
            // for the owner, so the ZDO is taken before the first add and the packed inventory is
            // read back out of it afterwards.
            var touched = new List<Container>();
            var claimed = true;
            if (_pending.Count > 0)
            {
                if (!ContainerWrite.EnsureOwned(_target, "trip deposit")) claimed = false;
                touched.Add(_target);
            }

            foreach (var ticket in _pending.ToArray())
            {
                if (!ticket.IsValid())
                {
                    _pending.Remove(ticket);
                    continue;
                }

                var clone = ticket.Item.Clone();
                if (!inventory.CanAddItem(clone))
                {
                    FlowQueue.EnqueueFront(ticket);
                    _pending.Remove(ticket);
                    continue;
                }

                if (ticket.Source == FlowSource.Ground)
                {
                    // Add before destroying the drop: a failed add leaves the drop in the world.
                    if (!inventory.AddItem(clone))
                    {
                        FlowQueue.EnqueueFront(ticket);
                        _pending.Remove(ticket);
                        continue;
                    }

                    if (ticket.Drop != null)
                    {
                        if (ZNetScene.instance != null) ZNetScene.instance.Destroy(ticket.Drop.gameObject);
                        else Object.Destroy(ticket.Drop.gameObject);
                    }

                    StorageRouter.Learn(_target, clone.m_shared.m_name, memory);
                    _pending.Remove(ticket);
                    moved++;
                    continue;
                }

                // Container and pocket stacks: take from the source, then add; put it back if the
                // add fails, because the source is the only copy. A source chest's ZDO has the same
                // problem as the target's, and its removal has to be taken away from the items too:
                // an unsaved removal would leave the stack in the chest *and* in the destination.
                if (ticket.Source == FlowSource.Container && ticket.Container != null && !touched.Contains(ticket.Container))
                {
                    if (!ContainerWrite.EnsureOwned(ticket.Container, "trip pickup")) claimed = false;
                    touched.Add(ticket.Container);
                }

                var source = ticket.SourceInventory();
                if (source == null || !source.RemoveItem(ticket.Item, ticket.Amount))
                {
                    _pending.Remove(ticket);
                    continue;
                }

                if (!inventory.AddItem(clone))
                {
                    source.AddItem(clone);
                    Log.Error("flow: target refused " + clone.m_shared.m_name + " after removal; returned to source");
                    _pending.Remove(ticket);
                    continue;
                }

                StorageRouter.Learn(_target, clone.m_shared.m_name, memory);
                _pending.Remove(ticket);
                moved++;
            }

            var persisted = claimed;
            if (moved > 0)
            {
                foreach (var container in touched)
                {
                    if (!ContainerWrite.Persist(container, "trip")) persisted = false;
                }
            }

            Landed += moved;
            if (moved > 0) FlowScheduler.Instance?.Spend(moved);
            Log.Debug("Flow", "trip deposit claim=" + _claim.BowlId + " moved=" + moved + " target=" + _target.name +
                              " at=" + Where(_target.transform.position) + " why=" + _why +
                              " remaining=" + _pending.Count);
            if (moved > 0 && !persisted)
            {
                Log.Error("flow: deposit into " + _target.name + " at=" + Where(_target.transform.position) +
                          " did not reach the ZDO");
            }
            DiagnosticsFile.Line("[Flow] landed moved=" + moved + " target=" + _target.name +
                                 " at=" + Where(_target.transform.position) + " why=" + _why +
                                 " src=" + SourceToken() + " zdo=" + (persisted ? 1 : 0));
        }

        /// <summary>Which task types this batch came from: ground, container, pockets, or a mix.</summary>
        private string SourceToken()
        {
            if (_sources.Count == 0) return "none";
            if (_sources.Count > 1) return "mixed";

            foreach (var source in _sources) return FlowQueue.Token(source);
            return "none";
        }

        public void Abort()
        {
            if (Finished) return;

            _phase = Phase.Aborted;
            if (_motion != null) _motion.Stop();

            foreach (var ticket in _pending)
            {
                if (ticket.IsValid()) FlowQueue.EnqueueFront(ticket);
            }

            Log.Debug("Flow", "trip aborted claim=" + (_claim != null ? _claim.BowlId.ToString() : "-") +
                              " returned=" + _pending.Count);
            _pending.Clear();
        }

        public string ShortState()
        {
            if (Finished) return "done";
            return _phase + (Carried > 0 ? "/" + Carried : "");
        }

        // ------------------------------------------------------------------ points

        /// <summary>A place to stand near a piece: one step from the piece toward the nisse.</summary>
        private static Vector3 StandPoint(Vector3 piece, Vector3 from)
        {
            var direction = piece - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            return piece - direction.normalized * 1.15f;
        }

        private static Vector3 HomePoint(Claim claim, NisseActor nisse)
        {
            var bowl = claim.Bowl;
            if (bowl == null) return nisse.transform.position;

            var forward = bowl.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            return bowl.transform.position + forward.normalized * 1.5f + Vector3.up * 0.1f;
        }

        private static ItemMemory MemoryOf(NisseActor nisse)
        {
            var view = nisse != null ? nisse.GetComponent<ZNetView>() : null;
            return view != null && view.GetZDO() != null ? new ItemMemory(view.GetZDO()) : null;
        }
    }
}
