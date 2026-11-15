# Client gate — drain, Alt, orders, raid, relog

Card `t_a9aba3e7`. Five checks on a real client against a settled native claim, on a
dedicated server. The gate found one blocking defect: **the order channel never reaches the
server**, so neither the per-bowl Alt feeding toggle nor either order works from a client.

| | |
|---|---|
| Build under test | `Husnisse.dll` 0.2.2, commit `7a9eb44` — sha256 `e48d347e8bbb6f98b8c66bcee062220884040deec0bb932c42fbb686ed38db40` (108 544 B), identical on server and client |
| Server | `V:\ValheimTest`, port 2556, world `MTTest`, `build_parity: MATCH E5E761C58099` |
| Client | `DESKTOP-PAOVLP7` (192.168.1.172), Steam-owned Valheim, BepInEx 5.4.23.6, character `Stranger`, player id `-202584743` |
| Mod set (both sides) | Husnisse, Jotunn, ConditionalConfigSync, YamlDotNet, JsonDotNET; server adds `ServerSoftStop` + `TestHarness`, client adds `BootstrapBoys-GateClientProbe`; the other 12 plugin folders parked in `plugins.disabled` for the session and restored afterwards |
| Config | `Feeding Required = false` (per-bowl opt-in), `Bowl Drain Minutes = 1`, `Debug Logging = true`, `Allow Orders = true` |
| Drivers | `probe/gate-client` (client; commands `state`, `alt`, `use`, `open`, `close`, `containers`, `players`, `watch`, `cam`, `shot`, `teleport`, `god`, `quit`) and `TestHarness` (`event`, `damage`, `fill`, `players`, `husnisse`, `zdo`, `zdoset`) |
| Evidence | `client-bepinex.log`, `server-bepinex.log`, `server-game.log`, `server-husnisse.log`, `harness.log`, `gate-*.png` |

## Results

| Check | Result | Evidence |
|---|---|---|
| **Bowl Drain Minutes** | **PASS** | Drain tick at 1 minute, watched on the client: satiety 40 → 69.84 (t=10) → 99.73 (t=60), pantry 2 → 1 items at t=60, hover `Satiety 100/100`. Server `[Bowl] ate $item_boar_meat_cooked -> satiety=55` |
| **Per-bowl Alt feeding (Optional)** | **FAIL — request dropped** | Client `interact husnisse_bowl(Clone) alt=True -> True` + `order RPC registered`; no ZDO change, hover still `This nisse asks for nothing`; server never logs `[Bowl] feeding=`. Writing the same ZDO key directly proves the rest: hover becomes `Satiety 25/100 … [Alt] no offerings needed` and the drain runs |
| **Order RPC round trip** | **FAIL — request dropped** | Client `alt piece_chest_wood` → HUD `The nisse will see to it.`; chest A kept its 6 coal, chest B stayed empty, server logged no `[Orders] …`. Server-side re-entry throws `order RPC failed: Unable to read beyond the end of the stream` |
| **Invulnerability under a raid** | **PASS** | `event skeletons` / `event foresttrolls` active (client HUD `A skeleton surprise!`, `THE GROUND IS SHAKING`), `damage husnisse_nisse 500 3` → `health 1000 -> 1000` three times; mod log `[Nisse] ignored damage 500 from …` ×3. `gate-09-raid-trolls.png`, `gate-14-bowl-ui.png` |
| **Relog persistence** | **PASS** | Before: `feeding=True satiety=99.98 mood=76.97 pantry 2 owner=-202584743`, player `-202584743`. After quit + relaunch: `feeding=True satiety=99.80 mood=76.75 pantry 2 owner=-202584743`, same player id, nisse at the bowl. Server after: claim `81827723:39795 satiety=100 mood=77 nisse=yes`. `gate-13-after-relog.png` |
| Bowl pantry UI (supporting) | **PASS** | `open gui=husnisse_bowl(Clone) stacks=1 items=1 inUse=True owner=True`; window titled `PORRIDGE BOWL` with the food slot. `gate-11-bowl-ui.png`, `gate-14-bowl-ui.png` |

## The blocking defect: the order channel

Three separate faults line up so that a client's order is silently discarded.

**1. The server's peer record has no player id.** `ZNetPeer.m_playerID` is `0` for a connected
client on this build (`RPC_PlayerID` is registered but nothing invokes it). `OrderRpc.Handle`
reads the package, then returns early on `peer.m_playerID != playerId`:

```
players s_players=0 nr=1
  peer[0] uid=238028798 playerID=0 name=Stranger character=238028798:1 refpos=(60.1, 41.9, 60.2)
```

**2. A dedicated server has no `Player` objects.** `players` reports `s_players=0` with one peer
connected, so `Player.GetPlayer(playerId)` in `OrderService.ExecuteOnServer` returns `null` and the
order returns before it is queued. This is the same root cause as the 0.2.2 voice fix (0.2.2 fixed
the voice by addressing peers; the order path still resolves the player by id).

**3. `OrderRpc.Send` hands `Handle` a package it has just written, without rewinding it.** On the
host/single-player and any server-initiated call the read starts at the end of the stream:

```
interact husnisse_bowl alt: Container=True
[Warning: Husnisse] order RPC failed: Unable to read beyond the end of the stream.
```

**User-visible consequence.** `OrderService.RequestTidy` / `RequestEmptyPockets` print
`The nisse will see to it.` on the client *before* the server has accepted anything, so the player
is told the work is queued while nothing happens. The Alt toggle at least stays silent.

## A rig finding, not a mod defect

Every piece spawned by the harness carried a **non-persistent ZDO**. `ZNetView.Awake` copies the
prefab's `m_persistent` only when it authors the ZDO itself; a ZDO handed in through
`ZNetScene.CreateObject` keeps the default (`false`). When a player opened the bowl the client took
ownership of that ZDO, and on disconnect `ZDOMan.RemoveOrphanNonPersistentZDOS` destroyed it:

```
03:04:17: RPC_Disconnect
03:04:17: Destroying abandoned non persistent zdo 81827723:39791 owner 238028798   <- the bowl
```

The two chests (never opened, still server-owned) survived, which is how the artefact was isolated.
The harness `spawn` command now sets `zdo.Persistent = true`, so spawned pieces behave like placed
ones across a relog. A player-placed bowl takes the other `ZNetView` branch and is expected to be
persistent, but that was **not** exercised here; the relog check above used a server-owned bowl.

## What this does not prove

- The Tidy / Empty Pockets work path (queue → trip → move): blocked at the RPC decode, so no order
  ever reached the queue.
- A player-placed bowl's own persistence across a client relog while the client held its ZDO.
- Two-client behaviour (handoff, duplication); one client only.
- `Bowl Drain Minutes` values other than 1, and drain while a player has the pantry open.

## Repro

```bash
# server: minimal matching set + gate config (rig stopped)
./tooling/client-rig/rigrun.sh docs/evidence/client-gate-2026-11-15/setup-rig.ps1
cd ../testrig && ./rig.sh deploy ../nisse/artifacts/debug/Husnisse.dll BootstrapBoys-Husnisse
./rig.sh deploy artifacts/harness/TestHarness.dll BootstrapBoys-TestHarness
./rig.sh up

# client: same build + probe
./tooling/client-rig/psrun.sh docs/evidence/client-gate-2026-11-15/install-client.ps1
./tooling/client-rig/psrun.sh docs/evidence/client-gate-2026-11-15/launch-client.ps1

# scene, then checks (see commands/ for the exact batches)
cd ../testrig && ./rig.sh scenario ../nisse/docs/evidence/client-gate-2026-11-15/commands/gate-scene-raid.cmd
```

Client command batches are `commands/c*.cmd`; server scenarios are `commands/gate-*.cmd`.
