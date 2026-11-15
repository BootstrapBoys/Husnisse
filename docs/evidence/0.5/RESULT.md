# 0.5 — craft from the claim, on a headless server and a real client

**Build:** Husnisse 0.5.0. The headless gate ran on the final build `Husnisse.dll` sha256 `4f4ce6e08de3…`
(commit `af7cbe0` plus the `CanConsume` bind fix); the client craft gate ran on `f189b7cd8f36…`, which
differs from the final build in **one line of integration startup** (`CanConsume` no longer requires the
plugin's `Running` flag, which is false while the consumer integrations bind in `Awake`). That line makes
`epicloot=bound planbuild=bound` on the final build (read from the server's startup log); it does not
touch the supply path the client proved.

**Where:** dedicated tree `V:\Gate031`, port 2956, world `MTTest` (fresh, renamed per run). Client
`DESKTOP-PAOVLP7`, character `Stranger` `-202584743`, with the consumer pack installed on both sides.

**Commands:** `testrig/scenarios/husnisse-05-supply.cmd` (headless, 20 assertions) and
`docs/evidence/0.5/client/gate.sh {prep,client,scene,p1,p2,collect}` (client).

## The decision this release settles

The delivery fiction is a **fetch before the pay**. A craft the pockets cannot cover becomes a supply
errand: the nisse walks to the chests holding the missing materials, takes the planned stacks while it
stands at each one, carries a visible load to the bench and puts it in the player's hands; the game's own
craft then spends the pockets. Nothing leaves a chest at request time, and no craft runs before its
materials are held.

`TryConsume` is honest about what it can do synchronously: it debits pockets → ExtraSlots rows → the
equipped pack, and when an amount would need the claim it asks the nisse for the shortfall and answers
`false` with nothing mutated. A claim chest belongs to its ZDO owner, so a client cannot debit one inside
a synchronous call; returning `true` on a promise is how a craft ends up paid for by a deletion.

## Headless gate — PASS, 20 assertions, 0 failed

`husnisse-05-supply.cmd` on the final build:

| # | Expectation | Read back | Verdict |
|---|---|---|---|
| 1 | A request larger than the claim holds is refused whole | `[Supply] refused result=Missing note="missing 10 of 30 (the claim holds 20)"`, chest still `stacks=1 items=20` | **PASS** |
| 2 | Nothing leaves the chest at request time | same-tick `zdoitems piece_chest_wood[0] … stacks=1 items=20` after the accepted request | **PASS** |
| 3 | One errand at a time | second request `[Supply] refused result=Busy` | **PASS** |
| 4 | The pickup happens at the chest | `[Supply] errand start token=…` then `[Supply] picked up units=10 at=(12,3)` — in that order in the log | **PASS** |
| 5 | The load reaches the player | `[Supply] handover local stacks=1`; `pockets player=0 stacks=1 items=10` | **PASS** |
| 6 | The chest's ZDO (not just memory) is short exactly 10 | `zdoitems … stacks=1 items=10` (20 → 10) | **PASS** |
| 7 | The errand ends idle | `supply errands=0` (newest value), `[Supply] errand done` | **PASS** |
| 8 | The contract moved from read-only to spend-with-a-delivery | `[contract] api=1 provider=native can_consume=True`, `count $item_wood=20 carried=10 claim=10` | **PASS** |

The claim's pantry bowl is not a supply chest; every other container in the claim is a source. A container
the claim may not use contributes nothing (inherited from the 0.2b/0.4 claim proof).

## Client gate — the craft, twice, on a real client

Scene: a 5×5 wooden floor, a workbench at +2/−1 m, the bowl at −1.5 m and a chest at +2.5 m, all inside the
claim the client's bowl owns. The chest is filled **from the client** (`give` → `open` → `move` → `close`) so
its contents are real on both sides; the client's pockets are empty of wood.

| Step | Read back | Verdict |
|---|---|---|
| readout + button | `available [1] Club $item_club canCraft=True`; `select Club craft winner=Club button=True visible=True have=True $item_wood=6+carried0+claim20` | **PASS** — the 0.4 row and the 0.5 craft button both treat the claim as the source |
| press | `hud center='The nisse fetches what the base can spare.'`; vanilla's craft did not run (no Club, no wood spent) | **PASS** — the press became an errand |
| delivery + craft | at +7 s: `pockets stacks=1 items=1 [clubx1]`, chest `20`, then `stacks=1 items=14 [woodx14]`, `count $item_wood=14 carried=0 claim=14` | **PASS** — the nisse brought 6 wood, vanilla's craft spent them from the pockets, the chest lost exactly 6 |
| repeat | a second press/craft: `pockets stacks=2 items=2 [clubx1,clubx1]`, `claim8` | **PASS** — the path is repeatable, and the panel's count follows the chest |
| repaint | the panel's requirement count moved `claim20 → claim14 → claim8` as each craft landed | **PASS** — the recipe list is rebuilt after a craft |

## What is not proven here

- **The bundle as a picture.** The load is built from the item prefabs on each peer (`NisseBundle`) and is
  shown/cleared with the errand, but the gate's screenshots did not catch a readable frame: the crafting
  panel covers half the screen, AAA's first-run "Welcome" popup covers the lower left, and the scene ran at
  night. The visual pass is on `docs/PLAYTEST.md` with the exact steps (daylight, panel closed after the
  press, camera on the chest→player line).
- **EpicLoot's and PlanBuild's cost paths end to end.** Both bind on the final build
  (`epicloot=bound planbuild=bound`), and both start the same `SupplyService.Request` the craft path proves;
  but their own UIs were not driven in this gate, so "the enchant/blueprint pays from delivered materials"
  is bound-and-shaped, not yet observed.
- **AAA's batch on the client.** The batch amount is read from AAA 2.1.x
  (`InventoryGuiShowCraftingPanelPatch.CurrentCraftAmount`) and the deferred craft keeps its size, but the
  gate did not run a batch press. The two single crafts above are the evidence for the press/resume path.
- **DropMoreLoot's diagnostics line**: DropMoreLoot is not in the gate pack, so `stacks=native` is what both
  sides printed. Husnisse writes no stack size at all (grep-able in the tree), which is the deferral.

## Four defects the client gate caught (all fixed in the final build)

1. **The channel was never registered on the server.** `FetchRpc.Register()` ran only from
   `RegisterContent`, where `ZRoutedRpc.instance` is still null; the server silently had no `Husnisse_Fetch`
   method. It is now also registered from the server tick, beside the order and rule channels (the 0.3
   lesson, re-learned).
2. **A bowl addressed by ZDOID cannot be found on the server.** A ZDOID's user component is a per-peer key
   table entry, so the id the client holds is not the id the server files the claim under; every request was
   refused `NoClaim`. The request now carries the bowl's **position**, the same rule `RuleRpc` already uses.
3. **The consumer integrations stood down for the session.** They bind in `Awake`, before the plugin sets
   `Running`, and `CanConsume` required `Running`; both reported `off`. `CanConsume` now depends only on the
   config entry (the Quartermaster refusal is the real disable).
4. **A relaunched client replayed the previous probe command file** and quit on the stale `quit` before
   drawing a frame; the gate resets the probe file before every launch.

Rig notes: the harness gained `pin [x z]`, which puts the synthetic player at a spot and makes its body
**kinematic** — gravity is re-enabled every frame, so a pinned body at terminal velocity fell 200–700 m
between two commands and every order was refused as too far. `give <item> <amount> <slotY>` uses a fixed
grid row; a fresh character's flint occupied `(0,1)` and the give failed silently, so the scene fills from
row 3 after a `clear`.

## Rig changes

- `V:\Gate031` runs the 0.5 build plus the consumer pack; world renamed per run.
- Shared harness rebuilt with `pin` (`TestHarness.dll` sha256 `b57aa1d3318d`).
- Client probe rebuilt with `available / select / craft / craftopen / craftstate / aaa / burst`
  (`HusnisseGateClientProbe.dll` sha256 `4233bcae9216`).

## Release

**None.** No tag, no zip, no Hexium publish: 0.5 is internal only and publishing needs an explicit request.
