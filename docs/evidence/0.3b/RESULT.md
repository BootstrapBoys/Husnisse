# 0.3b — categories, priority and routing: the headless gate

Run 2026-11-15 on the rig's spare tree `V:\ValheimTest2` (port 2656, world `MTTest`), dedicated
server, no client. Same game build as the live server (`build_parity: MATCH E5E761C58099`).

- `Husnisse.dll` sha256 `ceef65632f3ef81b7523c486020ef17ee18b42a4f63581cb1f133af9b7597303` — the build
  that ran these three scenarios. `HEAD` after it removes two helpers with no callers
  (`StorageRouter.FindTarget`, `ContainerNetwork.OnNetwork`) and builds clean as
  `e748e90733d8b2f5c287d417df08cda2492815dd5d90178d981412e6e76dcea9`; neither change can alter a
  routing decision, so the gate was not re-run for it.
- plugins in the tree: Husnisse, TestHarness, Jotunn 2.30.2, ConditionalConfigSync 1.0.11,
  ValheimModding-YamlDotNet (detector + 16.3.1), ServerSoftStop, MineTogether
- the whole log of the run is `raw.log`: the 0.3a rule scenario first (no regression), then both
  0.3b scenarios, then `Husnisse.log`

## What was run

| Scenario | Result | Proves |
|---|---|---|
| `testrig/scenarios/husnisse-03a-rules.cmd` | **PASS 16/16** | 0.3a's rule storage still behaves: 20 containers, 20 rules, `none`, an unknown `v2`, the blacklist |
| `testrig/scenarios/husnisse-03b-routing.cmd` | **PASS 21/21** | a metal lands in the priority-1 metals chest and not the priority-0 one; wood lands in the wood chest and a second wood stack joins it; an item on a saved list lands there; an item no rule claims lands in Overflow; with the overflow chest off the network it lands in the plain networked chest; the deposit chest receives nothing; every landing names its reason |
| `testrig/scenarios/husnisse-03b-rules-edit.cmd` | **PASS 10/10** | the suggestion reads contents (2 metals stacks to 1 wood → metals); the first toggle writes it (`v1;k=cat;c=metals`); priority is stored in the rule (`p=2`); copy writes both neighbours and is idempotent (`targets=2 written=2`, then `written=0`) |

The chest row is one prefab at `z=3`, `x = 2,4,6,8,10,12,14`, so both the harness (`container`,
`zdo`) and `husnisse_rule` address the same chest at the same index.

The assertions that matter, verbatim from the harness log:

```
container piece_chest_wood[1] stacks=1 items=1      # CopperScrap -> metals p1
container piece_chest_wood  stacks=0 items=0        # the p0 metals chest stayed empty
container piece_chest_wood[2] stacks=1 items=1      # Wood -> wood
container piece_chest_wood[5] stacks=1 items=1      # BoneFragments -> the saved list
container piece_chest_wood[3] stacks=1 items=1      # Feathers -> overflow
container piece_chest_wood[4] stacks=1 items=1      # SurtlingCore -> the plain chest
container piece_chest_wood[6] stacks=0 items=0      # the deposit chest, every time
container piece_chest_wood[2] stacks=2 items=2      # Resin joined the wood chest
```

and the mod's own line for each landing (`BSBLogs/Husnisse.log`):

```
[Flow] landed moved=1 target=piece_chest_wood(Clone) at=(4,3) why=cat:metals p=1
[Flow] landed moved=1 target=piece_chest_wood(Clone) at=(6,3) why=cat:wood
[Flow] landed moved=1 target=piece_chest_wood(Clone) at=(12,3) why=custom:1
[Flow] landed moved=1 target=piece_chest_wood(Clone) at=(8,3) why=overflow
[Flow] landed moved=1 target=piece_chest_wood(Clone) at=(10,3) why=no rule
```

Raw logs: `raw.log` (harness + `Husnisse.log` from the run), `harness.log` (the routing run alone).

## The preset table, and how it stays honest

The eight presets are not a list typed from memory. `husnisse_categories dump` wrote every item
`ObjectDB` holds in this build, with its item type and satiety, and the names in `CategoryMap` come
from that dump; `husnisse_categories check` is the same read again and reports what the table misses.

```
category metals      game=25  named=25
category wood        game=11  named=11
category food        game=128 named=36
category trophies    game=72  named=0     (item type is the whole answer)
category valuables   game=15  named=15
category building    game=9   named=9
category consumables game=29  named=27
category tools       game=34  named=28
categories items=1102 covered=318 gaps=277 overlaps=0 gear=507
```

`overlaps=0` matters: an item two presets both claim would make routing depend on preset order.
`gaps=277` is the honest remainder — hides, pelts, bones, boss drops and crafting odds and ends that
no preset family covers. They route to Overflow or to a plain chest, and a player who wants one
filed puts it on a saved list. Full list in `categories-audit.txt`.

One thing the dump changed in the design: **Valheim files most raw meat and every uncooked meal as
`ItemType.Material`, not `Consumable`** (e.g. `$item_wolf_meat` food=50, `$item_bakedpoteitr_uncooked`
food=37). Food is therefore "anything with satiety", and the mead bases — also materials — are named
into Consumables instead.

## Not proven by this gate

- The panel itself: the priority row, the checkbox list, the suggestion button and the copy confirm
  are built and wired, but no button was clicked on a client in this run (`probe/gate-client` carries
  the steps; see the handoff).
- Two clients on one chest, a controller, and a narrow screen — unchanged from 0.3a.
- Throughput at scale (200 containers) — 0.3d's benchmark.
