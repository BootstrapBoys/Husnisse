# Order channel — client orders reach the server

Card `t_9dc34cce`, follow-up to the client gate (`t_a9aba3e7`, `docs/evidence/client-gate-2026-11-15`).
The gate proved that on a dedicated server with a real client **no order ever reached the server**.
This card fixes the three faults behind that, and proves on the rig everything a dedicated server can
answer without a second machine. The client-side acceptance is **not run** — see "Client gate" below
for the reason and the exact commands.

| | |
|---|---|
| Build under test | `Husnisse.dll` 0.3.0 + the order-channel fix, sha256 `3a9b4e09ae2f` (final build; `544eba090dc4`, `570d80aa6c09` and `f68892326280` were the same code as it converged), identical on both rig trees it ran on |
| Harness | `TestHarness.dll` sha256 `a082605d6b98` |
| Tree | `V:\ValheimOrders`, port 2856, world `MTTest` (fixture), `build_parity: MATCH E5E761C58099`, live server untouched (pid 7284) |
| Config | `Feeding Required = false` (per-bowl opt-in), `Debug Logging = true`, `Allow Orders = true`, `Bowl Drain Minutes = 20` |
| Drivers | `TestHarness` commands `orderid`, `pocketorder`, `pockets`, `container`, `interact`, `zdo`, `logcheck` |
| Scenarios | `tests/10-husnisse-orders.cmd` (PASS 15/15), `scenarios/husnisse-orders-toggle.cmd` (PASS 6/6) |

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

## Client gate — not run

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
# Husnisse cfg with Feeding Required=false + Debug Logging=true (see the card's setup-orders-tree.ps1)
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
