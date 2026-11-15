# Runtime spike — 2026-11-04

What Husnisse's design assumes, tested against the real runtime rather than argued from the docs.

**Environment:** rig tree `V:\ValheimTest` (port 2556, world `MTTest`), `build_parity: MATCH
E5E761C58099`, BepInEx 5.4.23.6, no game client attached. Probe: `probe/NisseProbe.dll`
(`artifacts/NisseProbe.dll`), three boots, probe removed from the tree afterwards.

## 1. Where chest contents live — PASS

Contents are in the chest's ZDO, not in a side channel, and they are replicated.

- `Container.Save()` packs the whole `Inventory` into a `ZPackage` and writes it to the ZDO key
  `items` as a byte array; `Container.Load()` re-reads it whenever `DataRevision` changes, and
  `CheckForChanges()` polls that once a second **on clients too**.
- `ZDO.Serialize()` writes every stored byte array with no per-peer filtering, and
  `ZDOMan.SendZDOs` sends each ZDO to any peer whose `ShouldSend` covers its sector. There is no
  "owner-only field" concept for container data.
- Runtime: a freshly spawned `piece_chest_wood` had `zdos items = 0 bytes`; adding 50 wood made it
  `21 bytes`, readable through `ZNetView.GetZDO().GetByteArray("items")` without opening anything.

**Consequence:** QI's `Count` / `CanAfford` can be answered from the claim client-side with a plain
scan of instantiated `Container` objects. **No custom server→client snapshot layer is needed at
0.3.** The earlier concern that craft-from-claim needed a networking design was wrong; this is the
cheap path, and the contract's "data-source swap" sentence stands.

Residual: confirm on a real client join that a chest in range but out of line of sight is counted.
That is one join, not a design question.

## 2. Are player-built pieces pathable? — YES as geometry, no runtime proof yet

- `Pathfinding.m_layers = 0x01908C01` → `Default(0)`, **`piece(10)`**, `terrain(11)`,
  `static_solid(15)`, `Default_small(20)`, `blocker(23)`, `pathblocker(24)`; water `0x10`.
- `BuildTile` calls `NavMeshBuilder.CollectSources(bounds, m_layers, PhysicsColliders, ...)`, so
  wood floors and walls on layer 10 are baked into the navmesh exactly like terrain.
- Humanoid agent build: height 1.8 m, radius 0.4 m, climb 0.3 m, slope 85°. A 2 m door is
  comfortably wide; any step taller than 0.3 m is not walkable without stairs or a ramp.

**The constraint that matters more than the answer:** tiles are 32 m and built **lazily**.
`GetPath` pokes the from/to tiles plus their neighbours and then immediately snaps to the navmesh —
so the *first* request in a fresh tile returns `false` while `Buildtiles` waits out
`m_updateInterval` (measured: 10 s). Any nisse AI must treat "no path" as retry-later, and must not
spam path requests, because every request re-pokes the tile and postpones its rebuild.

## 3. Indoor pathing — RESOLVED (client probe)

The dedicated server could not answer this. With a player connected it still reported the player's
zone unloaded after 180 s, and raycasts found no terrain; `ZoneSystem.Update` returns before zone
creation on this world. The probe was therefore run **on the client** (`mode=client`), where zones
always load.

Run: player at `(-37.13, 40.05, -10.85)`, standing on a `WaterVolume` collider at y = 40. The probe
built a 3×3 wood-floor room with a 2 m doorway gap plus a four-tile walkway, then pathed from the
walkway's far end (z −10.5) to the room centre.

```
navsample[house_inside] found=True dist=0.05
solid[house_inside]     collider=collider layer=piece y=40.15
path[poll0..2]  have=True  partial=True/6  partial_end_h=0.00  full=True/6  full_end_h=0.00
path[poll3..9]  have=False partial=True/2  partial_end_h=5.42  full=False/0
```

- `found=True dist=0.05` on layer `piece`: **built floors are navmesh**, confirmed on the built
  geometry rather than from the mask alone.
- Polls 0–2 report a clean path, but that is the tile from *before* the walls were baked; polls 3–9
  are the rebuilt tile, and they stop 5.42 m short of centre — i.e. at the doorway.

**Conclusion from this run: floors are walkable, and this 3×3 room's 2 m doorway was not.** A 0.4 m
agent radius plus wall posts left no corridor the navmesh builder would bridge. Husnisse must not
design walking around crossing player doorways: walking is room-scale and outdoor, and a threshold
fades. **Superseded in part by §4**: a free-standing 2 m opening *is* walkable on the same build, so
doorways are unpredictable and the fade stays the fallback.

## 4. Movement on a dedicated server — 2026-11-14

Two probes, both throwaway, both removed from the tree after the run. Tree `V:\ValheimTest`, world
`MTTest`, `build_parity: MATCH E5E761C58099`, `Pathfinding.m_updateInterval = 10`, tile 32 m.

### 4a. The server does bake player-built floors (`probe/legs`)

A 21-piece `wood_floor` walkway at y = 40, no terrain anywhere, no client connected. The first probe
run built the walkway but ZNetScene culled it before the tile baked, and every path failed — the
source of the earlier "the dedicated server has no navmesh" note. With `ZNetScene.CreateDestroyObjects`
suppressed (the harness `hold` equivalent):

```
navmesh_vertices=0  navmesh_tris=0        (3 s after the build)
path[leg0] full=False/0 end_h=20.00
navmesh_vertices=24 navmesh_tris=12       (6 s after the build)
path[leg1] have=True partial=True/6 end_h=0.00 full=True/6 end_h=0.00
```

**Conclusion: a server-owned creature can path along player-built floors.** Evidence has to be taken
after the rebuild, and the test pieces must survive culling.

### 4b. The trip walks, and a closed room fades (`probe/legs-smoke`)

A 20 m walkway, a bowl at one end, a chest at the other, three `Coal` drops near the bowl. The mod's
own scheduler ran; the probe only observed.

```
trip start stacks=3 pickups=3 target=piece_chest_wood(Clone)
leg waiting for navmesh -> (2.0, 40.4, 0.0)
leg -> (2.0, 40.4, 0.0) corners=2 attempt=2          (cold tile: the one retry carried it)
leg -> (3.5, 40.4, 0.0) corners=2 attempt=1
leg -> (5.0, 40.4, 0.0) corners=2 attempt=1
leg -> (13.9, 40.1, 0.0) corners=2 attempt=1
trip deposit moved=3
leg -> (1.5, 40.2, 0.0) corners=2 attempt=1
walk end chest=3 start_chest=0
```

Per-second samples never exceeded **1.94 m** of movement (walk speed 2 m/s), and the nisse stayed at
y = 40.1 for the whole round trip: it walked, it did not teleport. A nearest-first sweep visited the
drops in order 2.0 → 3.5 → 5.0.

Doorway measurement, on a 3×3 floor room with walls on every side and one 2 m opening, after the tile
rebuild:

```
bake_probe doorway0..6 full=True corners=2 end_h=0.12    (7/7, opening is walkable)
```

So a free-standing 2 m doorway **is** passable here, unlike the 3×3 room in §3. The shape, not the
width, decides; the mod must not promise either.

The doorway was then closed with one wall piece, and the same probe re-run:

```
bake_probe closed1..6 full=False corners=0 end_h=13.85
leg blocked -> (13.9, 40.1, 0.0)
fade across to (13.9, 40.1, 0.0)
trip deposit moved=2
leg blocked -> (1.5, 40.2, 0.0)
fade across to (1.5, 40.2, 0.0)
doorway end chest=5 start_chest=3
summary samples=150 max_step_x10=123 fades=2
```

The fade is visible as a **12.3 m jump in one sample** between two fades of 0.45 s each; the work still
happened and the chest received the batch. No path, fade, report, carry on — the failure is legible.

### 4c. Creature facts measured along the way

- **The Greyling controller has no `spawn` trigger.** Its animator parameters are `forward_speed`,
  `sideway_speed`, `turn_speed` (float), `jump`, `attack`, `stagger`, `throw` (trigger), `inWater`,
  `dead`, `wakeup` (bool), `footstep`, `random` (float), `idle` (**int**). `PlaySpawn` previously set a
  trigger that did not exist; it now uses the idle variation.
- **The dedicated server's Greyling materials are stripped**: renderers `Cube`, `Cube.001`, `Plane`,
  `Plane.001` on `Custom/Creature` and `Standard`, with no float, vector or texture properties exposed.
  Shader-property discovery must run on a client; the server never renders the fade.
- **A nisse spawned at the edge of a floor falls out of the world**, and a falling character still
  reports "arrived" on a horizontal-only distance check. Spawn points are navmesh-sampled now, arrival
  compares y, and a body 15 m below its path is warped back.

## 0.1.0 headless smoke — 2026-11-04

Tree: `V:\ValheimTest`, pack parked in `plugins.disabled`, only Jotunn 2.30.2,
ConditionalConfigSync 1.0.11, SoftStop and Husnisse loaded (`4 loaded, 0 failed`).

| Step | Result |
|---|---|
| Plugin loads with declared dependencies | pass — after fixing the CCS GUID (below) |
| `CustomPiece` + `CustomCreature` register | pass — `bowl registered`, `creature registered` |
| Bowl instantiated server-side (harness) | pass — claim registered, `r=20`, `satiety=25`, `mood=100`, `nisse=yes` |
| Nisse spawns bound to the bowl | pass — `nisse settled at bowl <zdo>` |
| Bowl destroyed | pass — claim removed, `nisses=0` (despawn works) |
| Diagnostics + schema | pass — `BepInEx/BSBLogs/Husnisse.log`, `schema=1` |

**Fixed by the smoke:**
- ConditionalConfigSync's real GUID is **`_shudnal.ConditionalConfigSync`** (leading underscore); the
  first boot refused with a clear "missing dependencies" error.
- The cauldron clone still carried a `CraftingStation`; it is now stripped with `CookingStation` and
  `Container`.
- Destroying a bowl left its nisse behind; `NisseFactory.Despawn` now removes it.

**Still unproven:** placement through the hammer (ghost validation), feeding by interact, a round
that actually files an item, and WardIsLove binding with the mod present. Those need a client.

## 5. Claim authority — 2026-11-14

Question: with WardIsLove 4.0.9 installed, does a bowl resolve the right ground per bowl, survive a
ward appearing and disappearing, and refuse a second bowl in the same ward — headless, on the rig?

**What WardIsLove 4.0.9 actually exposes** (decompiled for the API surface only; no WardIsLove code
lives in this tree):

- `WardIsLove.Util.WardMonoscript.m_wardInstances` is a `public static List<WardMonoscript>` and is the
  authoritative list. `PrivateArea.m_allAreas` never sees these wards, because WardIsLove replaces the
  guard stone with `Thorward`.
- `IsEnabled()` is the `General / WardEnabled` toggle **and** the ZDO bool `enabled` **and**
  `WILWardLimitCheck()`. That limit check returns **false** for a ward that is not flagged limited, so
  an unlimited ward must carry `WILLimitedWard = true` and `WILLimitedWardTime = -1`. A ward spawned
  with only `enabled = true` reports disabled — this is the trap that cost the first run.
- `GetWardRadius()` reads the ZDO float `wardRadius` (default `WardRange`), so the radius written into
  the ZDO is the radius the claim must use.
- The owner is the ward `Piece`'s creator. Read it from the ZDO (`creator`), not from `Piece.m_creator`:
  the field is a copy taken in `Piece.Awake`, so a piece whose creator is written after Awake keeps 0.
- `CustomCheck.CheckAccess` stops at the **first** enabled ward containing the point, which is not
  necessarily the ward this bowl selected. Per-ward permits come from
  `WardMonoscript.permissionsManager.IsPermitted(playerID)`, so the claim layer asks the ward it
  actually chose.

**How it was driven.** The shared harness gained `zdoset <prefab> <key> <type> <value> [index]`,
`destroy <prefab> [index]`, an optional index on `zdo`, an optional creator on `spawn`, and a Husnisse
command (`husnisse claims | containers | status | preview <x> <z> [radius] [player]`) that reads the
mod's live claim objects and calls `ContainerIndex.InClaim` or `ClaimPreview.Evaluate`. `expect_last`,
`expect_claim` and `expect_preview` assert the newest observation, so a line left over from an earlier
step cannot make a transition test pass. Five scenarios, one server session:

| Scenario | File | Verdict (final) | What it shows |
|---|---|---|---|
| native | `husnisse-02-native.cmd` | PASS (19) | `source=native`, `ward=0`, `r=20`, no snapshot; `husnisse preview 0 0` says `verdict=ok` before placement and `verdict=overlap` after it, `41 0` stays `ok`; status carries `center=0,0` |
| ward claim | `husnisse-02-ward.cmd` | PASS (21) | `source=ward`, `owner=111`, `r=25` (the ward's radius), `native_r=20`, snapshot written; the preview promises `source=ward center=-20,-20 r=25 owner=111 verdict=ok` before the bowl exists and `overlap` anywhere in the same ward; `husnisse_status` prints `[compat] ... wards=1 ward_api=bound` and a claim line with `center=-20,-20` |
| ward removed | `husnisse-02-ward-removed.cmd` | PASS (14) | ward → native within 6 s without a restart; `r` back to 20, snapshot still 20; preview at the bowl is native `r=20` |
| second bowl in a ward | `husnisse-02-second-bowl.cmd` | PASS (11) | `total=2 working=1 suppressed=1`, refusal `another bowl already holds this ward`, same answer on a second pass, status `refusal_code=WardHeld` |
| overlap | `husnisse-02-overlap.cmd` | PASS (31) | a ward over a native claim without containing the bowl: the bowl stays native and its container count drops 1 → 0; preview at the chest says ward/25/222 but refuses as `overlap`, and clear ground is `ok`. Two wards over one bowl: the placer's ward wins over the smaller (222/r=28 beats 111/r=12); with neither ward owned by the placer the smallest wins (111/r=12); the preview follows both branches |

**What this proves, and what it does not.** The provider seam, per-bowl selection, one bowl per ward,
the native snapshot, the live switch and the preview calculation are proven on a real server with the
real WardIsLove, and the same five scenarios pass with a real client connected on the same build
(`docs/evidence/0.2b/client-session.txt`). The client's own claim objects — not the server's — were
polled through all five and matched every transition (`client-claims-final.txt`). Still open: placement
through the hammer UI with a real player's piece table, and the ledger panel (0.3.1), which will call
the same preview seam.

### 5a. Range preview on a client — 2026-11-15

**Question.** Does the range ring render, and does the claim line reach a player's hover?

**Answer.** Yes, both, with one measured correction.

- The ring is a 72-segment `LineRenderer` on a shared `husnisse_claim_ring` object, `Sprites/Default`,
  0.3 m wide, 0.1 m above the sampled ground. On the client it renders clearly (green at 3 m radius
  around the player, `docs/evidence/0.2b/range-preview-ring.jpg`).
- **The heightmap can sit below a built or static surface.** At the spawn platform the heightmap and a
  terrain raycast answered ~0.3 m below the visible stone, so a ring placed at `hit + 0.1` was hidden
  under the platform even though it existed and was active. The fix is to treat the caller's own height
  (the placement ghost, or the player asking for a preview) as a floor: `max(sampled, caller - 0.02) +
  0.1`. A preview ring must never sink below the surface it is previewing.
- The hover line reaches the player: `Porridge Bowl / Satiety 25/100 Mood 100/100 / [E] open bowl /
  Claim 20 m (native)` (`hover-native-claim.jpg`), and `Claim 10 m (ward)` for a bowl inside a ward
  (client probe log). WardIsLove denies the container hover to a player its ward does not permit
  (`No access` in game), which is correct for a non-owner and is why the ward screenshot shows the ward's
  own access gate rather than our line; the mod's computed text is in the probe log.
- The placement postfix was exercised on a client by putting a real bowl prefab into
  `Player.m_placementGhost`; the ring follows it. Driving the actual hammer selection needs a character
  that has learned the recipe and remains a manual check.

**Reproduce**

```bash
cd ../testrig
./rig.sh down && ./rig.sh deploy ../nisse/artifacts/debug/Husnisse.dll BootstrapBoys-Husnisse
./rig.sh deploy artifacts/harness/TestHarness.dll BootstrapBoys-TestHarness
./rig.sh up
for f in native ward ward-removed second-bowl overlap; do ./rig.sh scenario "scenarios/husnisse-02-$f.cmd"; done
./rig.sh down
```

The tree's config must keep `2. Nisse / Claim Radius = 20`, `Respect WardIsLove = true` and
`Require Ward Ownership = false`; the scenarios assume those values.

## 6. Who the nisse can talk to — 2026-11-15

**A dedicated server has no `Player` objects.** `Player.GetAllPlayers()` (which is
`Player.s_players` filled by `Player.Awake`) is **empty** on the server, because the server
simulates no player characters: a peer drives its own character ZDO and the server only holds the
ZDO. Measured with the scene driver on `V:\ValheimTest`:

```
route players=0 peers=1 isServer=True
route Player.GetAllPlayers() is empty on this peer
route peer uid=265214798 name="Stranger" ready=True refpos=(2.01, 33.84, 2.01) character=265214798:1
```

**Consequence:** any code that addresses players by iterating `Player.GetAllPlayers()` on the
server is a no-op — the loop runs zero times, and `Player.Message` is never reached. The audience
is `ZNet.instance.GetPeers()`; `ZNetPeer.GetRefPos()` is the player's position as the server knows
it (exact in x/z, and the same reference the zone system uses).

**Delivery.** `Player.Message(type, msg)` shows on the local HUD when that character's ZDO is owned
by this peer and otherwise sends `InvokeRPC("Message", …)` to the ZDO owner — and `Player.Awake`
registers the `Message` handler **only on the owner**. That route is real but needs a `Player`
object to call it on. The route that works from a dedicated server is the game's own HUD handler,
which `MessageHud.MessageAll` uses as a broadcast and which takes a peer id for one target:

```csharp
ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage",
    (int)MessageHud.MessageType.TopLeft, text);
```

Proven on the client, not inferred: `lc hud msg "PROBE-B ShowMessage peer"` in the client's
BepInEx log, then the real line twice in two separate trips
(`docs/evidence/0.2.2-voice/RESULT.md`).

**Asserting a HUD line.** `MessageHud` keeps every line it showed in `m_messageLog`
(`GetLog()`) and the TopLeft text on screen in `m_messageText.text`; both are readable from a
client probe, so "the player saw it" stops being a screenshot judgement. A message suppressed
because the HUD is hidden (`Hud.IsUserHidden()`) is neither shown nor logged.

## What this means for the plan

| Assumption | Status |
|---|---|
| Client can count claim storage without opening chests | **proven** |
| Player-built floors become navmesh | **proven** (mask, a live path endpoint on layer `piece`, and a server-side 20 m walkway) |
| A creature can walk inside a player base | **proven headless** — 20 m round trip at 1.94 m/s, no fades (§4b); client visual pass pending |
| An unreachable container fades and is reported | **proven headless** — closed room, `leg blocked` + fade + delivery (§4b) |
| Doorways are passable | **shape-dependent** — free-standing 2 m yes, this 3×3 room's 2 m no (§3, §4b) |
| Pathing is free | **false** — 10 s lazy tile build, first query in a new tile loses the race |
| 0.3 needs a new sync layer | **false** — ZDO already carries it |

## Probe lessons (for the next spike)

- The dedicated rig never loads terrain zones, even with a client connected. Client-side is the only
  reliable place for navmesh tests.
- An unspawned character sits at the `(1000000, 1000000)` sentinel; reject implausible positions or
  the probe builds its test room a million metres away.
- A navmesh result taken during the first 10 s after geometry changes is stale — the tile rebuild has
  not happened yet. Poll for at least 30 s and trust the later polls.

## Libraries

Checked against the actual assemblies rather than reputation:

- **Jotunn 2.30.2** ships `CreatureManager` + `CustomCreature` (ctor takes a prefab clone and a
  `CreatureConfig`, supports `Spawns`), plus `CustomPiece`, `CustomRPC`, `PrefabManager`,
  `LocalizationManager`. That is the whole "no manual patches" ask for the nisse and the bowl, and
  it is already in the rig's plugin set.
- **Newtonsoft.Json** is already inside `valheim_Data/Managed`; referencing it costs no extra DLL.
- **ConditionalConfigSync** is the right answer for host-owned config: the design already says
  world-state config is server-authoritative, and this is that mechanism rather than a custom RPC.
- **YamlDotNet** is the only genuinely new DLL to ship, and only if config/data files are YAML.

None of the four solve the two hard problems (pathing, and the creature feeling good); they remove
boilerplate around them, which is the correct reason to use them.

## Reproduce

```bash
cd probe && ./build.sh                       # or: dotnet build NisseProbe.csproj -c Release
cd ../testrig && ./rig.sh down && ./rig.sh deploy ../nisse/probe/artifacts/NisseProbe.dll NisseProbe
./rig.sh up && sleep 55 && ./rig.sh tail 900 | grep 'probe:'
./rig.sh down                                # then remove BepInEx/plugins/NisseProbe
```
