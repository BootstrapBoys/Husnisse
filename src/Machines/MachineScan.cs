using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Compat;
using Husnisse.Config;
using Husnisse.Contract;
using Husnisse.Core;
using Husnisse.Storage;
using UnityEngine;

namespace Husnisse.Machines
{
    /// <summary>What the nisse would do at one machine.</summary>
    public enum MachineJobKind
    {
        None = 0,
        Feed = 1,
        Collect = 2,
        Animal = 3,
    }

    /// <summary>
    /// One piece of machine work, fully planned: which machine, which item, how many, and where the
    /// item comes from. A job is a plan, not a reservation — the chest is untouched until the nisse
    /// stands at it, exactly as a supply errand behaves.
    /// </summary>
    public sealed class MachineJob
    {
        public Machine Machine;
        public MachineJobKind Kind;
        public Demand Demand;
        public string Shared;
        public Container Source;
        public int Amount;
        public string Why = string.Empty;

        public string Describe()
        {
            return Machine.Token(Machine != null ? Machine.Kind : MachineKind.None) +
                   " " + (Kind == MachineJobKind.Collect ? "collect" : "feed") +
                   " item=" + (Shared ?? "-") +
                   " n=" + Amount +
                   " machine=" + (Machine != null ? Machine.Where(Machine.Position) : "-");
        }
    }

    /// <summary>
    /// Finds the machines a claim contains and decides what the nisse should do next. Read-only: the
    /// scan answers a question and never writes a ZDO, so `husnisse_machines` and the scheduler see
    /// the same world.
    ///
    /// A machine is tended only while it is inside the claim and on ground the claim owns — the same
    /// ward rule that governs containers, because a nisse must not stoke another tribe's kiln any
    /// more than it may file into their chests.
    /// </summary>
    public static class MachineScan
    {
        public static List<Machine> InClaim(Claim claim)
        {
            return Scan(claim, false);
        }

        /// <summary>
        /// Every machine the claim contains, paused ones included — what the console and the ledger
        /// list, because a paused machine has to be visible to be resumed.
        /// </summary>
        public static List<Machine> All(Claim claim)
        {
            return Scan(claim, true);
        }

        private static List<Machine> Scan(Claim claim, bool includePaused)
        {
            var machines = new List<Machine>();
            if (claim == null || claim.Suppressed || claim.Bowl == null) return machines;
            if (HusnisseConfig.TendMachines == null || !HusnisseConfig.TendMachines.Value) return machines;

            Collect<Smelter>(claim, machines, includePaused);
            Collect<Fermenter>(claim, machines, includePaused);
            Collect<CookingStation>(claim, machines, includePaused);
            Collect<Beehive>(claim, machines, includePaused);
            Collect<SapCollector>(claim, machines, includePaused);
            if (HusnisseConfig.RefuelFires == null || HusnisseConfig.RefuelFires.Value) Collect<Fireplace>(claim, machines, includePaused);
            if (HusnisseConfig.FeedAnimals == null || HusnisseConfig.FeedAnimals.Value) Collect<Tameable>(claim, machines, includePaused);

            machines.Sort(delegate(Machine a, Machine b)
            {
                var first = a.Position;
                var second = b.Position;
                if (Mathf.Abs(first.x - second.x) > 0.01f) return first.x < second.x ? -1 : 1;
                if (Mathf.Abs(first.z - second.z) > 0.01f) return first.z < second.z ? -1 : 1;
                return first.y.CompareTo(second.y);
            });
            return machines;
        }

        private static void Collect<T>(Claim claim, List<Machine> machines, bool includePaused) where T : Component
        {
            foreach (var component in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (component == null) continue;

                var machine = Machine.For(component);
                if (machine == null || !machine.Valid) continue;
                if (!claim.Contains(machine.Position)) continue;
                if (!Allowed(claim, machine.Position)) continue;
                if (!includePaused && machine.Paused) continue;
                if (AlreadyAdded(machines, machine)) continue;

                machines.Add(machine);
            }
        }

        /// <summary>One piece, one machine: a windmill is a smelter with a rotor, not two jobs.</summary>
        private static bool AlreadyAdded(List<Machine> machines, Machine machine)
        {
            for (var i = 0; i < machines.Count; i++)
            {
                if (ReferenceEquals(machines[i].View, machine.View)) return true;
            }
            return false;
        }

        /// <summary>Ground beats ownership, the rule containers already obey.</summary>
        public static bool Allowed(Claim claim, Vector3 point)
        {
            if (claim == null) return false;
            if (HusnisseConfig.RespectWards == null || !HusnisseConfig.RespectWards.Value) return true;
            return !WardIsLoveCompat.BlocksOwner(claim.OwnerId, point);
        }

        /// <summary>
        /// The next errand, in the order a base cares about: finished goods first (they are the point
        /// of the machine), then fuel (nothing runs without it), then input, then a creature's meal.
        /// Nearest-first inside each kind, so the nisse works the room it is standing in.
        /// </summary>
        public static MachineJob Next(Claim claim, Vector3 from)
        {
            var machines = InClaim(claim);
            if (machines.Count == 0) return null;

            var collectAt = HusnisseConfig.CollectAt != null ? Mathf.Max(1, HusnisseConfig.CollectAt.Value) : 1;
            var collectWanted = HusnisseConfig.CollectOutput == null || HusnisseConfig.CollectOutput.Value;

            if (collectWanted)
            {
                var job = Nearest(machines, from, delegate(Machine machine)
                {
                    return machine.Ready() >= collectAt ? new MachineJob
                    {
                        Machine = machine,
                        Kind = MachineJobKind.Collect,
                        Amount = machine.Ready(),
                        Why = "ready=" + machine.Ready(),
                    } : null;
                });
                if (job != null) return job;
            }

            var feed = Nearest(machines, from, delegate(Machine machine)
            {
                // Fuel outranks input: a furnace with ore and no coal is a cold furnace.
                return Plan(claim, machine, "fuel", from) ?? Plan(claim, machine, "input", from) ??
                       Plan(claim, machine, "food", from);
            });
            return feed;
        }

        private static MachineJob Nearest(List<Machine> machines, Vector3 from, System.Func<Machine, MachineJob> make)
        {
            MachineJob best = null;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < machines.Count; i++)
            {
                var job = make(machines[i]);
                if (job == null) continue;

                var distance = Vector3.Distance(from, machines[i].Position);
                if (best != null && distance >= bestDistance) continue;
                best = job;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// Turns one demand into a job with a real source: the first acceptable item the claim can
        /// actually give for this purpose, from the nearest container that holds it. The amount is
        /// trimmed to what that container holds and the machine still wants.
        /// </summary>
        private static MachineJob Plan(Claim claim, Machine machine, string title, Vector3 from)
        {
            var demands = machine.Demands();
            for (var d = 0; d < demands.Count; d++)
            {
                var demand = demands[d];
                if (demand.Title != title || demand.Want <= 0) continue;

                var containers = SupplyService.Eligible(claim, demand.Purpose);
                if (containers.Count == 0)
                {
                    // Silent on purpose: a base whose every chest has this purpose switched off is a
                    // configuration, not an error, and the pump runs every few seconds — a line here
                    // would be the loudest thing in the log.
                    return null;
                }

                string shared;
                Container source;
                int amount;
                if (!FindSource(machine, demand, containers, from, out shared, out source, out amount)) continue;

                return new MachineJob
                {
                    Machine = machine,
                    Kind = title == "food" ? MachineJobKind.Animal : MachineJobKind.Feed,
                    Demand = demand,
                    Shared = shared,
                    Source = source,
                    Amount = amount,
                    Why = demand.Title + " " + demand.Have + "/" + demand.Cap,
                };
            }
            return null;
        }

        /// <summary>
        /// Where the item comes from. Preference inside a demand's accepted names: what the machine
        /// is already working on (filling a batch beats switching ores mid-queue), then the first
        /// accepted name, so the order is the prefab's own and a base with one ore behaves the same
        /// every time.
        /// </summary>
        private static bool FindSource(Machine machine, Demand demand, List<Container> containers, Vector3 from,
                                       out string shared, out Container source, out int amount)
        {
            shared = null;
            source = null;
            amount = 0;

            var names = Preferred(machine, demand);

            for (var n = 0; n < names.Count; n++)
            {
                var name = names[n];
                Container best = null;
                var bestDistance = float.MaxValue;
                var bestHave = 0;

                for (var c = 0; c < containers.Count; c++)
                {
                    var container = containers[c];
                    if (container == null) continue;
                    var inventory = container.GetInventory();
                    if (inventory == null) continue;

                    var have = inventory.CountItems(name, -1, true);
                    if (have <= 0) continue;

                    var distance = Vector3.Distance(from, container.transform.position);
                    if (best != null && distance >= bestDistance) continue;
                    best = container;
                    bestDistance = distance;
                    bestHave = have;
                }

                if (best == null) continue;

                shared = name;
                source = best;
                amount = Mathf.Min(demand.Want, bestHave);
                return amount > 0;
            }

            return false;
        }

        /// <summary>The demand's names, with a processor's queued ore moved to the front.</summary>
        private static List<string> Preferred(Machine machine, Demand demand)
        {
            var names = new List<string>(demand.Shared);
            if (machine == null || machine.Kind != MachineKind.Processor) return names;

            var zdo = machine.Zdo;
            if (zdo == null) return names;

            var queued = zdo.GetString(ZDOVars.s_item0);
            if (string.IsNullOrEmpty(queued)) return names;

            // The ZDO stores the ore's prefab name; the demand lists shared names.
            var shared = SharedOfPrefab(queued);
            if (string.IsNullOrEmpty(shared)) return names;

            var index = names.IndexOf(shared);
            if (index <= 0) return names;

            names.RemoveAt(index);
            names.Insert(0, shared);
            return names;
        }

        /// <summary>An item prefab name to its shared name (`CopperScrap` → `$item_copperore`).</summary>
        public static string SharedOfPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return string.Empty;

            var database = ObjectDB.instance;
            if (database == null) return string.Empty;

            var prefab = database.GetItemPrefab(prefabName);
            if (prefab == null) return string.Empty;

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) return string.Empty;
            return drop.m_itemData.m_shared.m_name;
        }

        /// <summary>One machine of a claim by prefab name and index, in the scan's own order.</summary>
        public static Machine Find(Claim claim, string prefab, int index)
        {
            if (string.IsNullOrEmpty(prefab)) return null;

            var position = 0;
            foreach (var machine in All(claim))
            {
                if (machine.Prefab != prefab) continue;
                if (position == index) return machine;
                position++;
            }
            return null;
        }

        /// <summary>The live machine nearest a point, for a request addressed by position.</summary>
        public static Machine At(Vector3 point, float radius = 1.5f)
        {
            Machine best = null;
            var bestDistance = radius;

            var all = new List<Component>();
            foreach (var component in Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None)) all.Add(component);
            foreach (var component in Object.FindObjectsByType<Fermenter>(FindObjectsSortMode.None)) all.Add(component);
            foreach (var component in Object.FindObjectsByType<CookingStation>(FindObjectsSortMode.None)) all.Add(component);
            foreach (var component in Object.FindObjectsByType<Beehive>(FindObjectsSortMode.None)) all.Add(component);
            foreach (var component in Object.FindObjectsByType<SapCollector>(FindObjectsSortMode.None)) all.Add(component);
            foreach (var component in Object.FindObjectsByType<Fireplace>(FindObjectsSortMode.None)) all.Add(component);
            foreach (var component in Object.FindObjectsByType<Tameable>(FindObjectsSortMode.None)) all.Add(component);

            for (var i = 0; i < all.Count; i++)
            {
                var machine = Machine.For(all[i]);
                if (machine == null || !machine.Valid) continue;

                var distance = Vector3.Distance(machine.Position, point);
                if (distance > bestDistance) continue;
                best = machine;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>A feed plan for one machine, for the console's forced errand.</summary>
        public static MachineJob PlanFeed(Claim claim, Machine machine, Vector3 from)
        {
            if (claim == null || machine == null) return null;
            return Plan(claim, machine, "fuel", from) ?? Plan(claim, machine, "input", from) ?? Plan(claim, machine, "food", from);
        }

        /// <summary>One line per machine for `husnisse_machines` and the diagnostics file.</summary>
        public static List<string> Describe(Claim claim)
        {
            var lines = new List<string>();
            if (claim == null) return lines;

            foreach (var machine in All(claim))
            {
                lines.Add("machine claim=" + claim.BowlId + " " + machine.Describe());
            }
            return lines;
        }

        public static int Count(Claim claim)
        {
            return All(claim).Count;
        }

        /// <summary>The summary line `husnisse_status` prints beside the queue.</summary>
        public static string Summary(Claim claim)
        {
            var machines = All(claim);
            var paused = 0;
            var ready = 0;
            var wants = 0;

            for (var i = 0; i < machines.Count; i++)
            {
                if (machines[i].Paused)
                {
                    paused++;
                    continue;
                }
                if (machines[i].Ready() > 0) ready++;
                if (machines[i].Demands().Count > 0) wants++;
            }

            return "machines_tended=" + (machines.Count - paused) + " machines_paused=" + paused +
                   " machines_ready=" + ready + " machines_wanting=" + wants;
        }
    }
}
