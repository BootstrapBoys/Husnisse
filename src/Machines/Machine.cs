using System;
using System.Collections.Generic;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Storage;
using Husnisse.Util;
using UnityEngine;

namespace Husnisse.Machines
{
    /// <summary>What a piece is, as far as tending it goes.</summary>
    public enum MachineKind : byte
    {
        None = 0,

        /// <summary>A smelter, blast furnace, charcoal kiln, eitr refinery or windmill: fuel plus a queue.</summary>
        Processor = 1,

        /// <summary>A fermentation barrel: one mash, one tap.</summary>
        Fermenter = 2,

        /// <summary>A cooking station or oven: slots, sometimes fuel.</summary>
        Cooker = 3,

        /// <summary>A beehive: honey accumulates, nothing goes in.</summary>
        Hive = 4,

        /// <summary>A sap extractor: resin accumulates, nothing goes in.</summary>
        Tap = 5,

        /// <summary>A fire pit, hearth, bonfire or standing torch: wood goes in, warmth comes out.</summary>
        Fire = 6,

        /// <summary>A tameable creature: a portion laid down, breeding keeps going.</summary>
        Animal = 7,
    }

    /// <summary>
    /// One thing the nisse could top up. The demand is computed from the machine's own live state and
    /// its own maximum, then trimmed by the configured cap, so a request can never overfill a smelter
    /// past what its prefab allows or past what the owner asked for.
    /// </summary>
    public sealed class Demand
    {
        /// <summary>"fuel", "input", "food" — the token the logs and the ledger print.</summary>
        public string Title;

        public SupplyPurpose Purpose;

        /// <summary>Every item that would satisfy it, in preference order. A machine that accepts any
        /// of three ores is one demand with three names, not three demands.</summary>
        public List<string> Shared = new List<string>();

        /// <summary>How many units to add to reach the cap. Always &gt; 0 for a live demand.</summary>
        public int Want;

        /// <summary>What the machine holds now, and what it is allowed to hold.</summary>
        public int Have;
        public int Cap;
        public int Max;

        public string Describe()
        {
            return Title + "=" + Have + "/" + Cap + " want=" + Want;
        }
    }

    /// <summary>
    /// A machine the nisse tends, wrapped so the rest of the mod never has to know which vanilla
    /// component it is talking to.
    ///
    /// Every interaction goes through the component's **own RPC**, not through a ZDO write of ours:
    /// `RPC_AddOre`, `RPC_AddFuel`, `RPC_AddItem`, `RPC_Tap`, `RPC_Extract`, `RPC_EmptyProcessed`,
    /// `RPC_RemoveDoneItem`. That is the path a player's interact takes, so the machine's own rules
    /// (what it accepts, its ceilings, its effects, its cheat flags) still decide what happens — and
    /// every transfer plays the prefab's own effect on every peer, which is the visible half of the
    /// design. We read state from the ZDO, never from private fields, so a machine we do not own is
    /// still described correctly on a client.
    /// </summary>
    public sealed class Machine
    {
        /// <summary>Wood a base should not burn. The list is shared names, the same tokens the
        /// routing table uses; it is only ever consulted for a machine's declared fuel, so a modded
        /// furnace that asks for fine wood is refused rather than silently fed the good timber.</summary>
        private static readonly string[] Valuable =
        {
            "$item_finewood",
            "$item_corewood",
            "$item_elderbark",
            "$item_yggdrasilwood",
            "$item_ancientbark",
        };

        public MachineKind Kind;
        public Component Component;
        public ZNetView View;
        public Transform Transform;

        public string Prefab
        {
            get { return Transform != null ? Utils.GetPrefabName(Transform.gameObject) : "?"; }
        }

        public Vector3 Position
        {
            get { return Transform != null ? Transform.position : Vector3.zero; }
        }

        public ZDO Zdo
        {
            get { return View != null ? View.GetZDO() : null; }
        }

        public bool Valid
        {
            get { return Kind != MachineKind.None && Component != null && View != null && Zdo != null; }
        }

        // ------------------------------------------------------------------ classification

        /// <summary>The machine a component belongs to, or null when it is not one.</summary>
        public static Machine For(Component component)
        {
            if (component == null) return null;

            var kind = KindOfType(component.GetType());
            if (kind == MachineKind.None) return null;

            var view = component.GetComponent<ZNetView>() ?? component.GetComponentInParent<ZNetView>();
            if (view == null) return null;

            return new Machine
            {
                Kind = kind,
                Component = component,
                View = view,
                // A component can sit under the piece root (a Smelter is usually the root itself, a
                // Tameable on a child hierarchy); the ZDO is the piece's, so the position follows it.
                Transform = view.transform,
            };
        }

        /// <summary>
        /// The machine kind a GameObject carries, used by the container filter to keep a machine's own
        /// bin (the incinerator's) out of the storage network. Deliberately checks parents: a piece
        /// whose Container sits on a child object is still the machine's bin.
        /// </summary>
        public static MachineKind KindOf(GameObject go)
        {
            if (go == null) return MachineKind.None;

            if (go.GetComponentInParent<Smelter>() != null) return MachineKind.Processor;
            if (go.GetComponentInParent<Fermenter>() != null) return MachineKind.Fermenter;
            if (go.GetComponentInParent<CookingStation>() != null) return MachineKind.Cooker;
            if (go.GetComponentInParent<Beehive>() != null) return MachineKind.Hive;
            if (go.GetComponentInParent<SapCollector>() != null) return MachineKind.Tap;
            if (go.GetComponentInParent<Fireplace>() != null) return MachineKind.Fire;
            if (go.GetComponentInParent<Tameable>() != null) return MachineKind.Animal;
            return MachineKind.None;
        }

        private static MachineKind KindOfType(Type type)
        {
            if (type == typeof(Smelter)) return MachineKind.Processor;
            if (type == typeof(Fermenter)) return MachineKind.Fermenter;
            if (type == typeof(CookingStation)) return MachineKind.Cooker;
            if (type == typeof(Beehive)) return MachineKind.Hive;
            if (type == typeof(SapCollector)) return MachineKind.Tap;
            if (type == typeof(Fireplace)) return MachineKind.Fire;
            if (type == typeof(Tameable)) return MachineKind.Animal;
            return MachineKind.None;
        }

        /// <summary>
        /// Whether this object is a machine's own bin rather than storage. The incinerator is the one
        /// vanilla piece with a `Container` that exists to be emptied by hand and destroyed; filing
        /// into it, or counting it as a chest the base may draw from, would feed a player's stock to
        /// a furnace. The test is by component, so a modded machine with a hopper is caught too when
        /// its prefab carries one of these pieces.
        /// </summary>
        public static bool IsMachineBin(GameObject go)
        {
            if (go == null) return false;

            if (go.GetComponentInParent<Incinerator>() != null) return true;
            if (go.GetComponentInParent<Smelter>() != null) return true;
            if (go.GetComponentInParent<Fermenter>() != null) return true;
            if (go.GetComponentInParent<CookingStation>() != null) return true;
            if (go.GetComponentInParent<Beehive>() != null) return true;
            if (go.GetComponentInParent<SapCollector>() != null) return true;
            return false;
        }

        public static string Token(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.Processor: return "processor";
                case MachineKind.Fermenter: return "fermenter";
                case MachineKind.Cooker: return "cooker";
                case MachineKind.Hive: return "hive";
                case MachineKind.Tap: return "tap";
                case MachineKind.Fire: return "fire";
                case MachineKind.Animal: return "animal";
                default: return "none";
            }
        }

        // ------------------------------------------------------------------ pause

        /// <summary>
        /// Whether the nisse leaves this one alone. A pause is per machine and lives in its ZDO, so
        /// it survives a relog and every peer agrees: the ledger writes one key and the server's scan
        /// reads the same key, with no second local flag to drift.
        /// </summary>
        public bool Paused
        {
            get
            {
                var zdo = Zdo;
                return zdo != null && zdo.GetBool(ZdoKeys.MachinePause, false);
            }
        }

        public void SetPaused(bool paused, string who)
        {
            var zdo = Zdo;
            if (zdo == null) return;

            zdo.Set(ZdoKeys.MachinePause, paused);
            Log.Debug("Machines", (paused ? "paused " : "resumed ") + Prefab + " at=" + Where(Position) + " by " + who);
        }

        // ------------------------------------------------------------------ state

        /// <summary>What this machine would like, right now, after its caps. Empty means leave it alone.</summary>
        public List<Demand> Demands()
        {
            var demands = new List<Demand>();
            if (!Valid) return demands;

            switch (Kind)
            {
                case MachineKind.Processor:
                    ProcessorDemands(demands);
                    break;
                case MachineKind.Fermenter:
                    FermenterDemands(demands);
                    break;
                case MachineKind.Cooker:
                    CookerDemands(demands);
                    break;
                case MachineKind.Fire:
                    FireDemands(demands);
                    break;
                case MachineKind.Animal:
                    AnimalDemands(demands);
                    break;
            }
            return demands;
        }

        private void ProcessorDemands(List<Demand> demands)
        {
            var smelter = Component as Smelter;
            if (smelter == null) return;

            var fuel = FuelDemand(smelter.m_fuelItem, smelter.m_maxFuel,
                Mathf.FloorToInt(Zdo.GetFloat(ZDOVars.s_fuel)), "fuel");
            if (fuel != null) demands.Add(fuel);

            if (smelter.m_maxOre <= 0) return;

            var accepted = new List<string>();
            var conversions = smelter.m_conversion;
            if (conversions != null)
            {
                for (var i = 0; i < conversions.Count; i++)
                {
                    var conversion = conversions[i];
                    if (conversion == null || conversion.m_from == null) continue;
                    var shared = conversion.m_from.m_itemData != null ? conversion.m_from.m_itemData.m_shared : null;
                    if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                    if (!accepted.Contains(shared.m_name)) accepted.Add(shared.m_name);
                }
            }
            if (accepted.Count == 0) return;

            var have = Zdo.GetInt(ZDOVars.s_queued);
            var cap = Cap(smelter.m_maxOre, HusnisseConfig.MachineInputPercent.Value);
            if (have >= cap) return;

            demands.Add(new Demand
            {
                Title = "input",
                Purpose = SupplyPurpose.Production,
                Shared = accepted,
                Want = cap - have,
                Have = have,
                Cap = cap,
                Max = smelter.m_maxOre,
            });
        }

        private void FermenterDemands(List<Demand> demands)
        {
            var fermenter = Component as Fermenter;
            if (fermenter == null) return;
            if (Zdo.GetInt(ZDOVars.s_content) != 0) return;   // one mash at a time, and it is full

            var accepted = new List<string>();
            var conversions = fermenter.m_conversion;
            if (conversions != null)
            {
                for (var i = 0; i < conversions.Count; i++)
                {
                    var conversion = conversions[i];
                    if (conversion == null || conversion.m_from == null) continue;
                    var shared = conversion.m_from.m_itemData != null ? conversion.m_from.m_itemData.m_shared : null;
                    if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                    if (!accepted.Contains(shared.m_name)) accepted.Add(shared.m_name);
                }
            }
            if (accepted.Count == 0) return;

            demands.Add(new Demand
            {
                Title = "input",
                Purpose = SupplyPurpose.Production,
                Shared = accepted,
                Want = 1,
                Have = 0,
                Cap = 1,
                Max = 1,
            });
        }

        private void CookerDemands(List<Demand> demands)
        {
            var station = Component as CookingStation;
            if (station == null) return;

            if (station.m_useFuel)
            {
                var fuel = FuelDemand(station.m_fuelItem, station.m_maxFuel,
                    Mathf.FloorToInt(Zdo.GetFloat(ZDOVars.s_fuel)), "fuel");
                if (fuel != null) demands.Add(fuel);
            }

            var slots = station.m_slots != null ? station.m_slots.Length : 0;
            if (slots <= 0) return;

            var free = 0;
            for (var i = 0; i < slots; i++)
            {
                if (string.IsNullOrEmpty(Zdo.GetString("slot" + i))) free++;
            }

            var cap = Cap(slots, HusnisseConfig.MachineInputPercent.Value);
            var filled = slots - free;
            if (filled >= cap || free == 0) return;

            var accepted = new List<string>();
            var conversions = station.m_conversion;
            if (conversions != null)
            {
                for (var i = 0; i < conversions.Count; i++)
                {
                    var conversion = conversions[i];
                    if (conversion == null || conversion.m_from == null) continue;
                    var shared = conversion.m_from.m_itemData != null ? conversion.m_from.m_itemData.m_shared : null;
                    if (shared == null || string.IsNullOrEmpty(shared.m_name)) continue;
                    if (!accepted.Contains(shared.m_name)) accepted.Add(shared.m_name);
                }
            }
            if (accepted.Count == 0) return;

            demands.Add(new Demand
            {
                Title = "input",
                Purpose = SupplyPurpose.Production,
                Shared = accepted,
                Want = Mathf.Min(cap - filled, free),
                Have = filled,
                Cap = cap,
                Max = slots,
            });
        }

        private void FireDemands(List<Demand> demands)
        {
            var fire = Component as Fireplace;
            if (fire == null) return;
            if (fire.m_infiniteFuel) return;

            var fuel = FuelDemand(fire.m_fuelItem, Mathf.RoundToInt(fire.m_maxFuel),
                Mathf.FloorToInt(Zdo.GetFloat(ZDOVars.s_fuel)), "fuel");
            if (fuel != null) demands.Add(fuel);
        }

        private void AnimalDemands(List<Demand> demands)
        {
            var tameable = Component as Tameable;
            if (tameable == null) return;

            var character = Component.GetComponent<Character>();
            var ai = Component.GetComponent<MonsterAI>();
            if (character == null || ai == null) return;

            // A hungry creature is a hungry creature whether or not it is tame: an untamed one is
            // being tamed by the feeding, a tame one needs the meal before it will breed.
            if (!tameable.IsHungry()) return;

            var accepted = new List<string>();
            var items = ai.m_consumeItems;
            if (items != null)
            {
                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null || item.m_itemData == null || item.m_itemData.m_shared == null) continue;
                    var shared = item.m_itemData.m_shared.m_name;
                    if (!string.IsNullOrEmpty(shared) && !accepted.Contains(shared)) accepted.Add(shared);
                }
            }
            if (accepted.Count == 0) return;

            demands.Add(new Demand
            {
                Title = "food",
                Purpose = SupplyPurpose.Feed,
                Shared = accepted,
                Want = 1,
                Have = 0,
                Cap = 1,
                Max = 1,
            });
        }

        /// <summary>
        /// A fuel demand, or null when the machine is full enough or its fuel is one the base has
        /// decided not to burn. The valuable-wood guard lives here rather than in the planner so a
        /// machine can never be fuelled by a path that forgot to ask.
        /// </summary>
        private Demand FuelDemand(ItemDrop fuelItem, int max, int have, string title)
        {
            if (fuelItem == null || fuelItem.m_itemData == null || fuelItem.m_itemData.m_shared == null) return null;
            if (max <= 0) return null;

            var shared = fuelItem.m_itemData.m_shared.m_name;
            if (string.IsNullOrEmpty(shared)) return null;

            if (HusnisseConfig.ProtectValuableWood != null && HusnisseConfig.ProtectValuableWood.Value && IsValuable(shared))
            {
                Log.Debug("Machines", Prefab + " asks for " + shared + " as fuel; refused as valuable wood");
                return null;
            }

            var cap = Cap(max, HusnisseConfig.MachineFuelPercent.Value);
            if (have >= cap) return null;

            var demand = new Demand
            {
                Title = title,
                Purpose = SupplyPurpose.Fuel,
                Want = cap - have,
                Have = have,
                Cap = cap,
                Max = max,
            };
            demand.Shared.Add(shared);
            return demand;
        }

        public static bool IsValuable(string sharedName)
        {
            for (var i = 0; i < Valuable.Length; i++)
            {
                if (string.Equals(Valuable[i], sharedName, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static int Cap(int max, int percent)
        {
            if (max <= 0) return 0;
            var scaled = Mathf.CeilToInt(max * Mathf.Clamp(percent, 1, 100) / 100f);
            return Mathf.Clamp(scaled, 1, max);
        }

        /// <summary>How much finished work is waiting to be carried away.</summary>
        public int Ready()
        {
            var zdo = Zdo;
            if (zdo == null) return 0;

            switch (Kind)
            {
                case MachineKind.Processor:
                {
                    var ore = zdo.GetString(ZDOVars.s_spawnOre);
                    return string.IsNullOrEmpty(ore) ? 0 : zdo.GetInt(ZDOVars.s_spawnAmount);
                }

                case MachineKind.Fermenter:
                {
                    var fermenter = Component as Fermenter;
                    if (fermenter == null) return 0;
                    if (zdo.GetInt(ZDOVars.s_content) == 0) return 0;

                    var start = zdo.GetLong(ZDOVars.s_startTime, 0L);
                    if (start <= 0L || ZNet.instance == null) return 0;
                    var elapsed = (ZNet.instance.GetTime() - new DateTime(start)).TotalSeconds;
                    return elapsed > fermenter.m_fermentationDuration ? 1 : 0;
                }

                case MachineKind.Cooker:
                {
                    var station = Component as CookingStation;
                    if (station == null || station.m_slots == null) return 0;

                    var done = 0;
                    for (var i = 0; i < station.m_slots.Length; i++)
                    {
                        if (string.IsNullOrEmpty(zdo.GetString("slot" + i))) continue;
                        if (zdo.GetInt("slotstatus" + i) == 0) continue;   // 0 = still cooking
                        done++;
                    }
                    return done;
                }

                case MachineKind.Hive:
                case MachineKind.Tap:
                    return zdo.GetInt(ZDOVars.s_level);

                default:
                    return 0;
            }
        }

        // ------------------------------------------------------------------ work

        /// <summary>Whether the named item would satisfy one of this machine's demands.</summary>
        public Demand DemandFor(string sharedName)
        {
            var demands = Demands();
            for (var i = 0; i < demands.Count; i++)
            {
                if (demands[i].Shared.Contains(sharedName)) return demands[i];
            }
            return null;
        }

        /// <summary>
        /// Puts a stack into the machine through its own RPC. The amount is trimmed to the demand the
        /// planner asked for, so a stale plan can never overfill: the machine's own ceiling is checked
        /// again by the RPC, which is the point of using it.
        /// </summary>
        public int Insert(Demand demand, string sharedName, int amount)
        {
            if (!Valid || demand == null || amount <= 0) return 0;

            var prefab = PrefabOf(sharedName);
            if (string.IsNullOrEmpty(prefab))
            {
                Log.Warn("machines: no item prefab for " + sharedName + "; cannot feed " + Prefab);
                return 0;
            }

            switch (Kind)
            {
                case MachineKind.Processor:
                {
                    var smelter = Component as Smelter;
                    if (smelter == null) return 0;

                    if (demand.Title == "fuel")
                    {
                        for (var i = 0; i < amount; i++) View.InvokeRPC("RPC_AddFuel");
                        return amount;
                    }

                    for (var i = 0; i < amount; i++) View.InvokeRPC("RPC_AddOre", prefab, false);
                    return amount;
                }

                case MachineKind.Fermenter:
                    View.InvokeRPC("RPC_AddItem", prefab.GetStableHashCode(), false);
                    return 1;

                case MachineKind.Cooker:
                {
                    if (demand.Title == "fuel")
                    {
                        for (var i = 0; i < amount; i++) View.InvokeRPC("RPC_AddFuel");
                        return amount;
                    }

                    for (var i = 0; i < amount; i++) View.InvokeRPC("RPC_AddItem", prefab, false);
                    return amount;
                }

                case MachineKind.Fire:
                    View.InvokeRPC("RPC_AddFuelAmount", (float)amount);
                    return amount;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Asks the machine for its finished work. The machine spawns the items itself — at its
        /// output point, toward the point the nisse is standing at — so what the nisse carries away
        /// is a real world object that another player could have picked up first.
        /// </summary>
        public bool Collect(Vector3 standPoint)
        {
            if (!Valid) return false;
            if (Ready() <= 0) return false;

            switch (Kind)
            {
                case MachineKind.Processor:
                    View.InvokeRPC("RPC_EmptyProcessed");
                    return true;

                case MachineKind.Fermenter:
                    View.InvokeRPC("RPC_Tap");
                    return true;

                case MachineKind.Cooker:
                {
                    var station = Component as CookingStation;
                    if (station == null || station.m_slots == null) return false;

                    var asked = 0;
                    for (var i = 0; i < station.m_slots.Length; i++)
                    {
                        if (string.IsNullOrEmpty(Zdo.GetString("slot" + i))) continue;
                        if (Zdo.GetInt("slotstatus" + i) == 0) continue;
                        View.InvokeRPC("RPC_RemoveDoneItem", standPoint, 1);
                        asked++;
                    }
                    return asked > 0;
                }

                case MachineKind.Hive:
                case MachineKind.Tap:
                    View.InvokeRPC("RPC_Extract");
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Where the nisse should stand to work at this machine.</summary>
        public Vector3 StandPoint(Vector3 from)
        {
            var point = Position;
            var direction = point - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            return point - direction.normalized * 1.3f;
        }

        /// <summary>
        /// The machine's own prefab name for an item, which is what every vanilla RPC takes. The
        /// item's drop prefab is the direct answer; the shared-data lookup is the fallback for an
        /// item whose prefab reference was lost in a save round trip.
        /// </summary>
        public static string PrefabOf(string sharedName)
        {
            if (string.IsNullOrEmpty(sharedName)) return string.Empty;

            var database = ObjectDB.instance;
            if (database == null) return string.Empty;

            var items = database.m_items;
            if (items == null) return string.Empty;

            for (var i = 0; i < items.Count; i++)
            {
                var prefab = items[i];
                if (prefab == null) continue;
                var drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;
                if (drop.m_itemData.m_shared.m_name == sharedName) return prefab.name;
            }
            return string.Empty;
        }

        // ------------------------------------------------------------------ reporting

        public string Describe()
        {
            var text = Token(Kind) + " " + Prefab + " at=" + Where(Position);
            if (Paused) text += " paused";
            text += " ready=" + Ready();

            var demands = Demands();
            for (var i = 0; i < demands.Count; i++) text += " " + demands[i].Describe();
            return text;
        }

        public static string Where(Vector3 point)
        {
            return "(" + point.x.ToString("0.#") + "," + point.z.ToString("0.#") + ")";
        }
    }
}
