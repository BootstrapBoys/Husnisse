# 0.3a — 20-container UI sweep (the missing acceptance run)

Card `t_d3176117`. The literal gate sentence from the roadmap: **a 20-container base opened, toggled
and ruled one chest at a time through the UI, in one clean run.** This is that run.

| | |
|---|---|
| Build | Husnisse 0.3.0 on both sides, sha256 `e92f919a…` (the ack build) |
| Gate client | `BootstrapBoys-GateClientProbe` `ad20ed16…` (adds container in-use/distance/window fields and a `stand` that moves the rigidbody) |
| Server | `V:\ValheimTest`, port 2556, world `MTTest`, minimal set (Husnisse, Jotunn, ConditionalConfigSync, JsonDotNET, YamlDotNet, ServerSoftStop, TestHarness) |
| Client | `DESKTOP-PAOVLP7`, Steam Valheim, character `Stranger`, player id `-202584743` |
| Result | **20/20 chests toggled and ruled through the UI; client `containers total=24 net=20 ruled=20`** |

## The base

The fixture was restored (`rig.sh fixture restore`) so no earlier scene survived, then exactly one
scene was built and held (`hold on`): a 7 × 6 wood deck at y = 34.07, the bowl at (-4, -4) with
creator `-202584743`, and 20 `piece_chest_wood` on a 5 × 4 grid, 2 m apart (x = -2…6, z = -2…4), in a
native claim of radius 20. The scene script asserts its own shape before the sweep:

```
husnisse claim 83557706:15563 source=native ward=0:0 owner=-202584743 r=20 … containers=20
husnisse status [rules] containers=0 ruled=0
scenario done: PASS (3 assertions, 0 failed)
```

## The sweep

`docs/evidence/0.3a-ui/sweep-driver.sh`, one chest at a time, three probe batches per chest:

1. `close` + `stand piece_chest_wood <i>` — places the player beside the chest and **prints the
   container's own coordinates**, which are asserted against the scene's coordinate list before the
   chest is touched. This is the position addressing: an index whose coordinates are checked cannot
   drift onto a fixture chest the way a bare index did.
2. `open piece_chest_wood <i>` — the window opens (read back as `open gui=piece_chest_wood(Clone) … inUse=True owner=True`).
3. `click HusnisseNet`, `click HusnisseRules`, `click HusnisseCat<X>`, `click HusnisseRulesClose`, `ui`
   — every click goes through the button's own `onClick`; the categories cycle Metals, Wood, Food,
   Trophies, Valuables, Building, Consumables, Tools.

Per chest the client read back `net=True rule='v1;k=cat;c=<category>'`, and the strip drew
`rules='Rules: <Category>'`. Across the run the client invoked 20 × `HusnisseNet`, 20 ×
`HusnisseRules`, 20 × `HusnisseRulesClose` and 20 category clicks over 8 distinct categories.

## The numbers

The client's own container list, taken after the last chest:

```
gc containers total=24 net=20 ruled=20
```

`total=24` is the 20 chests plus the fixture's 4 world chests, none of which is on the network.
Per position, from the same dump (the base reads back as written, not just counted):

| chest | rule | chest | rule |
|---|---|---|---|
| (-2,-2) | `v1;k=cat;c=metals` | (2,-2) | `v1;k=cat;c=metals` |
| (-2, 0) | `v1;k=cat;c=wood` | (2, 0) | `v1;k=cat;c=wood` |
| (-2, 2) | `v1;k=cat;c=food` | (2, 2) | `v1;k=cat;c=food` |
| (-2, 4) | `v1;k=cat;c=trophies` | (2, 4) | `v1;k=cat;c=trophies` |
| (0,-2) | `v1;k=cat;c=valuables` | (4,-2) | `v1;k=cat;c=valuables` |
| (0, 0) | `v1;k=cat;c=building` | (4, 0) | `v1;k=cat;c=building` |
| (0, 2) | `v1;k=cat;c=consumables` | (4, 2) | `v1;k=cat;c=consumables` |
| (0, 4) | `v1;k=cat;c=tools` | (4, 4) | `v1;k=cat;c=tools` |
| (6,-2) | `v1;k=cat;c=metals` | (6, 0) | `v1;k=cat;c=wood` |
| (6, 2) | `v1;k=cat;c=food` | (6, 4) | `v1;k=cat;c=trophies` |

The server logged the same twenty writes, in sweep order, all from the joining player:

```
[debug][Rules] rule written piece_chest_wood v1;k=cat;c=metals by player -202584743
… (20 lines, metals → wood → food → trophies → valuables → building → consumables → tools ×2,
   then metals, wood, food, trophies)
```

`verify-sweep.sh <positions> <logdir>` reproduces the table and prints `verified=20 failed=0 of 20`.

## Relog

The client was quit through the game (`Application.Quit`, so the character saves and keeps its player
id), relaunched with `-joinserverwithcharacter`, and one chest was opened again:

```
gc stand piece_chest_wood(Clone)[12] at 4.00,34.17,-2.00 -> player 5.60,34.37,-2.00
gc ui ui container=piece_chest_wood(Clone) strip=True net='On the network' … rules='Rules: Valuables' … panel=False win=True
gc ui zdo net=True rule='v1;k=cat;c=valuables' inUseFlag=False owner=True
gc containers total=24 net=20 ruled=20
```

Screenshots: `client/gate-03a-sweep-strip.png` (the two buttons on an open chest, `On the network` /
`Rules: Valuables`) and `client/gate-03a-sweep-panel.png` (the panel open with Valuables selected).
The base of 20 chests is visible behind the window in both.

Evidence bundle: `client/sweep-evidence.txt` (table, scene build, both acceptance lines, the 20 server
writes, the click counts and the relog read-back).

## Why the earlier attempts lost the run — and the traps for the next one

1. **A teleport suppresses the container window.** `Player.TeleportTo` holds `IsTeleporting()` true for
   at least 2 s (`Player.UpdateTeleport`), and `InventoryGui.Update` calls `Hide()` every frame while
   it is. A chest opened right after a teleport therefore closes on the next frame, and the click
   batch finds no buttons. The probe's `stand` now moves the rigidbody directly (`Place`), which
   leaves no such state. `TeleportTo` also refuses a second call within 2 s (`m_teleportCooldown < 2f`)
   and needs `ZoneSystem.FindFloor` at the target — that is why some teleports did nothing at all.
2. **A bare `transform.position` write is reverted** by the character's rigidbody; set `body.position`
   as well, or the player never actually stands where the probe says they will.
3. **The server only has a claim while a bowl *object* exists.** A dedicated server instantiates no
   pieces outside its zones, so after any later harness scenario resets `hold` the pieces are culled:
   `husnisse status` reads `claims=0` even though the bowl's ZDO is present, and every rule write is
   then refused. Build the scene and run the sweep in one session — do not queue another scenario in
   between.
4. **A second scene build under `hold on` leaves duplicates.** Observed: 44 containers with two
   chests at seven positions, because the world had been saved between builds. Restore the fixture
   for a clean base; `cleanup` alone is not enough once the save holds the pieces.
5. **The probe skips a command batch identical to the last one** (`_lastCommands`). Every batch now
   carries a timestamp comment (`pc.sh`).
6. **A leftover `quit` in the probe command file quits the next launch immediately** — the file is
   replayed from scratch when the probe starts. Reset it to `# idle` before relaunching.
7. The client's own log is per launch; copy the per-chest probe output before a relog overwrites it
   (`sweep-driver.sh` keeps one log per chest).

## Not covered here

- Buttons hidden on a container outside a claim, a blacklisted container clicked through, two clients
  on one chest, controller/narrow screen: unchanged from `RESULT.md`.
- The rule *content* is one category per chest; priority, custom lists and the copy/suggest controls
  are 0.3b and were not exercised.
