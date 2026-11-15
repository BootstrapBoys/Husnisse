# 0.3.1 — teaching, ledger, pickup filter, pocket merge, rules file: evidence

Build under test: the gate worktree `~/projects/valheim/nisse-031-gate` at commit `321fff9`
(`0.3.1: teaching, the ledger, the pickup filter and the rules file`).

- `Husnisse.dll` sha256 `31ddbd35417c1c82a956139478f159382a32079886bc8d24abf4600dfa109344`
- `HusnisseGateClientProbe.dll` sha256 `367cbd99819a3c1de6aad8b35a882c6071d1983f47841d7f1083c715c4a9e616`
- Run 2026-11-15 on the rig's spare tree `V:\ValheimTest2` (port 2656, world `MTTest`), dedicated
  server, no client in-world; same game build as the live server (`build_parity: MATCH E5E761C58099`).
- Plugins in the tree: Husnisse 0.3.1, TestHarness, Jotunn, ConditionalConfigSync,
  ValheimModding-YamlDotNet (detector + 16.3.1), ServerSoftStop, MineTogether.

## Headless gate — 70 assertions, 0 failed

| Scenario | Result | Proves |
|---|---|---|
| `testrig/scenarios/husnisse-031-teach.cmd` | **PASS 19/19** | Alt-interact the nisse starts watching; three samples in one window teach three chests (`net=on rule='v1;k=custom;…'`), the samples stay in the chests, three taught types then route by name from the floor; after `teachoff` the same move into a fourth chest writes nothing (`net=False rule=''`) |
| `testrig/scenarios/husnisse-031-ledger.cmd` | **PASS 25/25** | `husnisse_ledger list|unknown` prints the model (claims/rows/unknown/pickup); classify, rule, priority, net and blacklist edits land in the same ZDO the container window reads; the pickup filter suppresses a copper drop while wood still lands and releases it when metals is ticked again; Empty Pockets merges two partial copper stacks into one before queuing (`pockets merged units=10`) |
| `testrig/scenarios/husnisse-031-rules.cmd` | **PASS 26/26** | `husnisse_rules preset metals $item_coal` narrows the preset live (`category metals count=1`; coal routes to the higher-priority custom chest, copper stays on the floor), `builtin` restores it and the copper lands; `export` → mutate every rule → `plan` → `apply` restores all three; destroying one chest makes `plan` report `unmapped=1` and `apply` refuse, with the surviving chests untouched |

The raw runs are in this folder (`husnisse-031-*.log`, `harness-0.3.1.log`, `husnisse-0.3.1.log`).

Regressions on the same build: `husnisse-03b-routing` 21/21, `husnisse-03c-overflow` 14/14,
`husnisse-03a-rules` 16/16, `husnisse-03d-blacklist` 19/19.

## What the runs show

```
teachmove piece_chest_wood[0] item=CopperScrap amount=5 interacted=True moved=True net=True rule='v1;k=custom;i=%24item_copperscrap' chest[stacks=1 items=5] player[stacks=0 items=0]
teachmove piece_chest_wood[1] item=Wood amount=5 interacted=True moved=True net=True rule='v1;k=custom;i=%24item_wood' chest[stacks=1 items=5] player[stacks=0 items=0]
[Teach] watching player=0 name=... claim=-534288907:2325 for=120s
[Teach] learned name=$item_copperscrap chest=piece_chest_wood@3,3 rule=custom:1
[Teach] learned name=$item_wood chest=piece_chest_wood@6,3 rule=custom:1
[Teach] learned name=$item_barley chest=piece_chest_wood@9,3 rule=custom:1
[Teach] stopped watching player=0
[Flow] landed moved=1 target=piece_chest_wood(Clone) at=(3,3) why=custom:1 already holds it src=ground
```

```
ledger claim=-1825257555:2341 source=native owner=0 r=20 rows=4 networked=0 ruled=0 unknown=4 pickup=all presets=builtin
ledger wrote piece_chest_wood[0] 3,3 net=on rule=cat:metals
[Ledger] pickup wood,food,trophies,valuables,building,consumables,tools claim=-1825257555:2341
[Orders] pockets merged units=10
```

```
rules preset metals $item_coal presets=file:metals
category metals count=1
yaml row 1 claim=1 file=piece_chest_wood 3,3 target=piece_chest_wood 3,3 net=True rule=cat:metals@2 blacklist=False
yaml row 2 claim=1 file=piece_chest_wood 6,3 target=piece_chest_wood 6,3 net=True rule=custom:$item_coal,$item_wood@3 blacklist=False
yaml row 3 claim=1 file=piece_chest_wood 9,3 target=piece_chest_wood 9,3 net=True rule=cat:wood blacklist=False
rules apply applied=3 claims=1
rules plan ok=True unmapped=1
rules apply refused: plan has 1 unmapped
```

## Defects found and fixed inside this card

- **Food's named set was dead.** `CategoryMap.Accepts(Food)` only checked `m_food > 0`, so seeds,
  crops, flour and spices — the pantry half the table documents — classified as nothing and routed
  nowhere. `Accepts` now consults the named set too.
- **A refused order said nothing anywhere.** `OrderService.Report` showed the player a line but wrote
  no diagnostics, so a headless run of "the order did nothing" had no reason in the log. Every verdict
  is now a `[Orders] order <kind> -> <result>` line.
- **`Teach.Active` keyed on the player id,** which is 0 on a synthetic player and on a client before
  its character ZDO is authored — the window never opened in either case. It keys on the local
  `Player` object now.
- **The harness spawned players fell through the world** (no zones on a dedicated server → no terrain
  colliders, y ≈ −90 m), so every order was refused `TooFar`. The harness pins the synthetic player at
  the bowl and turns gravity off.
- **An export's custom list was written as `custom:3`** (the display form) and parsed back as a list
  containing the item "3". `ContainerRule.ToShorthand()` is the round-trippable form now, and an export
  is sorted by position so a diff is readable.
- **Importing an unedited export turned every preset into a file override.** A set identical to the
  shipped one is no longer stored.

## Client gate

The UI/ledger client proof needs the one client rig, which a concurrent card was driving on port 2556
for the whole of this run (its client stayed connected and its probe kept issuing commands). The client
half therefore could not be recorded from this card; the exact checklist it must cover is
`docs/PLAYTEST.md` §5c, and the probe already carries the commands it needs (`ledger`,
`ledgerpanel`, `click <button>`, `alt <prefab>`, `move <prefab> <item> <amount>`). Everything the
client would click reaches the same `LedgerActions` the headless scenarios exercised, and the actions'
effects are read back from the ZDO in those runs — but a click on the panel itself is not yet proven
on screen.
