# 0.6 — machines and four-way permissions: evidence

Build: **Husnisse 0.6.0**, `artifacts/debug/Husnisse.dll` sha256 `2b0337920298`.
Rig: `V:\ValheimTest` (dedicated server, port 2556, Jotunn 2.30.2, ConditionalConfigSync 1.0.11,
YamlDotNet 16.3.1, TestHarness). No client was connected; nothing was released.

## Gates

| Scenario | Assertions | Result |
|---|---|---|
| `testrig/scenarios/husnisse-06-machines.cmd` | 21 | **PASS**, 0 failed |
| `testrig/scenarios/husnisse-06-permissions.cmd` | 25 | **PASS**, 0 failed |
| `testrig/scenarios/husnisse-06-home.cmd` | 17 | **PASS**, 0 failed |

Each scenario starts with `cleanup` and rebuilds its own cell. Run them one at a time:
`./rig.sh scenario scenarios/husnisse-06-machines.cmd`.

### machines — a smelter fed, capped and emptied

Setup: bowl at (0,0), a spawned `smelter` at (6,0) with no roof, a `piece_chest_wood` at (6,4) holding
40 coal and 6 copper scrap, and a 6×6 floor of `wood_floor` under the smelter.

* `husnisse machines` finds `processor smelter at=(6,0)`, and `husnisse status` prints
  `machines_tended=1` with a `[machines]` line.
* The nisse feeds it: `[Machines] fed machine=smelter item=$item_coal units=10 fuel=0/10` and
  `… item=$item_copperscrap units=5 input=0/5`.
* The caps hold: `zdo smelter fuel` **float=10** (the prefab's 20 × `Fuel Cap Percent` 50) and
  `zdo smelter queued` **int=5** (the prefab's 10 × `Input Cap Percent` 50); the chest went 46 → 31
  items (10 coal + 5 scrap) and a second 10 s wait took nothing more.
* With a `wood_floor` as a roof and `nettime 25` + `nettime 35` to move the world clock, the smelter
  smelts: the ore queue fell 5 → 3, and the chest ended with
  `stack[2] $item_copper x2` — the nisse carried the bars out and filed them by the chest's rule
  (`cat:metals`, written by the first network toggle's suggestion).

### permissions — four purposes, per chest, red/green on one setup

One smelter, one chest, one permission changed at a time (`console husnisse_rule supply …`), with the
ZDO read back each time (`v1;k=none;s=…`).

| Step | Chest permission | Result |
|---|---|---|
| control | `all` (default) | fuel 10/10, ore 5/5, chest 24 → 10 items |
| none | `none` | after 20 s: fuel 0, ore 0, chest untouched at 30 |
| production | `production` | ore 4 taken, fuel still 0, chest 30 (coal only) |
| fuel | `fuel` | fuel 10, ore unchanged, chest 20 |
| valuable wood | `all`, `m_fuelItem = FineWood` | fuel stays 0, the 10 fine wood untouched; flipping `m_fuelItem` back to Coal fills to 10 |
| crafting | `-crafting` | `[contract] count $item_wood` 12 → **0** → 12 as the purpose is switched off and on |

### home — fires and animals

* A `fire_pit` with its fuel zeroed is refuelled: `[Machines] fed machine=fire_pit item=$item_wood`,
  `zdo fire_pit fuel` float=5, chest 20 → 15 wood.
* With the chest's `fuel` purpose off, the same fire stays at 0 and the fresh chest keeps all 20 wood.
* A hungry `Boar`: with the chest's `feed` purpose off nothing leaves it (10 blueberries stay); with
  `feed` on, one portion leaves (10 → 9) and `[Machines] fed animal=Boar` is written.
* `machines_tended=2` at the end, so both the fire and the creature are in the scan.

## Engine facts measured on the rig (all in `docs/SPIKE-REPORT.md`)

1. **A dedicated server freezes `ZNet.m_netTime` while nobody is connected** (`ZNet.UpdateNetTime`
   only advances it when `GetNrOfPlayers() > 0`). Every vanilla machine that measures work with
   `ZNet.GetTime()` — smelter, fermenter, cooking station, beehive, sap tap, fireplace — stands still
   in an empty world. The harness gained `nettime <seconds>`, which moves the same clock a connected
   player would, so the gate drives the machine's own update loop instead of waiting ten real seconds
   per bar. This is the game's rule, not the mod's.
2. **`SmokeSpawner.IsBlocked()` is "no smoke in the last 4 s"**, so a smelter also wants
   `m_requiresRoof` satisfied by a real collider above it; a spawned `wood_floor` at +5 m does it.
3. **A smelter's output is a real drop, not a ZDO field** (`m_spawnStack = false` on this prefab):
   `spawnAmount`/`spawnOre` stay absent and the bar is instantiated at `m_outputPoint`. On a server
   with no terrain it falls for ever unless something catches it — the gate lays a floor, as a base
   would. The mod's ground pass queues it and files it, which is what the `$item_copper` stack proves.
4. **A spawned creature does not eat on this rig.** Awake (`m_sleeping=False`), a 4 m consume range
   and a 1 s search still leave a berry on the ground for 15 s (`husnisse-06-probe8.cmd`,
   `-probe11`); an untamed boar also despawns when the clock is pushed into daylight. The mod's half
   — the portion leaving the chest, being reserved from the pickup pass and being set down beside the
   animal — is proven; the animal's own eating is a client-rig check.
5. **A claim is a circle on the map, not a cylinder.** A drop that falls out of the world still reads
   as inside the claim and was retried every drain for ever; a ground ticket more than 40 m below the
   bowl is now dropped, and the trip does not fade the nisse underground to chase it.

## Not proven here (client)

* The four supply toggles and the ledger's **Pause machines** switch are built and compile; no client
  was driven. Same for `husnisse_rule supply`'s UI path and the `[machines]` lines in the ledger.
* A creature eating the portion laid down for it.
* The visible bundle when the nisse carries a machine load (the same `NisseBundle` 0.5 proved).

## Rig changes made for this phase

* `testrig/harness/HarnessPlugin.cs`: `contents <prefab> [index]` (per-stack names and a summary),
  `machinefield <prefab> <field> <value> [index]` (writes any field on any component of a piece, and
  echoes the old value, which is also how a field is read), `nettime <seconds>` (advance
  `ZNet.m_netTime`), `husnisse machines` (the mod's machine list through its own types), and `zdo`
  now prints `long=` values.
* `testrig/scenarios/husnisse-06-{machines,permissions,home}.cmd` (gates) and
  `husnisse-06-probe{,2,3,4,5,6,7,8}.cmd` (the probes the numbers above came from).

## Not released

No tag on the build, no zip, no Hexium publish.
