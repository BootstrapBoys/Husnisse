# 0.3d — blacklist, animation LOD, fair queue: the headless gate

Run 2026-11-15 on the rig's spare tree `V:\ValheimTest2` (port 2656, world `MTTest`), dedicated
server, no client. Same game build as the live server (`build_parity: MATCH E5E761C58099`).

- `Husnisse.dll` sha256 `a21c60a81c4a8f2a01e9bd45da230aa3dee4867d5d6b9ee0ea85305236c3d055` — the build
  that ran every scenario below; no source change after it.
- `TestHarness.dll` sha256 `02f439fc73bcb11033c777b304bdba2ca752affac225601ff2b4678fa01bbee8`
- plugins in the tree: Husnisse, TestHarness, Jotunn, ConditionalConfigSync, ValheimModding-YamlDotNet
  (detector + 16.3.1), ServerSoftStop, MineTogether
- The rig's harness log is per boot, so the runs are kept as one file per boot:
  `Harness-blacklist.log`, `Harness-fair-bench.log`, `Harness-regressions.log`,
  `Harness-03b-routing.log`, `Harness-03b-rules-edit.log`, with the mod's own diagnostics beside them.

## What was run

| Scenario | Result | Proves |
|---|---|---|
| `testrig/scenarios/husnisse-03d-blacklist.cmd` | **PASS 19/19** | a cart and a ship inside a claim ship blacklisted and never offer the buttons while an ordinary chest beside them does; `husnisse_rule blacklist` hides one container and takes it off the network; a blacklisted chest is never a destination (a metal drop stays on the floor until the flag is released, then lands) |
| `testrig/scenarios/husnisse-03d-fair.cmd` | **PASS 13/13** | the queue hands out one task type in turn (`order=Ground,Container,Ground,Container,…`) and a deposit chest whose contents wait behind a floor of 30 drops is served on the second trip — it empties while the ground lane still holds 28 tickets |
| `testrig/scenarios/husnisse-03d-bench.cmd` | **PASS 11/11** | 200 containers on one claim, all on the network; the benchmark records the pass (`docs/evidence/0.3d/BENCH.md`) |
| `testrig/scenarios/husnisse-03d-lod.cmd` | **PASS 7/7** | with no players the viewer distance is infinite and the claim reports `lod=far`; `husnisse lod apply 5` restores `Animator.enabled` with `AlwaysAnimate`, `apply 60` turns it off with `CullCompletely`, and the nisse stays bound to its claim either way |
| `testrig/scenarios/husnisse-03b-routing.cmd` | **PASS 21/21** | no regression: every routing case still lands where it did |
| `testrig/scenarios/husnisse-03b-rules-edit.cmd` | **PASS 10/10** | no regression: suggestion, first-toggle write, priority and copy |
| `testrig/scenarios/husnisse-03c-overflow.cmd` | **PASS 14/14** | no regression: full-category overflow and the `(full: …)` reason |
| `testrig/scenarios/husnisse-03c-deposit.cmd` | **PASS 25/25** | no regression: deposit chest as a source, deposit-all on and off the network |
| `testrig/scenarios/husnisse-03a-rules.cmd` | **PASS 16/16** | no regression: rule storage, `none`, an unknown version, the old blacklist flag |

136 assertions, 0 failed, all on the final build.

## The blacklist is two layers behind one predicate

`husnisse container <prefab>` prints the two answers the container window uses, from the mod itself:

```
husnisse container piece_chest_wood[0] at=(3.00, 1.00, 3.00) claim=True blacklisted=False ui=True
husnisse container Container[0]        at=(6.00, -4438.81, 3.05) claim=True blacklisted=True ui=False   # a cart
husnisse container piece_chest[0]      at=(9.00, 0.96, 4.30) claim=True blacklisted=True ui=False       # a ship hold
```

- `ui` is `ContainerNetwork.OffersUi(claim, container)` — the same function the button patch calls, so
  "a blacklisted container never shows a button" is decided once, not copied into the patch.
- A cart's own container object is called `Container`; a ship's hold is called `piece_chest`. Both are
  `Vagon`/`Ship` parents, so the filter sees the class, not the name.
- The flag round-trips: `blacklist on` → `blacklisted=True ui=False`, `[rules] … blacklisted=3`; a
  metal dropped beside it stays on the floor for 20 s (`drops within 6m of …: 1 of 1`), and after
  `blacklist off` + `net on` it lands (`stacks=1`).
- Class defaults follow the config: `ContainerFilter.DefaultBlacklisted` reads
  `4. Storage / Skip Carts and Ships` and `Skip Display Piles` (before this card it ignored them, so
  turning the toggle off changed nothing). A per-container flag adds a blacklist and never overrides a
  class; releasing a cart logs that it is still blacklisted by its class.

## The queue is fair, and a trip keeps its batch

```
husnisse fair queued=30 lane_ground=26 lane_container=4 lane_pockets=0 next=ground
husnisse fair paused=True order=Ground,Container,Ground,Container,Ground,Container
# 75 s later
container piece_chest_wood[2] stacks=0 items=0          (the deposit chest is empty)
container piece_chest_wood[1] stacks=1 items=15         (wood, five-stack batches)
husnisse queue queued=28 lane_ground=28 lane_container=0 lane_pockets=0 next=ground
[Flow] landed moved=4 target=piece_chest_wood(Clone) at=(4,4) why=cat:metals src=container
[Flow] landed moved=5 target=piece_chest_wood(Clone) at=(6,4) why=cat:wood src=ground
```

`husnisse fair` pauses the scheduler (`1. General / Enabled`, restored in a `finally`), clears the
claim's queue, runs one real `FlowScheduler.Refill`, prints the lanes, dequeues six tickets for their
source order, and puts them back. One harness command, so no scheduler tick can drain the queue between
the refill and the observation — the first attempt asserted across two ticks and was flaky.

The behavioural half is judged by two numbers that cannot be faked: a metal stack left the deposit
chest (a container trip ran, with its whole four-stack batch) **while** the ground lane still held 28
wood tickets. A FIFO queue would have to empty the ground lane first and would leave all four metal
stacks in place.

## Defects found and fixed inside this card

- **Class defaults ignored their own toggles.** `DefaultBlacklisted` hard-coded carts/ships/display
  piles, so `Skip Carts and Ships = false` did nothing. Fixed; the per-container flag is the only thing
  that is stored.
- **Fairness cost the batch.** Filling a trip from the rotation made every trip carry one stack
  whenever two lanes had work (the second ticket had a different destination and was requeued). A trip
  now takes its first ticket from the rotation and fills the rest of its budget from that same lane
  (`FlowQueue.TryDequeueFrom`), so fairness is across trips and a batch is still five.
- **`containers=` was overloaded.** The new `[flow]` status line printed `containers=<queued container
  tickets>`, which collided with the claim line's container count and broke three older scenarios'
  numeric assertions (`expect_ge containers=(\d+) N` matched the flow line's 0). The lanes are
  `lane_ground` / `lane_container` / `lane_pockets` now.
- **A stack could be queued twice.** `FlowQueue.InFlight` compared `ItemData` references, and writing a
  container's ZDO reloads its inventory into new `ItemData` objects — observed as `containers=4`
  growing to `8` on the next reconcile, and four stale tickets dropped as invalid on the next trip.
  Identity is now the stack's slot, count and shared name within the same container, which survives the
  reload.
- **Item drops spawned at `ground` can fall out of this world.** A spawned cart ended at y = −4438 and
  the fair scenario's 30 drops ended far away after a while. The fairness scenario no longer asserts on
  drop positions; it reads the deposit chest and the queue, and the `drops` command now prints
  `nearest=`/`farthest=` so the next person can tell "the drops are gone" from "the drops fell".
- **The test server was killed externally three times mid-run** (no shutdown lines, no WER report: the
  host is shared with another card's client gate). Each verdict above is from the final build, and the
  runs are kept per boot.

## Not proven by this gate

- The **buttons** on a client (0.3a/0.3c's client gate owns that), and MyLittleUI / a narrow screen
  with three buttons in the strip.
- **Animation LOD as a picture.** The effect is proven on a live body (`husnisse lod apply` flips
  `Animator.enabled` and `CullCompletely`; a dedicated server reports `lod=far` because it has no
  players), but whether the transition reads well from 30 m is a client question — added to
  `docs/PLAYTEST.md`.
- `Skip Carts and Ships = false` making carts eligible: it needs a config change and a restart, and the
  class default is config, not state.
- Display-pile detection: no display-pile mod is installed on the rig, so the `DynamicStorage` type-name
  path has no subject to run against.
