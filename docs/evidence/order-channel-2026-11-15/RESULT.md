# Order channel — client orders reach the server

Card `t_9dc34cce`, follow-up to the client gate (`t_a9aba3e7`, `docs/evidence/client-gate-2026-11-15`).
The gate proved that on a dedicated server with a real client **no order ever reached the server**.
This card fixes the three faults behind that, and proves on the rig everything a dedicated server can
answer without a second machine. The client-side acceptance is **not run** — see "Client gate" below
for the reason and the exact commands.

| | |
|---|---|
| Build under test | `Husnisse.dll` 0.3.0 on HEAD `a9e0e1d` (0.3b merged) + the identity refactor, sha256 `165f728beb32`, built from a clean worktree so no other card's uncommitted work is in it |
| Harness | `TestHarness.dll` sha256 `a082605d6b98` |
| Tree | `V:\ValheimOrders`, port 2856, world `MTTest` (fixture), `build_parity: MATCH E5E761C58099`, live server untouched |
| Config | `Feeding Required = false` (per-bowl opt-in), `Debug Logging = true`, `Allow Orders = true`, `Bowl Drain Minutes = 20` |
| Drivers | `TestHarness` commands `orderid`, `pocketorder`, `pockets`, `handover`, `container`, `interact`, `zdo`, `zdoset`, `logcheck` |
| Scenarios | `tests/10-husnisse-orders.cmd` (PASS 15/15), `scenarios/husnisse-orders-toggle.cmd` (PASS 6/6), `scenarios/husnisse-orders-handover.cmd` (PASS 4/4) |

Earlier runs of the same code produced `3a9b4e09ae2f` (on `86a9f45`) and `2da40c5002da`; the numbers
below are the **re-run on current HEAD** after 0.3b merged, because 0.3b changed the order path (see
"0.3b changed what a destination is" below). Rebuilding the same commit does not reproduce a
byte-identical DLL while sibling cards keep merging; quote the build hash with the commit.

## 0.3b changed what a destination is — the fixtures had to follow

0.3b's router only files into a container that is **on the storage network**
(`husnisse_net = true`); a bare chest is not a destination. On HEAD the order path therefore queued
**0** stacks and said so — `[Orders] tidy queued=0 unmatched=2` — where the 0.3.0 fixtures still
expected `queued=2`. That is the new behaviour working, not a regression: the fixtures were stale.

`tests/10-husnisse-orders.cmd`, `scenarios/husnisse-orders-handover.cmd` and the client scene
(`make-scene.py`) now mark their destination chests `husnisse_net` before the order
(`zdoset piece_chest_wood husnisse_net bool true <index>`). With that, the same scenarios pass
unchanged on HEAD. Anyone writing a Tidy / Empty Pockets fixture after 0.3b needs the same line.

## The three faults and what changed

**1. `OrderRpc.Handle` dropped the request on an identity this build never fills.**
`ZNetPeer.m_playerID` is `0` for a connected client (`RPC_PlayerID` is registered and nothing invokes
it), so the guard `peer.m_playerID != playerId` returned early with no log line at all.

`OrderRpc.ResolveCaller` / `CallerFor` now name the caller from the character ZDO the connection is
driving — `peer.m_characterID` → `ZDOMan.GetZDO` → `GetLong("playerID")`, the same value the client
computes in `Player.GetPlayerID()`. `peer.m_playerID` is still honoured when a build does fill it. The
id in the package is only compared against the resolved id; a mismatch is refused, not obeyed.

**2. `OrderService.ExecuteOnServer` could not resolve a player.**
`Player.GetPlayer(playerId)` searches `Player.GetAllPlayers()`, which is empty on a dedicated server
(no player characters are simulated there). Orders now run against an `OrderCaller`: a live `Player`
when one exists (host, single player), otherwise the peer's identity plus its reference position. The
order rules (`Validate`) run against that caller — the same list the client pre-checks, so both sides
agree.

**Empty Pockets** is the one order that needs the player's inventory, and on a dedicated server that
inventory only exists on the player's own machine. It is two steps now: the server accepts the order
and answers `Handover`; `OrderRpc` asks the caller's peer; the client serialises the stacks it may give
up (not hotbar, not worn, not quest items), removes them and sends the item data; the server rebuilds
them, drops them by its bowl as ordinary server-owned drops and queues them for the usual trip. From
the handover on, the server owns the items; a handover the server cannot rebuild leaves the stack out
rather than placing a wrong one.

**3. `OrderRpc.Send` fed `Handle` a package it had just written, without `SetPos(0)`.**
On the host, single-player and any server-initiated call the read started at the end of the stream
(`order RPC failed: Unable to read beyond the end of the stream`). One line: rewind before the local call.

**Also user-visible:** the queued message was printed before the server accepted anything. The server
now answers through `Husnisse_OrderAck` with a verdict and a count, and the caller speaks then:
`The nisse takes 2 stack(s).` / `The nisse files 2 stack(s).` / `The nisse leaves it as it is.`, and the
bowl's toggle answers `The nisse will expect offerings.` / `… work without offerings.` Refusals keep
their existing lines. For the bowl toggle and the orders the server also writes `[Bowl] feeding=` and
`[Orders] … queued=N` into `BSBLogs/Husnisse.log`, which is rewritten every boot, so a scenario can
assert on them without matching an earlier run.

## Headless proof (no client)

`tests/10-husnisse-orders.cmd` — **PASS (15 assertions, 0 failed)**, on the fixture world:

| Check | Evidence |
|---|---|
| Identity from the character ZDO, with `peer.m_playerID = 0` | `orderid peer_m_playerID=0 zdo_playerID=-202584743`, `orderid PlayerIdOf=-202584743` |
| A package naming the resolved player is accepted | `orderid caller claimed=-202584743 player=-202584743 peer=424242 valid=True` |
| A package naming another player is refused | `orderid caller claimed=999 player=0 peer=0 valid=False` |
| Empty Pockets: stacks off the hotbar leave the player | `pocketorder … before stacks=2 items=42`; after the trips `pockets player=0 stacks=0 items=0` |
| … and the nisse files them in the chest | `container piece_chest_wood stacks=2 items=42` (12 coal + 30 wood), `[Orders] empty pockets queued=2` |
| Tidy: Alt on a chest queues its contents | `interact piece_chest_wood alt: Container=True`, `[Orders] tidy queued=2` |
| … and the nisse moves them to the other chest | source `stacks=0 items=0`, target `stacks=2 items=42` |

`scenarios/husnisse-orders-toggle.cmd` — **PASS (6 assertions, 0 failed)** on the per-bowl tree:

| Check | Evidence |
|---|---|
| Alt on the bowl flips the flag on, then off | `zdo … = absent` → `int=1 bool=True` → `int=0 bool=False` |
| The server logged both writes | `[Bowl] feeding=True by 0`, `[Bowl] feeding=False by 0` |
| The interaction reached `ExecuteOnServer` (fault 3 gone) | `interact husnisse_bowl alt: Container=True` (no `Unable to read beyond the end of the stream`) |

`scenarios/husnisse-orders-handover.cmd` — **PASS (4 assertions, 0 failed)**. This is the half of Empty
Pockets a dedicated server cannot reach by itself: the client serialises the stacks and takes them out
of the inventory, the server rebuilds them, drops them by the bowl and queues them. Only the routed RPC
in the middle belongs to the client gate.

| Check | Evidence |
|---|---|
| Client half: two stacks serialised and removed | `handover client stacks=2 after stacks=0 items=0` |
| Server half: both rebuilt and queued | `handover server materialised=2/2` |
| The nisse files them | `container piece_chest_wood stacks=2 items=42`, `[Orders] empty pockets queued=2` |

A note on the rewind bug: `ZPackage.SetPos(0)` alone is **not** enough after writing, because the
`BinaryWriter` still holds the tail of the package in its buffer. The fix is `Flush()` then `SetPos(0)`
(`OrderRpc.Send`, and the harness's own handover command learned the same lesson the hard way).

Note: a ZDO stores a bool as an int (`ZDO.Set(int,bool)` → `Set(hash, value ? 1 : 0)`), so `zdo` prints
both slots. The old `tests/09-husnisse.cmd` expectation `BowlPiece=True` was stale — the bowl's
Interactable is its `Container` — and 0.3.0's diagnostics rewrite changed the summary line
(`schema=2 feeding_required=… claims=… trips=… nisses=… bowl_piece=present`), which made `claims=1 nisses=1`
and `nisse=yes` stale too. Both were repaired in place with the old and new strings recorded in the file.

## Rig instruments added

All in `testrig/harness/HarnessPlugin.cs`, all generic:

- `orderid <playerId> [claimedPlayerId]` — drives the order channel's identity seam with a peer record
  whose `m_playerID` is 0 and a ZDO that carries the id, and prints what the mod resolves. The fault
  that silenced every client order is now a headless assertion.
- `pocketorder <item> <amount> …` — Empty Pockets on the local path through the mod's own
  `NisseHands.Interact`, with a synthetic player whose stacks sit in inventory row 1 (row 0 is the hotbar).
- `pockets` — that player's inventory totals, for reading after a trip has landed.
- `container <prefab> [index]` — a container's contents as stacks/items, so a moved stack is a number and
  not a byte count in a ZDO slot.
- `logcheck` now opens its target with `FileShare.ReadWrite`, so `BepInEx/LogOutput.log` can be asserted
  on while the server runs (it used to fail with a sharing violation and read as MISSING).

## Client gate — the plan (superseded by the results at the end of this file)

The acceptance for a real client (Alt on the bowl with the hover line, Alt on a chest, interact the
nisse) needs the Windows client, and that machine was held by card `t_fed257f1` (0.3a container UI) for
the whole of this run: `valheim.exe` was in world on `192.168.1.136:2556` from before this card started,
restarted twice (pids 3972 → 8452 → 2740), and one ~3-minute window at 04:34 closed while the game was
relaunching — the install then failed with `The requested operation cannot be performed on a file with a
user-mapped section open` because the new process already had the DLL mapped. Nothing was taken from that
session and the client's plugins are still its own build. Everything the gate needs is staged and the
plan below is ready to run the moment the client is free:

- `C:\Users\Hermes\stage\Husnisse.dll` = `544eba090dc4…` (this card's build) and
  `HusnisseGateClientProbe.dll` = `895433df…` (current probe, already speaks `alt`, `use`, `give`, `inv`,
  `state`, `hover`, `watch`, `shot`).
- `V:\ValheimOrders` is provisioned, fixture-restored and `doctor: PASS`; it is stopped now. Its firewall
  rule (`Valheim orders rig UDP 2856`, UDP 2856-2857, Private) exists, so a client can join it.
- The scheduled task `HusnisseLegsLaunch` still joins 2556 for the 0.3a card; give the order gate its own
  task (or change the address to 2856 for the run and change it back).

```bash
# server
cd /home/hermes/projects/valheim/testrig
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh up

# client: install this build + the gate probe, then join 192.168.1.136:2856 (password mtest)
cd /home/hermes/projects/valheim/nisse
./tooling/client-rig/psrun.sh docs/evidence/client-gate-2026-11-15/install-client.ps1
# launch: valheim.exe -console -joinserverwithcharacter 192.168.1.136:2856 nissetester -password mtest
```

Client commands (the probe already speaks all of them; `give <item> <amount> <slotY>` seeds row 1):

| Step | Command | Expected |
|---|---|---|
| bowl is off | `state` | `bowl feeding=False`, hover `This nisse asks for nothing` |
| Alt on the bowl | `alt husnisse_bowl` | server `[Bowl] feeding=True`; `state` shows `feeding=True`, hover `Satiety …/100 … [Alt] no offerings needed` |
| Alt again | `alt husnisse_bowl` | `[Bowl] feeding=False`, hover back |
| seed pockets | `give Coal 6 1`, `give Wood 12 1`, `inv` | two stacks, non-zero items |
| empty pockets | `use husnisse_nisse` | HUD `The nisse takes 2 stack(s).`; server `[Orders] empty pockets queued=2`; client `inv` drops the stacks; the nisse carries them to a chest |
| tidy | `alt piece_chest_wood` | HUD `The nisse files N stack(s).`; server `[Orders] tidy queued=N`; the chest empties into its target |

Do not read a passing client sitting in-world as the toggle working: the hover line and the HUD lines
are the client-side evidence, and the server's `[Bowl]`/`[Orders]` lines in `BSBLogs/Husnisse.log` are
the server-side half.

## Repro

```bash
# headless, from scratch
cd /home/hermes/projects/valheim/testrig
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh provision
# copy CCS/YamlDotNet/JsonDotNet from the live install, the fixture from V:\ValheimTest, and the
# Husnisse cfg with Feeding Required=false + Debug Logging=true (see setup-orders-tree.ps1 in this folder)
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh deploy <build>/Husnisse.dll BootstrapBoys-Husnisse
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh deploy artifacts/harness/TestHarness.dll BootstrapBoys-TestHarness
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh fixture restore
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh up
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh scenario tests/10-husnisse-orders.cmd
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh scenario scenarios/husnisse-orders-toggle.cmd
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh down
```

## Cross-card note

`src/Net/PeerIdentity.cs` (card `t_fed257f1`) implements the same identity rule as
`OrderRpc.PlayerIdOf(ZNetPeer)`. The order seam keeps its own copy only because the headless test needs
a peer object ZNet does not know; when the two cards merge, `PeerIdentity` should own the rule and
`OrderRpc` should delegate to it.

## Client gate — results (2026-11-15 07:04–07:48, build 165f728b)

Run on the real client (`DESKTOP-PAOVLP7`, 192.168.1.172, character `Stranger`, player id
`-202584743`) joined to `V:\ValheimOrders` (port 2856) with the same build and probe. Logs are
`client/logs/client-bepinex.log`, `client/logs/server-husnisse.log`, `client/logs/server-harness.log`.

| Check | Result | Evidence (client) | Evidence (server) |
|---|---|---|---|
| **Alt on the bowl, feeding on** | **PASS** | `interact husnisse_bowl(Clone) alt=True -> True`; `hud center='The nisse will expect offerings.'`; `state bowl feeding=True`; hover `Porridge Bowl \| Satiety 25/100   Mood 100/100 \| [E] open bowl \| [Alt] no offerings needed \| Claim 20 m (native)` | `[Bowl] feeding=True by -202584743` |
| **Alt again, feeding off** | **PASS** | `hud center='The nisse will work without offerings.'`; `state bowl feeding=False`; hover back to `This nisse asks for nothing. \| [Alt] require offerings` | `[Bowl] feeding=False by -202584743` |
| **Empty Pockets from the client** | **PASS (order channel)** | `use husnisse_nisse` → `interact NisseHands alt=False -> True`; `hud center='The nisse takes 2 stack(s).'`; inventory went 2 stacks/36 items → 0 | `[Orders] empty pockets queued=2 claim=-1327346010:15577`; `[Flow] trip stacks=2 target=piece_chest_wood … at=(2.8,-0.5)` |
| **Tidy from the client** | **NOT PROVEN** | `interact piece_chest_wood(Clone) alt=True -> True` but no HUD line and no order; the client's nearest-chest lookup resolved a *ghost* chest left over from an earlier scene (the mod's `ContainerTidy` prefix is silent when its position has no claim) | no `[Orders] tidy` line |
| **Items landing in the destination chest** | **NOT PROVEN** | — | `[Flow] landed moved=0 target=piece_chest_wood(Clone) at=(2.8,-0.5)` — the nisse picked the 2 stacks up but moved none on landing |

So the defect this card set out to fix is fixed: **a client's order now reaches the server and is
executed.** The per-bowl Alt toggle is proven end to end including the hover line (the exact
acceptance wording), and Empty Pockets reaches the server, is accepted, round-trips the handover and
queues with a truthful HUD count. Tidy is exercised as a *channel* only in the headless scenarios; on
this client the chest was addressed through a ghost object, so the order never left.

### Rig findings that blocked the remaining two checks (not mod defects)

1. **A client-owned ZDO is never destroyed for that client.** Every scene run leaves the client
   holding the previous scene's chests; `cleanup` unregisters them server-side but the client keeps
   the GameObjects, so `stand <prefab> <index>` and `alt <prefab>` (nearest) address ghosts. After
   four scenes the client's list held 12 containers, 3 of them ghosts. A relog clears it; nothing
   short of a client restart did.
2. **A server-spawned chest's window does not open on this client.** `Container.Interact` returns
   true but `InventoryGui` never shows (`open gui=none`), which is also what the 0.3a sweep hit. The
   network toggle therefore has to be set by the mod's own `husnisse_rule net … on` console command
   through the harness (`console`), which works and is what this gate used.
3. **A floating deck decays and takes the bowl with it** (WearNTear support), and **a character with
   no collider under it falls through the server's empty world** (the nisse was seen at y=-4496). The
   working shape is a ground-level platform at the fixture's spawn terrain, pieces at 34.17–34.37.
4. **`setpos` and `Player.TeleportTo` fight the server's copy of the player position.** TeleportTo
   did not apply at all in some calls (`now=` unchanged) and a transform set can snap back to the
   character ZDO's position seconds later, which puts the caller out of the claim's radius and makes
   the order fail with `The nisse is not nearby.` The reliable shape is to build the scene around the
   position the client reports at `gc ready` and not move the player at all.
5. A client standing within auto-pickup range of the nisse's drop point picks its own handed-over
   stacks back up (`hud top='Added Coal x12'`), which masks the landing. Leave 3 m between the player
   and the bowl.

These are rig/scene constraints; the follow-up card is filed with them so the Tidy client check and
the landing check are cheap next time.

## Client gate — Tidy and the landing, proven (card `t_fa64f594`, 2026-11-15 08:52–08:58)

Second session. Same build (`165f728b`), same probe (`c00c6bd4`), same tree (`V:\ValheimOrders`,
2856), fixture restored and the client relaunched for the scene (a relaunch is the only thing that
clears the ghost containers the first session left behind).

**Why the first session's landing moved 0 — settled, not guessed.** The server's own debug line was
`[debug][Flow] trip deposit … moved=0 target=piece_chest_wood(Clone) at=(2.8,-0.5) why=no rule
remaining=0`. `remaining=0` means the tickets were *removed*, not requeued, and for an Empty Pockets
trip the tickets are `FlowSource.Ground`, whose `IsValid()` is `Drop != null`
(`src/Flow/FlowTicket.cs`). The drops had been destroyed because the player stood **1.95 m** from the
nisse (`state player=0.88,34.35,1.44`, `state nisse pos=-0.99,34.35,2.00`) and re-picked its own
handover. Nothing in `Land()` is wrong. The fix is geometry: the handover drops spawn at the nisse's
feet (`origin = nisse.transform.position` in `OrderService.MaterialisePockets`), which is the bowl
plus 1.5 m, so the bowl must stand **≥ 4.5 m from the player**.

**The scene** (`make-scene-at.py`, generated for the position the client reported at `gc ready`,
player `-202584743` at `-3.06, 34.17, -0.65`; the player is never moved):

| Piece | Where | Why |
|---|---|---|
| bowl | player + (-4.60, -0.40) = `-7.66, -1.05` | 4.6 m away, so the drop zone stays out of pickup range |
| chest A, source, 6 coal | player + (1.50, 1.70) = `-1.56, 1.05` | 2.3 m away and therefore the **nearest** container — `alt <prefab>` on the client resolves the nearest one |
| chest B, destination | player + (-0.90, 3.20) = `-3.96, 2.55` | **smaller x than chest A**, so it is index 0 for `console husnisse_rule net piece_chest_wood on 0`, `fill` and `container` (all of which index containers sorted by x then z) |
| 12 wood_floor tiles | player's own y, pieces 0.10 above | the dedicated server has no terrain colliders |

Server-side state at scene build: `container piece_chest_wood 0` → `stacks=0 items=0`;
`container piece_chest_wood[1]` → `stacks=1 items=6`; `rule -302931902:15531 -4,2.6 net=on rule=none`.

**Results, second pass** (witnessed in `client/logs/client-bepinex-orders.log`,
`client/logs/server-husnisse-orders.log`, `client/logs/server-harness-orders.log`):

| Check | Result | Evidence (client) | Evidence (server) |
|---|---|---|---|
| **Tidy from the client** | **PASS** | `interact piece_chest_wood(Clone) alt=True -> True`; `hud center='The nisse files 1 stack(s).'` | `[Orders] tidy queued=1 unmatched=0 claim=-302931902:15531 source=piece_chest_wood(Clone)`; `[Flow] trip stacks=1 target=… at=(-4,2.6)`; `[Flow] landed moved=1 … at=(-4,2.6)` |
| **Items landing in the destination chest** | **PASS** | `use husnisse_nisse` → `interact NisseHands alt=False -> True`; `hud center='The nisse takes 2 stack(s).'`; `inv stacks=2 items=18` → `inv stacks=0 items=0` | `[Orders] empty pockets queued=2`; `[Flow] trip stacks=2 … at=(-4,2.6) why=no rule remembered`; `[Flow] landed moved=2 …` |
| **Source empty, destination holds the items** | **PASS** | (the client's own `containers` line reads 0 — see below) | in-scene dump 120 s later: `container piece_chest_wood 0` → **`stacks=2 items=24`**; `container piece_chest_wood[1]` → **`stacks=0 items=0`** |

24 items in the destination = 6 coal (Tidy) + 6 coal + 12 wood (Empty Pockets): every stack that was
queued was filed, and the source chest emptied to nothing.

A first pass on the same geometry (08:47, destination `11.7, 9.2`) also passed with one stack each way
(`tidy queued=1` / `landed moved=1`, `empty pockets queued=1` / `landed moved=1`); it seeded only one
stack because the probe's `give <item> <amount> <slotY>` puts the stack in an explicit grid row and the
second `give` reused row 1, which the coal already occupied. `t04-give.cmd` now seeds rows 1 and 2.

**Two rig notes that made the difference, both worth carrying forward:**

- **A client-spawned copy of a server-spawned chest reads its contents stale.** The client's
  `containers` line shows `stacks=0 items=0` for both chests even *after* the server has 24 items in
  the destination — the client owns its copy of the piece and never loads the server's `items` blob.
  So the chest contents must be read **on the server**, from an in-scene `container` dump inside the
  scene's hold; the client's own read is not that evidence. (The client's *positions* and `net=` flags
  are correct, so it still proves which chest is which.)
- **The stage folder's canonical names are shared between cards.** `C:\Users\Hermes\stage\Husnisse.dll`
  and `HusnisseGateClientProbe.dll` were overwritten by another card's build (91b9af22) between this
  card's first and second attempts; an install that reads them installs the wrong build. Install from
  the uniquely named `Husnisse-orders-165f728b.dll` / `HusnisseGateClientProbe-orders-c00c6bd4.dll`
  (`client/install-orders-165f728b.ps1`), and verify the SHA-256 before launching.
- **One Valheim client at a time.** Two instances share `BepInEx/LogOutput.log`; the second launch
  truncates the first one's log and only one process survives. The 0.3d card's run took the client at
  07:59 (it launches `valheim.exe -joinserverwithcharacter …:2556`) and killed this card's instance
  mid-run. Check the running client's `joinserverwithcharacter` port and the rig's command lock before
  installing.

**Verdict:** both previously unproven checks now pass from a real client, and the destination chest's
contents are proven server-side. The order channel is closed: Alt on the bowl, Alt on a chest (Tidy)
and interact with the nisse (Empty Pockets) all reach the server and are executed there.

