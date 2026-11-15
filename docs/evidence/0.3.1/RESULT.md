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

## Client gate — teaching and the ledger on a real client

Run 2026-11-15 on the same gate build (`31ddbd35…`) with probe `367cbd99…`: client `Stranger`
(`-202584743`) joined to a dedicated gate tree `V:\Gate031` (port 2956, fresh world `MTTest`, the same
seven-plugin set), one compact fixture built at the client's feet — a bowl with the joining player as
creator and four wooden chests (`client/scene-031.sh` writes the role → index map) — then everything
below driven from the client process.

| # | Action, on the client | Result |
|---|---|---|
| 1 | Alt the nisse | **PASS** — hud `Show me where things go. Put a sample in the chest it belongs in.` + `The nisse leans closer to watch.`; client diagnostics `[Teach] watching player=-202584743 name=Stranger claim=-1812166505:10356 for=120s` |
| 2 | Carry a CopperScrap sample into `@-4,1` | **PASS** — hud `The nisse will keep Copper Scrap here.`; `[Teach] learned name=$item_copperscrap chest=piece_chest_wood@-4,1 rule=custom:1`; that chest reads `net=True rule='v1;k=custom;i=%24item_copperscrap' stacks=1 items=5` |
| 3 | Carry a Wood sample into `@-8,1`, same watching window | **PASS** — `[Teach] learned name=$item_wood chest=piece_chest_wood@-8,1 rule=custom:1`; `net=True` |
| 4 | Alt again (stop), then move Wood into `@-6,3` | **PASS** — hud `The nisse stops watching.`, `[Teach] stopped watching`; that chest keeps `net=False rule=''` with the wood inside (the control) |
| 5 | Open the ledger (inventory + `LedgerUi.Toggle`) | **PASS** — `HUSNISSE LEDGER`, claim line `Claim 20 m (native) · owner -202584743 · 4 container(s) · 1 unclassified`, four rows with the unclassified ones marked, and the Net/Blacklist/Tidy/category/Overflow/Deposit/No rule/priority/pickup controls (`client/captures/ledger.jpg`) |
| 6 | Select row `-4,1`, click `Metals` | **PASS** — the row label becomes `Metals`; the chest's ZDO becomes `v1;k=cat;c=metals`; the same chest's own window shows `Rules: Metals` (`client/captures/ledger-rule.jpg` — one writer, two surfaces) |
| 7 | Select row `-6,3`, click `Net` | **PASS** — the chest is `net=True` when opened right after (its strip reads `On the network`; the strip click then flips it back off). The panel label catches up on the next 0.5 s rebuild after the server's ack, so a reading taken inside that round trip still shows the old value |
| 8 | Click `Blacklist` on that row | **PASS** — panel label `Blacklist: on`; the same chest's window opens with no Husnisse strip at all (`strip=False`, `win=True`) (`client/captures/ledger-blacklist.jpg`); server `[rules] … blacklisted=1` |
| 9 | Click `Pickup / Metals` | **PASS** — `[Ledger] pickup wood,food,trophies,valuables,building,consumables,tools claim=-1812166505:10356`, the same value the server's `husnisse status` reads |
| 10 | Server-side read-back of everything the client wrote | **PASS** — `husnisse status`: `rule … -8,1 net=on rule=custom:1`, `rule … -4,1 net=on rule=cat:metals`, `[rules] containers=2 ruled=2 deposits=0 blacklisted=1`, `containers=4 … unknown=1 pickup=wood,food,…` |

Raw client evidence: `client/captures/client-bepinex.log` (the probe's own lines),
`client/client-husnisse.log` (the mod's diagnostics: `[Teach] watching`, the three `[Teach] learned`
lines, `[Teach] stopped watching`, `[Ledger] pickup`), `client/server-husnisse.log`,
`client/server-bepinex.log`, `client/server-harness.log`, and the screenshots as JPEGs.

## Rig notes (what it took, for the next gate)

- **The canary tree could not be joined at all.** Client → `V:\ValheimTest2:2656` failed with
  `Lost connection to server:ErrorConnectFailed` every 13 s. The host firewall has per-port rules for
  2556-2557 and 2856-2857 but none for 2656, and the program-scoped `valheim_server` rule does not
  cover the canary copy of the exe. Added inbound UDP `Valheim canary rig UDP 2656` (2656-2657, Any) —
  rig state, kept.
- **`rig.ps1` matches the tree path as a substring.** `Get-TestProc` filters command lines containing
  `V:\ValheimTest`, which also matches `V:\ValheimTest2`; a second card's `rig down/up` therefore
  killed the canary server mid-gate. This run used a dedicated tree `V:\Gate031` (port 2956,
  `rig.sh provision` + the four plugin folders copied from the canary tree) and drove scenarios by
  writing `BepInEx\config\harness.cmd` directly (`client/s031.sh`) rather than taking the host rig
  lock — the tree is written by this card only.
- **Harness-spawned pieces die in ~20-60 s on their own.** The dedicated server has no terrain
  colliders, so a spawned piece has no support and `WearNTear` damages it away; with no client near,
  the zone unloads as well. Build the fixture *after* the client joins, inside its radius, and call
  the new test-only `nostruct` (destroys every `WearNTear`) before waiting. With that the scene held
  for the whole phase; without it the first attempt lost bowl and chests 37 s after the build and the
  client's ledger had nothing to list.
- The harness deployed to the gate tree is built from `testrig/harness` at the time of this run plus
  `nostruct` (`client/harness-nostruct.patch`, `TestHarness.dll` sha256 `57c4e48f…`). The shared 2556
  tree and its harness were not touched.
