# 0.3c — overflow, deposit and deposit-all: the headless gate

Run 2026-11-15 on the rig's spare tree `V:\ValheimTest2` (port 2656, world `MTTest`), dedicated
server, no client. Same game build as the live server (`build_parity: MATCH E5E761C58099`).

- `Husnisse.dll` sha256 `4a02a00beb949dd899c8513da4bffb0e25cbb089b3f323b53bc5c16a6850973e` — the
  build that ran these scenarios. The tree's sources were unchanged after it (the only later edits
  are docs and scenarios); a rebuild of `HEAD` compiles clean.
- `TestHarness.dll` sha256 `dc172dfc90cef6f3973ab110ddb9266e133683ef1f65933b94fe82ff0be62791`
- plugins in the tree: Husnisse, TestHarness, Jotunn 2.30.2, ConditionalConfigSync 1.0.11,
  ValheimModding-YamlDotNet (detector + 16.3.1), ServerSoftStop, MineTogether
- `harness.log` is the whole run: 0.3b routing, 0.3b rules-edit, both 0.3c scenarios, then the 0.3a
  rule scenario; `Husnisse.log` is the mod's own diagnostics from the same boot.

## What was run

| Scenario | Result | Proves |
|---|---|---|
| `testrig/scenarios/husnisse-03c-overflow.cmd` | **PASS 14/14** | a full category chest is skipped, the next chest of the category (at any priority) is tried before Overflow, a stack whose every claimant is full goes to Overflow with `why=overflow (full: cat:metals)`, and a Tidy that can move nothing names both full rules |
| `testrig/scenarios/husnisse-03c-deposit.cmd` | **PASS 25/25** | a Deposit chest is drained as a source into the chests whose rules claim it; deposit-all on a networked chest hands the pockets to the nisse, which files them elsewhere; deposit-all on an off-network chest puts every stack in that chest, unsorted and at once, and nothing later moves them |
| `testrig/scenarios/husnisse-03b-routing.cmd` | **PASS 21/21** | no regression: every 0.3b routing case still lands where it did |
| `testrig/scenarios/husnisse-03b-rules-edit.cmd` | **PASS 10/10** | no regression: suggestion, first-toggle write, priority and copy still work |
| `testrig/scenarios/husnisse-03a-rules.cmd` | **PASS 16/16** | no regression: rule storage, `none`, an unknown version and the blacklist |

86 assertions, 0 failed.

The chest row in both scenarios is one prefab at `z=3`, `x = 2,4,6,8…`, so the harness (`container`,
`fill`, `interact`) and `husnisse_rule` address the same chest at the same index.

## Overflow: a full chest stops claiming the stack

The 0.3b gate already proved that an item *no rule claims* lands in Overflow. 0.3c is the other
half: a stack the rules **do** claim, when the chests that claim it are full.

```
# 1. priority holds while the preferred chest has room
spawn CopperScrap  →  piece_chest_wood[1] stacks=1   (cat:metals p1, not the p0 chest)
# 2. fill the p1 chest (5x2 = 10 slots) and drop a second metal
fill Coal 2000 [1] →  stacks=10 items=451            (full: 9 coal stacks + the copper stack)
spawn IronScrap    →  piece_chest_wood[0] stacks=1   (the p0 metals chest, not Overflow)
# 3. fill the p0 chest too and drop a third metal
spawn BronzeScrap  →  piece_chest_wood[2] stacks=1   (Overflow)
# 4. full category and a full Overflow: Tidy a chest holding another metal
interact piece_chest_wood alt 3 → the stack stays, and the reason is written
```

The two lines that matter, verbatim from `Husnisse.log`:

```
[Flow] landed moved=1 target=piece_chest_wood(Clone) at=(6,3) why=overflow (full: cat:metals)
[Flow] unmatched $item_blackmetalscrap why=no chest has room for $item_blackmetalscrap (full: cat:metals+overflow) from=piece_chest_wood(Clone) claim=-1279604915:1403
```

The decision recorded in `README.md` and `StorageRouter`: **room is decided per stack**. A chest with
no empty slot and no stack of that item cannot take the stack, so it drops out of the running and the
next chest of the same category — at any priority — is preferred over Overflow. Overflow is for what
nothing that claims the stack can hold *today*; a shipping order that pushes a stack past a
preferred chest must not empty it into Overflow while a same-category chest has room.

Before this card the behaviour was already correct by accident (the full chest failed the room test
and was skipped silently); what was missing was the explicit branch, the test, and the wording. The
`full:` note is also written when a Tidy routes nothing, because "unmatched=1" alone does not tell a
player whether a rule is missing or a chest is full.

## Deposit: a chest that is a source

A `Deposit` chest is never a destination (`NetworkCandidates` excludes it). From 0.3c it is a
*source*: on every reconcile tick the scheduler reads the deposit chests in the claim, asks the
router where each stack belongs (with the deposit chest itself excluded), and queues the ones with a
home. A stack it cannot place stays in the chest. The chest must be **on the network** to be drained
— an off-network chest is not part of the claim's work at all.

`FlowQueue.InFlight` keeps the reconcile from re-queueing a stack that is already waiting in the
queue or riding on the nisse's back: the item object is the key, so the same physical stack is never
in two tickets. This is the fix for the failure mode a periodic scan invites — with a 5 s tick and a
10 s round trip, the same stack would otherwise be queued three times and two of the tickets would
die at the source.

The scenario puts a metal, timber and an unclaimed item into the deposit chest and asserts it empties
itself:

```
fill piece_chest_wood CopperScrap 10 2 / Wood 20 / Feathers 5
container piece_chest_wood[2] → stacks=0 items=0        (the deposit chest is empty)
container piece_chest_wood   → stacks=1 items=10       (metals chest)
container piece_chest_wood[1] → stacks=1 items=20      (wood chest)
container piece_chest_wood[3] → stacks=1 items=5       (Overflow, the unclaimed item)
[Flow] landed moved=1 ... at=(2,3) why=cat:metals
[Flow] landed moved=1 ... at=(4,3) why=cat:wood
[Flow] landed moved=1 ... at=(8,3) why=overflow
```

## Deposit-all: one button, two halves, decided by the chest

The button lives in the container window next to the network toggle. Which half runs is the chest's
own state, read from its ZDO:

- **on the network** — the request becomes Empty Pockets: the server asks the player's client for the
  stacks, the client hands them over, the server drops them by the bowl and the ordinary trips file
  them by rule. The chest the button was pressed on gets nothing by right.
- **off the network** — the stacks go straight into that chest, unsorted, in one move. This is the
  player's own action (the same inventory write as a manual drag), so it needs no fed nisse, no
  claim and no queue.

```
# on the network (chest 0 = metals)
pocketfill CopperScrap 5 Wood 5      → stacks=2 items=10
depositall piece_chest_wood 0        → net=on  result=True after stacks=2 items=10  (queued)
pockets                              → stacks=0 items=0
container piece_chest_wood   → items=15     (10 + 5 copper, merged)
container piece_chest_wood[1] → items=25    (20 + 5 wood)
# off the network (chest 4)
pocketfill CopperScrap 5 Wood 5      → stacks=2 items=10
depositall piece_chest_wood 4        → net=off result=True after stacks=0 items=0  (immediate)
container piece_chest_wood[4] → stacks=2 items=10   (both, unsorted)
container piece_chest_wood   → items=15     (unchanged)
container piece_chest_wood[1] → items=25    (unchanged)
# 25 s later, untouched by the nisse:
container piece_chest_wood[4] → stacks=2 items=10
[Orders] deposit-all dumped=2 left=0 container=piece_chest_wood(Clone) at=(10,3)
```

The two `after` lines are the branch visible in one number: the networked dump leaves the stacks in
the pockets until a trip lands, the off-network dump empties them at once.

## Facts this gate added

- `piece_chest_wood` is **5×2 = 10 slots** (`husnisse_prefabs` dump: `piece_chest_wood 5 2`). A
  "the chest is full" test therefore fills it with nine coal stacks plus whatever it already holds,
  not twenty.
- `Inventory.AddItem(GameObject prefab, int amount)` adds **one stack**, not the requested amount.
  The harness `fill` command looped once and reported success, so a 2000-coal fill put in 50. It now
  loops until the amount is reached or the inventory refuses.
- The harness grew `pocketfill <item> <amount> …` (stacks in the synthetic player's pockets without
  giving an order), `depositall <prefab> [index]` (through `OrderService.RequestDepositAll`), and an
  optional index on `interact <prefab> [alt] [index]` (a Tidy cannot be aimed at the first chest
  only).
- The rig lost a test server mid-run (process gone, no shutdown lines, no WER report or event-log
  entry: an external kill, not a crash). The whole gate was re-run afterwards on a clean boot, and
  that run is the one in `harness.log`.

## Not proven by this gate

- The **button**. Deposit-all is wired into the container window, but no client clicked it in this
  run; the harness calls the same entry point a click calls. Filed with the 0.3b panel in the client
  gate card.
- The **client half of the off-network dump**: on a real client the write goes through the game's
  container inventory (the open chest's owner), which this process cannot exercise. The headless run
  proves the branch and that nothing is queued; the write itself is the game's own path.
- A narrow screen and a controller for the third button; MyLittleUI beside the strip.
- Throughput at scale (200 containers) and the fair queue — 0.3d's benchmark.
