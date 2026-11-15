using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Contract;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Localization;
using Husnisse.Net;
using Husnisse.Orders;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Flow
{
    /// <summary>
    /// One supply errand: the nisse goes to the chests the plan named, takes the materials out while
    /// it stands at each one, carries a visible load to the player at the bench, and hands it over.
    ///
    /// This is the delivery fiction the storage contract's claim leg needs. A claim chest is written
    /// by its ZDO owner, so a client cannot debit one inside a synchronous `TryConsume`; instead the
    /// nisse performs the transfer visibly, and the craft that asked for it spends the player's own
    /// pockets once the load is in them. Nothing leaves a chest until the nisse is standing at it,
    /// and nothing reaches the player until it is standing at the bench.
    /// </summary>
    public sealed class Fetch
    {
        private enum Phase
        {
            Pickup,
            Loading,
            ToBench,
            HandOver,
            Home,
            Done,
            Aborted,
        }

        /// <summary>One stack the plan will take, from the container it was found in.</summary>
        public struct Pick
        {
            public Container Container;
            public ItemDrop.ItemData Item;
            public int Amount;
        }

        private const float LoadSeconds = 0.35f;
        private const float HandOverSeconds = 0.8f;

        private readonly Claim _claim;
        private readonly NisseActor _nisse;
        private readonly NisseMotion _motion;
        private readonly NisseBundle _bundle;
        private readonly List<Pick> _picks;
        private readonly List<ItemDrop.ItemData> _carried = new List<ItemDrop.ItemData>();
        private readonly List<Vector3> _stops = new List<Vector3>();
        private readonly List<List<Pick>> _stopPicks = new List<List<Pick>>();
        private readonly List<Container> _touched = new List<Container>();
        private readonly long _playerId;
        private readonly long _peerUid;
        private readonly long _token;
        private readonly Vector3 _bench;
        private readonly Vector3 _home;
        private readonly Vector3 _origin;

        private Phase _phase;
        private int _stopIndex;
        private float _timer;
        private bool _reported;
        private bool _legSet;
        private Vector3 _legPoint;
        private float _legArrive;

        public int Carried => _carried.Count;
        public long Token => _token;
        public long PlayerId => _playerId;
        public bool Finished => _phase == Phase.Done || _phase == Phase.Aborted;
        public bool Aborted => _phase == Phase.Aborted;
        public string PhaseName => _phase.ToString();

        public Fetch(Claim claim, NisseActor nisse, long playerId, long peerUid, long token, Vector3 bench,
                     List<Pick> picks)
        {
            _claim = claim;
            _nisse = nisse;
            _playerId = playerId;
            _peerUid = peerUid;
            _token = token;
            _bench = bench;
            _picks = picks ?? new List<Pick>();
            _motion = nisse != null ? nisse.GetComponent<NisseMotion>() : null;
            _bundle = nisse != null ? nisse.GetComponent<NisseBundle>() : null;
            _home = HomePoint(claim, nisse);
            _origin = nisse != null ? nisse.transform.position : Vector3.zero;
            OrderStops();
        }

        public int Planned
        {
            get { return _picks.Count; }
        }

        /// <summary>
        /// Which containers the errand visits, nearest first from where the nisse stood when the
        /// errand began. A stop's stand point is one step from the chest toward that start, the same
        /// geometry a deposit trip uses, and each stop keeps the stacks planned from its own chest.
        /// </summary>
        private void OrderStops()
        {
            var containers = new List<Container>();
            for (var i = 0; i < _picks.Count; i++)
            {
                var container = _picks[i].Container;
                if (container != null && !containers.Contains(container)) containers.Add(container);
            }

            var points = new List<Vector3>();
            for (var i = 0; i < containers.Count; i++) points.Add(StandPoint(containers[i].transform.position, _origin));

            var order = new List<int>();
            var from = _origin;
            var remaining = new List<int>();
            for (var i = 0; i < containers.Count; i++) remaining.Add(i);

            while (remaining.Count > 0)
            {
                var best = 0;
                var bestDistance = Vector3.Distance(from, points[remaining[0]]);
                for (var i = 1; i < remaining.Count; i++)
                {
                    var distance = Vector3.Distance(from, points[remaining[i]]);
                    if (distance >= bestDistance) continue;
                    best = i;
                    bestDistance = distance;
                }

                var index = remaining[best];
                order.Add(index);
                remaining.RemoveAt(best);
                from = points[index];
            }

            for (var i = 0; i < order.Count; i++)
            {
                var container = containers[order[i]];
                var group = new List<Pick>();
                for (var p = 0; p < _picks.Count; p++)
                {
                    if (ReferenceEquals(_picks[p].Container, container)) group.Add(_picks[p]);
                }

                _stops.Add(points[order[i]]);
                _stopPicks.Add(group);
            }
        }

        /// <summary>Called every frame on the server by the runner while the errand is live.</summary>
        public void Tick(float dt)
        {
            if (Finished) return;

            if (_claim == null || _claim.Bowl == null || _nisse == null)
            {
                Abort();
                return;
            }

            switch (_phase)
            {
                case Phase.Pickup:
                    if (_stops.Count == 0)
                    {
                        // Nothing left to pick up: carrying nothing to the bench would be a pantomime.
                        Log.Warn("supply: the fetch plan had no stop left; nothing carried");
                        BeginHome();
                        return;
                    }

                    if (MoveTo(_stops[_stopIndex], 1.2f))
                    {
                        _phase = Phase.Loading;
                        _timer = LoadSeconds;
                    }
                    break;

                case Phase.Loading:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    TakeAt(_stopIndex);
                    _stopIndex++;
                    if (_stopIndex < _stops.Count)
                    {
                        _phase = Phase.Pickup;
                        _legSet = false;
                    }
                    else
                    {
                        _phase = Phase.ToBench;
                        _legSet = false;
                        ShowBundle();
                    }
                    break;

                case Phase.ToBench:
                    if (MoveTo(BenchPoint(), 1.1f) || _motion == null)
                    {
                        if (_motion != null) _nisse.FaceTowards(BenchPoint());
                        _phase = Phase.HandOver;
                        _timer = HandOverSeconds;
                    }
                    break;

                case Phase.HandOver:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    Deliver();
                    BeginHome();
                    break;

                case Phase.Home:
                    if (MoveTo(_home, 0.9f)) _phase = Phase.Done;
                    break;
            }
        }

        private void BeginHome()
        {
            _phase = Phase.Home;
            _legSet = false;
        }

        /// <summary>
        /// Empty the planned stacks out of one chest. This runs on the server, which owns the
        /// container's ZDO unless a player has it open; the write is taken and read back, because an
        /// unsaved removal would leave the stack in the chest *and* on the nisse.
        /// </summary>
        private void TakeAt(int stopIndex)
        {
            if (stopIndex < 0 || stopIndex >= _stopPicks.Count) return;

            var group = _stopPicks[stopIndex];
            var taken = 0;

            for (var i = 0; i < group.Count; i++)
            {
                var pick = group[i];
                var container = pick.Container;
                if (container == null || pick.Item == null) continue;

                var inventory = container.GetInventory();
                if (inventory == null) continue;

                if (!ContainerWrite.EnsureOwned(container, "supply pickup"))
                {
                    Log.Warn("supply: could not take " + container.name + "; the stack stays where it is");
                    continue;
                }

                if (!_touched.Contains(container)) _touched.Add(container);

                // The stack may have been emptied since the plan was made: take what is really
                // there, never more, and let the shortfall show up in the handover.
                if (!inventory.GetAllItems().Contains(pick.Item) || pick.Item.m_stack <= 0)
                {
                    Log.Debug("Flow", "supply: planned stack gone from " + container.name + "; skipping");
                    continue;
                }

                var amount = Mathf.Min(pick.Amount, pick.Item.m_stack);
                if (amount <= 0) continue;

                var clone = pick.Item.Clone();
                clone.m_stack = amount;

                if (!inventory.RemoveItem(pick.Item, amount))
                {
                    Log.Warn("supply: " + container.name + " refused the removal of " + clone.m_shared.m_name);
                    continue;
                }

                _carried.Add(clone);
                taken += amount;
            }

            for (var i = 0; i < _touched.Count; i++)
            {
                ContainerWrite.Persist(_touched[i], "supply");
            }
            _touched.Clear();

            if (taken > 0)
            {
                DiagnosticsFile.Line("[Supply] picked up units=" + taken + " stacks=" + _carried.Count +
                                     " at=" + Where(_stops[stopIndex]) + " claim=" + _claim.BowlId);
                Log.Debug("Flow", "supply pickup units=" + taken + " stacks=" + _carried.Count);
            }
        }

        /// <summary>
        /// The handover. A dedicated server has no inventory for the player, so the load travels to
        /// the owning client as item data and the client puts it in its own pockets. The host and a
        /// single-player game are that client; with nobody to take it, the load is set down at the
        /// bench as real drops — visible, recoverable, and never silently deleted.
        /// </summary>
        private void Deliver()
        {
            if (_carried.Count == 0) return;

            if (_peerUid != 0L)
            {
                var blobs = new List<byte[]>(_carried.Count);
                for (var i = 0; i < _carried.Count; i++)
                {
                    var blob = OrderService.SerialiseItem(_carried[i]);
                    if (blob != null) blobs.Add(blob);
                }

                if (blobs.Count > 0)
                {
                    FetchRpc.Deliver(_peerUid, _token, blobs);
                    DiagnosticsFile.Line("[Supply] handover sent stacks=" + blobs.Count + " units=" + _carried.Count +
                                         " player=" + _playerId + " at=" + Where(BenchPoint()) + " claim=" + _claim.BowlId);
                    Log.Debug("Flow", "supply handover sent stacks=" + blobs.Count + " player=" + _playerId);
                    _carried.Clear();
                    HideBundle();
                    return;
                }
            }

            var player = _playerId != 0L ? Player.GetPlayer(_playerId) : Player.m_localPlayer;
            if (player != null)
            {
                var moved = FetchRpc.AddToInventory(player.GetInventory(), _carried);
                var left = _carried.Count - moved;
                DiagnosticsFile.Line("[Supply] handover local stacks=" + moved + " left=" + left +
                                     " player=" + _playerId + " at=" + Where(BenchPoint()) + " claim=" + _claim.BowlId);
                Log.Debug("Flow", "supply handover local stacks=" + moved + " left=" + left);
                if (left > 0) Drop(_carried.GetRange(moved, left));
                _carried.Clear();
                HideBundle();
                return;
            }

            // No hands: set the load down where the player was standing. The claim's ground scan
            // picks the drops up on its next pass, so a lost connection costs a trip, not the items.
            Drop(_carried);
            _carried.Clear();
            HideBundle();
        }

        private void Drop(List<ItemDrop.ItemData> items)
        {
            if (items == null || items.Count == 0) return;

            var origin = BenchPoint();
            var placed = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || item.m_shared == null) continue;

                var angle = i * 0.9f;
                var offset = new Vector3(Mathf.Cos(angle) * 0.4f, 0.15f, Mathf.Sin(angle) * 0.4f);
                var drop = ItemDrop.DropItem(item, item.m_stack, origin + offset, Quaternion.identity);
                if (drop == null)
                {
                    Log.Error("supply: no drop prefab for " + item.m_shared.m_name + "; the stack was not placed");
                    continue;
                }
                placed++;
            }

            DiagnosticsFile.Line("[Supply] set down stacks=" + placed + " at=" + Where(origin) +
                                 " claim=" + _claim.BowlId);
        }

        private void ShowBundle()
        {
            if (_bundle == null || _carried.Count == 0) return;

            var names = new List<string>(_carried.Count);
            for (var i = 0; i < _carried.Count; i++)
            {
                var item = _carried[i];
                if (item == null || item.m_shared == null) continue;
                if (!names.Contains(item.m_shared.m_name)) names.Add(item.m_shared.m_name);
            }

            _bundle.Show(names);
        }

        private void HideBundle()
        {
            if (_bundle != null) _bundle.Hide();
        }

        /// <summary>
        /// Walks a leg and reports whether the nisse stands there. An unreachable leg is faded
        /// across once — the same rule as a deposit trip — and the errand reports it.
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
                        DiagnosticsFile.Line("[Supply] unreachable at=" + Where(point) + " claim=" + _claim.BowlId);
                    }
                    _motion.FadeAcross(point);
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>Ends the errand without a handover. Carried stacks are set down, never deleted.</summary>
        public void Abort()
        {
            if (Finished) return;

            _phase = Phase.Aborted;
            if (_motion != null) _motion.Stop();
            HideBundle();

            if (_carried.Count > 0)
            {
                Drop(_carried);
                _carried.Clear();
            }

            Log.Debug("Flow", "supply aborted claim=" + (_claim != null ? _claim.BowlId.ToString() : "-"));
            DiagnosticsFile.Line("[Supply] aborted claim=" + (_claim != null ? _claim.BowlId.ToString() : "-") +
                                 " player=" + _playerId);
        }

        /// <summary>The client's own count of what it actually took, for the diagnostics file.</summary>
        public void NoteClientTook(int stacks)
        {
            DiagnosticsFile.Line("[Supply] client took stacks=" + stacks + " token=" + _token +
                                 " player=" + _playerId);
        }

        /// <summary>
        /// Where the handover happens: the player as they were when the errand started, nudged one
        /// step toward the nisse so the walk ends beside them rather than inside them.
        /// </summary>
        private Vector3 BenchPoint()
        {
            return StandPoint(_bench, _origin);
        }

        private static Vector3 StandPoint(Vector3 piece, Vector3 from)
        {
            var direction = piece - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            return piece - direction.normalized * 1.15f;
        }

        private static Vector3 HomePoint(Claim claim, NisseActor nisse)
        {
            if (claim == null || claim.Bowl == null) return nisse != null ? nisse.transform.position : Vector3.zero;

            var bowl = claim.Bowl.transform;
            var forward = bowl.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            return bowl.position + forward.normalized * 1.5f + Vector3.up * 0.1f;
        }

        private static string Where(Vector3 point)
        {
            return "(" + point.x.ToString("0.#") + "," + point.z.ToString("0.#") + ")";
        }
    }
}
