using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Creature;
using Husnisse.Localization;
using Husnisse.Memory;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Machines
{
    /// <summary>
    /// One visible machine errand: the nisse walks to the chest that holds what a machine needs,
    /// takes it while it stands there, carries it to the machine and puts it in; or it walks to a
    /// machine whose work is finished, asks the machine for it, picks up what came out, and carries
    /// it to the chest whose rule claims it.
    ///
    /// Nothing is written from a distance: the chest is debited at the chest, the machine is fed at
    /// the machine, and the output is a real drop on the ground until the nisse's hands close on it.
    /// The design's law is that the creature is the visible cause of every move, and a machine errand
    /// is the plainest case of it — a base that runs itself because something is walking around it.
    /// </summary>
    public sealed class MachineErrand
    {
        private enum Phase
        {
            ToSource,
            Take,
            ToMachine,
            Insert,
            Tap,
            Settle,
            Gather,
            ToTarget,
            File,
            Lay,
            Home,
            Done,
            Aborted,
        }

        private const float LoadSeconds = 0.35f;
        private const float InsertSeconds = 0.8f;
        private const float SettleSeconds = 2.2f;
        private const float GatherRadius = 6f;

        private readonly Claim _claim;
        private readonly NisseActor _nisse;
        private readonly NisseMotion _motion;
        private readonly NisseBundle _bundle;
        private readonly MachineJob _job;
        private readonly Vector3 _home;
        private readonly Vector3 _origin;
        private readonly List<ItemDrop.ItemData> _carried = new List<ItemDrop.ItemData>();
        private readonly HashSet<int> _knownDrops = new HashSet<int>();
        private readonly List<Container> _touched = new List<Container>();
        private readonly List<Vector3> _stops = new List<Vector3>();
        private readonly List<List<ItemDrop.ItemData>> _stopLoads = new List<List<ItemDrop.ItemData>>();

        private Phase _phase;
        private int _stopIndex;
        private int _settleRounds;
        private float _timer;
        private bool _reported;
        private bool _legSet;
        private Vector3 _legPoint;
        private float _legArrive;
        private int _landed;

        public bool Finished => _phase == Phase.Done || _phase == Phase.Aborted;
        public bool Aborted => _phase == Phase.Aborted;
        public int Landed => _landed;
        public int Carried => _carried.Count;
        public MachineJob Job => _job;
        public string PhaseName => _phase.ToString();

        public MachineErrand(Claim claim, NisseActor nisse, MachineJob job)
        {
            _claim = claim;
            _nisse = nisse;
            _job = job;
            _motion = nisse != null ? nisse.GetComponent<NisseMotion>() : null;
            _bundle = nisse != null ? nisse.GetComponent<NisseBundle>() : null;
            _home = HomePoint(claim, nisse);
            _origin = nisse != null ? nisse.transform.position : Vector3.zero;

            if (job == null || job.Machine == null)
            {
                _phase = Phase.Aborted;
                return;
            }

            _phase = job.Kind == MachineJobKind.Collect ? Phase.ToMachine : Phase.ToSource;
        }

        /// <summary>Called every frame on the server by the runner while the errand is live.</summary>
        public void Tick(float dt)
        {
            if (Finished) return;

            if (_claim == null || _claim.Bowl == null || _nisse == null || _job == null || !_job.Machine.Valid)
            {
                Abort();
                return;
            }

            // A pause written while the nisse is walking, or the subsystem being switched off, ends
            // the errand where it stands: the load is set down, never deleted.
            if (_job.Machine.Paused || !TendingOn())
            {
                Abort();
                return;
            }

            switch (_phase)
            {
                case Phase.ToSource:
                    if (MoveTo(SourcePoint(), 1.2f)) Begin(Phase.Take, LoadSeconds);
                    break;

                case Phase.Take:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    Take();
                    if (_carried.Count == 0)
                    {
                        Abort();
                        return;
                    }
                    Begin(Phase.ToMachine, 0f);
                    break;

                case Phase.ToMachine:
                    if (MoveTo(MachinePoint(), _job.Kind == MachineJobKind.Animal ? 1.9f : 1.2f))
                    {
                        _nisse.FaceTowards(_job.Machine.Position);
                        if (_job.Kind == MachineJobKind.Animal) Begin(Phase.Lay, InsertSeconds);
                        else if (_job.Kind == MachineJobKind.Collect) Begin(Phase.Tap, 0f);
                        else Begin(Phase.Insert, InsertSeconds);
                    }
                    break;

                case Phase.Insert:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    Insert();
                    Begin(Phase.Home, 0f);
                    break;

                case Phase.Tap:
                    // Record what is already lying about, so the nisse carries away what its machine
                    // just made and not a stack somebody dropped there yesterday.
                    RememberNearbyDrops();
                    _job.Machine.Collect(MachinePoint());
                    Begin(Phase.Settle, SettleSeconds);
                    break;

                case Phase.Settle:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    Begin(Phase.Gather, 0f);
                    break;

                case Phase.Gather:
                    if (Gather() == 0 && _settleRounds < 2)
                    {
                        // A fermenter taps after its own delay; one more settle is cheaper than a
                        // special case per machine.
                        _settleRounds++;
                        Begin(Phase.Settle, SettleSeconds);
                        break;
                    }

                    if (_carried.Count == 0)
                    {
                        Log.Debug("Machines", "collect: nothing came out of " + _job.Machine.Prefab);
                        Begin(Phase.Home, 0f);
                        break;
                    }

                    if (!PlanStops())
                    {
                        PutDownAll();
                        Begin(Phase.Home, 0f);
                        break;
                    }

                    _stopIndex = 0;
                    Begin(Phase.ToTarget, 0f);
                    break;

                case Phase.ToTarget:
                    if (_stopIndex >= _stops.Count)
                    {
                        Begin(Phase.Home, 0f);
                        break;
                    }
                    if (MoveTo(_stops[_stopIndex], 1.2f)) Begin(Phase.File, 0f);
                    break;

                case Phase.File:
                    FileStop();
                    _stopIndex++;
                    if (_stopIndex < _stops.Count) Begin(Phase.ToTarget, 0f);
                    else Begin(Phase.Home, 0f);
                    break;

                case Phase.Lay:
                    _timer -= dt;
                    if (_timer > 0f) break;
                    Lay();
                    Begin(Phase.Home, 0f);
                    break;

                case Phase.Home:
                    if (MoveTo(_home, 0.9f)) Begin(Phase.Done, 0f);
                    break;
            }
        }

        private static bool TendingOn()
        {
            return HusnisseConfig.TendMachines == null || HusnisseConfig.TendMachines.Value;
        }

        private void Begin(Phase phase, float timer)
        {
            _phase = phase;
            _timer = timer;
            _legSet = false;

            if (phase == Phase.Take || phase == Phase.Gather) ShowBundle();
            if (phase == Phase.Done) HideBundle();
        }

        // ------------------------------------------------------------------ feed

        /// <summary>Empty the planned amount out of the chest, at the chest.</summary>
        private void Take()
        {
            var source = _job.Source;
            if (source == null || string.IsNullOrEmpty(_job.Shared)) return;

            var inventory = source.GetInventory();
            if (inventory == null) return;

            if (!ContainerWrite.Own(source, "machine supply pickup") ||
                !ContainerWrite.Sync(source, "machine supply pickup"))
            {
                Log.Debug("Machines", "machine supply: " + source.name + " is not this machine's to read; the stack stays where it is");
                return;
            }
            _touched.Add(source);

            var stack = inventory.GetItem(_job.Shared);
            if (stack == null || stack.m_stack <= 0)
            {
                Log.Debug("Machines", "machine supply: the planned stack left " + source.name + " before the nisse arrived");
                ContainerWrite.Persist(source, "machine supply");
                _touched.Clear();
                return;
            }

            var amount = Mathf.Min(_job.Amount, stack.m_stack);
            if (amount <= 0) return;

            var clone = stack.Clone();
            clone.m_stack = amount;

            if (!inventory.RemoveItem(stack, amount))
            {
                Log.Warn("machines: " + source.name + " refused the removal of " + _job.Shared);
                ContainerWrite.Persist(source, "machine supply");
                _touched.Clear();
                return;
            }

            _carried.Add(clone);
            ContainerWrite.Persist(source, "machine supply");
            _touched.Clear();

            DiagnosticsFile.Line("[Machines] picked up units=" + amount + " item=" + _job.Shared +
                                 " at=" + Machine.Where(source.transform.position) +
                                 " claim=" + _claim.BowlId);
            Log.Debug("Machines", "picked up " + amount + " " + _job.Shared + " for " + _job.Machine.Prefab);
        }

        /// <summary>
        /// Put the load into the machine, through the machine's own RPC. The demand is re-read at the
        /// machine, so a chest refilled or a machine topped up by a player while the nisse walked is
        /// respected: what does not fit goes back to the chest rather than overfilling a hopper.
        /// </summary>
        private void Insert()
        {
            if (_carried.Count == 0) return;

            var machine = _job.Machine;
            var carried = _carried[0];

            var demand = machine.DemandFor(_job.Shared);
            if (demand == null)
            {
                Log.Debug("Machines", machine.Prefab + " no longer wants " + _job.Shared + "; carrying it back");
                ReturnToSource(carried);
                _carried.Clear();
                HideBundle();
                return;
            }

            var amount = Mathf.Min(carried.m_stack, demand.Want);
            if (amount <= 0)
            {
                ReturnToSource(carried);
                _carried.Clear();
                HideBundle();
                return;
            }

            var accepted = machine.Insert(demand, _job.Shared, amount);
            var left = carried.m_stack - accepted;
            _landed += accepted;

            DiagnosticsFile.Line("[Machines] fed machine=" + machine.Prefab + " item=" + _job.Shared +
                                 " units=" + accepted + " " + demand.Title + "=" + demand.Have + "/" + demand.Cap +
                                 " left=" + left + " at=" + Machine.Where(machine.Position) +
                                 " claim=" + _claim.BowlId);

            if (left > 0)
            {
                var rest = carried.Clone();
                rest.m_stack = left;
                ReturnToSource(rest);
            }

            _carried.Clear();
            HideBundle();
        }

        private void ReturnToSource(ItemDrop.ItemData item)
        {
            var source = _job.Source;
            if (source != null)
            {
                var inventory = source.GetInventory();
                if (inventory != null && ContainerWrite.Own(source, "machine supply return") &&
                    ContainerWrite.Sync(source, "machine supply return") && inventory.AddItem(item))
                {
                    ContainerWrite.Persist(source, "machine supply return");
                    return;
                }
            }

            DropAt(item, MachinePoint());
        }

        /// <summary>Set the portion down for the creature and keep the claim's ground pass off it.</summary>
        private void Lay()
        {
            if (_carried.Count == 0) return;

            var item = _carried[0];
            var point = AnimalPoint();
            var drop = DropAt(item, point);
            if (drop != null)
            {
                FeedGuard.Reserve(drop);
                _landed++;
            }

            DiagnosticsFile.Line("[Machines] fed animal=" + _job.Machine.Prefab + " item=" + _job.Shared +
                                 " units=" + item.m_stack + " at=" + Machine.Where(point) +
                                 " claim=" + _claim.BowlId);

            _carried.Clear();
            HideBundle();
        }

        private ItemDrop DropAt(ItemDrop.ItemData item, Vector3 point)
        {
            if (item == null || item.m_shared == null) return null;

            var drop = ItemDrop.DropItem(item, item.m_stack, point + Vector3.up * 0.2f, Quaternion.identity);
            if (drop == null) Log.Error("machines: no drop prefab for " + item.m_shared.m_name + "; the stack was not set down");
            return drop;
        }

        // ------------------------------------------------------------------ collect

        private void RememberNearbyDrops()
        {
            _knownDrops.Clear();
            foreach (var drop in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (drop == null) continue;
                if (Vector3.Distance(drop.transform.position, _job.Machine.Position) > GatherRadius) continue;
                _knownDrops.Add(drop.GetInstanceID());
            }
        }

        /// <summary>
        /// Take what the machine just produced. Only drops that were not lying there before the tap
        /// are taken, and they are taken as real objects: the drop is destroyed after its stack is in
        /// the nisse's hands, so an item can never be duplicated or vanish.
        /// </summary>
        private int Gather()
        {
            var taken = 0;
            var found = new List<ItemDrop>();

            foreach (var drop in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (drop == null) continue;
                if (Vector3.Distance(drop.transform.position, _job.Machine.Position) > GatherRadius) continue;
                if (_knownDrops.Contains(drop.GetInstanceID())) continue;
                if (drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                if (FeedGuard.IsReserved(drop)) continue;
                found.Add(drop);
            }

            for (var i = 0; i < found.Count; i++)
            {
                var drop = found[i];
                if (drop == null || drop.m_itemData == null) continue;

                var clone = drop.m_itemData.Clone();
                _carried.Add(clone);
                taken += clone.m_stack;
                _landed += clone.m_stack;

                FeedGuard.Forget(drop);
                if (ZNetScene.instance != null) ZNetScene.instance.Destroy(drop.gameObject);
                else Object.Destroy(drop.gameObject);

                DiagnosticsFile.Line("[Machines] collected item=" + clone.m_shared.m_name + " units=" + clone.m_stack +
                                     " machine=" + _job.Machine.Prefab + " at=" + Machine.Where(_job.Machine.Position) +
                                     " claim=" + _claim.BowlId);
            }

            return taken;
        }

        /// <summary>
        /// Where each carried stack goes, nearest first. A stack nothing claims stays on the ground
        /// at the machine — the claim's own pass will offer it again — rather than being pushed into a
        /// chest whose rule said no.
        /// </summary>
        private bool PlanStops()
        {
            _stops.Clear();
            _stopLoads.Clear();

            var memory = MemoryOf(_nisse);

            for (var i = _carried.Count - 1; i >= 0; i--)
            {
                var item = _carried[i];
                if (item == null || item.m_shared == null)
                {
                    _carried.RemoveAt(i);
                    continue;
                }

                var decision = StorageRouter.Decide(_claim, item, memory, null);
                if (!decision.Found || decision.Container == null)
                {
                    Log.Debug("Machines", "collected " + item.m_shared.m_name + " but nowhere claims it (" + decision.Why + ")");
                    DiagnosticsFile.Line("[Machines] unmatched " + item.m_shared.m_name + " why=" + decision.Why +
                                         " claim=" + _claim.BowlId);
                    DropAt(item, MachinePoint());
                    _carried.RemoveAt(i);
                }
            }

            if (_carried.Count == 0) return false;

            var from = MachinePoint();
            var remaining = new List<ItemDrop.ItemData>(_carried);
            var holders = new List<Container>();

            while (remaining.Count > 0)
            {
                var item = remaining[0];
                var container = StorageRouter.Decide(_claim, item, memory, null).Container;
                remaining.RemoveAt(0);
                if (container == null) continue;

                var index = holders.IndexOf(container);
                if (index < 0)
                {
                    holders.Add(container);
                    _stopLoads.Add(new List<ItemDrop.ItemData>());
                    index = holders.Count - 1;
                }
                _stopLoads[index].Add(item);
            }

            if (holders.Count == 0) return false;

            // Nearest-first walk, so a two-chest unload reads as one errand.
            var points = new List<Vector3>();
            for (var i = 0; i < holders.Count; i++) points.Add(StandPoint(holders[i].transform.position, from));

            var order = new List<int>();
            var left = new List<int>();
            for (var i = 0; i < holders.Count; i++) left.Add(i);

            var cursor = from;
            while (left.Count > 0)
            {
                var best = 0;
                var bestDistance = Vector3.Distance(cursor, points[left[0]]);
                for (var i = 1; i < left.Count; i++)
                {
                    var distance = Vector3.Distance(cursor, points[left[i]]);
                    if (distance >= bestDistance) continue;
                    best = i;
                    bestDistance = distance;
                }

                var index = left[best];
                order.Add(index);
                left.RemoveAt(best);
                cursor = points[index];
            }

            _stops.Clear();
            var sortedLoads = new List<List<ItemDrop.ItemData>>();
            for (var i = 0; i < order.Count; i++)
            {
                _stops.Add(points[order[i]]);
                sortedLoads.Add(_stopLoads[order[i]]);
            }
            _stopLoads.Clear();
            _stopLoads.AddRange(sortedLoads);

            return true;
        }

        private void FileStop()
        {
            if (_stopIndex < 0 || _stopIndex >= _stopLoads.Count) return;

            var load = _stopLoads[_stopIndex];
            if (load.Count == 0) return;

            var container = Destination(_stopIndex, load);
            if (container == null) return;

            var inventory = container.GetInventory();
            if (inventory == null) return;

            if (!ContainerWrite.Own(container, "machine output") ||
                !ContainerWrite.Sync(container, "machine output"))
            {
                // A chest the nisse cannot write this trip: the load stays in its hands and is set
                // down beside the machine rather than being added to a copy that will not persist.
                Log.Debug("Machines", "machine output: " + container.name + " is not writable now; the load is not filed");
                return;
            }

            var memory = MemoryOf(_nisse);
            var moved = 0;
            for (var i = 0; i < load.Count; i++)
            {
                var item = load[i];
                if (item == null || item.m_shared == null) continue;

                if (!inventory.AddItem(item))
                {
                    DropAt(item, _stops[_stopIndex]);
                    continue;
                }

                StorageRouter.Learn(container, item.m_shared.m_name, memory);
                _carried.Remove(item);
                moved++;
            }

            ContainerWrite.Persist(container, "machine output");
            DiagnosticsFile.Line("[Machines] filed moved=" + moved + " target=" + container.name +
                                 " at=" + Machine.Where(container.transform.position) +
                                 " claim=" + _claim.BowlId);
            if (moved > 0) HideBundle();
        }

        private Container Destination(int stop, List<ItemDrop.ItemData> load)
        {
            var memory = MemoryOf(_nisse);
            for (var i = 0; i < load.Count; i++)
            {
                if (load[i] == null) continue;
                var decision = StorageRouter.Decide(_claim, load[i], memory, null);
                if (decision.Found && decision.Container != null) return decision.Container;
            }
            return null;
        }

        private void PutDownAll()
        {
            for (var i = 0; i < _carried.Count; i++) DropAt(_carried[i], MachinePoint());
            _carried.Clear();
            HideBundle();
        }

        // ------------------------------------------------------------------ movement

        private Vector3 SourcePoint()
        {
            if (_job.Source == null) return _origin;
            return StandPoint(_job.Source.transform.position, _origin);
        }

        private Vector3 MachinePoint()
        {
            return _job.Machine.StandPoint(_origin);
        }

        private Vector3 AnimalPoint()
        {
            var point = _job.Machine.Position;
            var direction = point - _origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            return point - direction.normalized * 0.8f;
        }

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
                        DiagnosticsFile.Line("[Machines] unreachable at=" + Machine.Where(point) +
                                             " claim=" + _claim.BowlId);
                    }
                    _motion.FadeAcross(point);
                    return false;

                default:
                    return false;
            }
        }

        private void ShowBundle()
        {
            if (_bundle == null || _carried.Count == 0) return;

            var names = new List<string>();
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

        /// <summary>Ends the errand without delivering. Carried stacks are set down, never deleted.</summary>
        public void Abort()
        {
            if (Finished) return;

            _phase = Phase.Aborted;
            if (_motion != null) _motion.Stop();
            HideBundle();
            PutDownAll();

            Log.Debug("Machines", "errand aborted claim=" + (_claim != null ? _claim.BowlId.ToString() : "-"));
        }

        private static ItemMemory MemoryOf(NisseActor nisse)
        {
            var view = nisse != null ? nisse.GetComponent<ZNetView>() : null;
            return view != null && view.GetZDO() != null ? new ItemMemory(view.GetZDO()) : null;
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
    }
}
