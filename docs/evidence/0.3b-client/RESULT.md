# 0.3b client gate — the rule panel on a real client

Cards `t_3f054844` (items 1–6) and `t_a8bd00fc` (items 7–12). What this run proves, what it found,
and what is still open.

| | |
|---|---|
| Build under test | `Husnisse 0.3.0`, sha256 `d35096e2…` — HEAD `28ee63a` + the three UI fixes, built in a clean worktree (`~/projects/valheim/nisse-gate03b`) so sibling cards' uncommitted work could not ride along |
| Probe | `BootstrapBoys-GateClientProbe` `d1804ced…` (adds `panel`, `res`, a visibility check on `click`, and `move` into the open container) |
| Server | `V:\ValheimTest`, port 2556, world `MTTest` (regenerated for this run — see *Rig notes*), minimal set (Husnisse, Jotunn 2.30.2, ConditionalConfigSync, JsonDotNET, YamlDotNet, SoftStop, TestHarness) |
| Client | `DESKTOP-PAOVLP7`, Steam Valheim `1.0.21`, character `Stranger`, player id `-202584743` |
| Scenes | items 1–6: `scene.sh` (ten chests + cart, ~27 m across). Items 7–12: `scene-compact.sh` (nine chests, every piece within ~5 m of the base). Item 12: `readonly-scene.sh` |
| Driver | `driver.sh` (phases p0–p13), `verify.sh` (one PASS/FAIL line per check), `analysis.py` (panel geometry), `collect.sh` (logs + screenshots) |
| Fix re-run | build `cd1f5f54…` (commit `bf32ae0`) + probe `6dd07418…`, phases `p10 p10relog p11 p12` on a fresh `MTTest`; `verify.sh` 21/0 over sections 10–12 (see *Fixed, and re-run on the client*) |

## Proven on the client (this build)

| # | Card item | Evidence |
|---|---|---|
| 1 | A claim chest shows the strip; a cart shows none | parent run: `p1-ui0.txt` `strip=True netClickable=True rulesClickable=True`; `p1-ui-cart.txt` `strip=False`. **Those two logs and their screenshots were overwritten by an aborted re-run and now live in `client/logs/aborted-1021/`** — the readings are as recorded on card `t_3f054844`, not re-derivable from disk |
| 2 | Network toggle is world state and survives close/reopen | `p2-before/after/read.txt`: `Off the network` → `On the network`, `zdo net=True`, still on after closing and reopening |
| 3 | Panel renders, 8 categories in 4×2, all controls present, and **nothing covers the vanilla grid** | `p3-panel-read.txt`, `p3-panel-narrow.txt`, `layout.txt`: 8 categories / 4 columns / 2 rows, `grid-overlap 0.0` at 1920×1080 **and** at 1024×768, `pairs … overlapping=0`; screenshots `gate-03b-panel-1080p.png`, `gate-03b-panel-1024x768.png` |
| 4 | A category writes the rule and paints the button | `p4-read.txt`: `Rules: Metals`, `v1;k=cat;c=metals`, `HusnisseCatMetals … color=#FFD65C` |
| 5 | Priority `+` / `−` | `p5-plus-read.txt` `priority='1'`, `v1;k=cat;c=metals;p=1`, strip reads `Rules: Metals p1`; `p5-minus-read.txt` back to `0` |
| 6 | `Save contents`: rows `[x] name xN`, untick one, Apply, reopen | `p6-rows.txt` `Save these items (3)`; `p6-untick-read.txt` row 1 `[ ] Bone Fragments x5`; `p6-applied.txt` strip `Rules: Save contents (2)`; `p6-reopen-read.txt` still unticked. Screenshots `gate-03b-save-contents.png`, `gate-03b-save-applied.png`, `gate-03b-save-reopen.png` |
| 6b | The list routes: the named item goes in, the unnamed one to Overflow | `p7-watch.txt` `The nisse takes 2 stack(s).` (server side: `[Flow] trip … why=custom` and `why=overflow`) |
| **7** | **`Use suggestion` on a non-empty chest with the rule cleared, and clicking it writes the category** | `p8-before-clear.txt`: chest `Rules: Wood` (fixture), `suggestActive=False`; `p8-clear.txt` `click HusnisseNoRule invoked`; `p8-suggest-read.txt`: `rules='Rules: No rule'`, `zdo rule='v1;k=none'`, `suggestActive=True suggest='Use suggestion: Metals'`, button `vis=True clickable=True`; `p8-suggest-done.txt`: `Rules: Metals`, `zdo net=False rule='v1;k=cat;c=metals'`. Screenshot `gate-03b-suggest-available.png` |
| **8** | **`Copy to nearby`: confirm line, Cancel changes nothing, Copy sets both neighbours** | `p9-confirm.txt` `status='Copy this rule to 2 nearby container(s)?'`; `p9-cancelled.txt` both neighbours still `rule=''` (dump in the same batch); `p9-yes.txt` `hud center='Rule copy sent for 2 container(s).'`; `p9-containers.txt` both neighbours `rule='v1;k=cat;c=metals'`. Screenshots `gate-03b-copy-confirm.png`, `gate-03b-copy-done.png` |
| **9** | `Deposit all` on a **networked** chest: the nisse files the metal and the timber elsewhere, the chest pressed on gets nothing | **PASS on the fix** (`bf32ae0`, `cd1f5f54…`) — `p10-click.txt` `hud center='The nisse takes 2 stack(s).'`; `p10-containers.txt` idx=0 metals `stacks=1 items=5 [copperscrapx5] zdosts=1`, idx=4 wood `stacks=1 items=10 [woodx10] zdosts=1`, idx=5 (the pressed chest) `stacks=0 zdosts=0`; `p10-relog.txt` after a full relog holds the same three readings. On `d35096e2` it failed — see “The defect this gate found” |
| **10** | `Deposit all` on an **off-network** chest: every pocket stack lands there at once, HUD count, and a later reopen still shows them | `p11-click.txt` `hud center='Deposited 3 stack(s).'`, `p11-read.txt` `inv stacks=0`; `p11-reopen.txt`: the dump chest's line reads `stacks=3 … zdosts=3` after close+reopen. Screenshot `gate-03b-deposit-offnet.png`. |
| **11** | The same chest with one slot left: one fits, two do not | `p12-fill.txt` `stacks=9` in the ten-slot chest; `p12-read.txt` `hud center='Deposited 1 stack(s); 2 would not fit.'`, `inv stacks=2 [obsidianx4,deerhidex4]`. Screenshot `gate-03b-deposit-full.png` |
| **12a** | Another player's bowl (`424242`): the strip is **read only** and a click is skipped | `p13-read.txt`: strip drawn, `name=HusnisseNet … clickable=False`, `name=HusnisseCustom … clickable=False`; `p13-click.txt` `click HusnisseNet label='Off the network' interactable=False` → `skipped: not interactable`; `p13-click-rules.txt` `click HusnisseRules skipped: not interactable`. Screenshot `gate-03b-readonly.png` |
| **12b** | A chest with no bowl near it: no strip | **not captured cleanly** — the no-claim chest was culled by the rig before a window-open reading; the best reading (`strip=False`, `dist=1.6`) came with `win=False`, so it cannot carry the row. See *Open*. |

`Deposit all` stays clickable in the read-only case on purpose (`OnDepositAllClicked`: “anyone who can
open the chest can leave their things in it”, the server refuses the order outside a claim); the
read-only pair is **Network + Rules**, and both are greyed.

## The defect this gate found (item 9) — the nisse's deposit is lost

The nisse removes the stacks from the player and logs a successful transfer, but the items never
reach any container. Proven three ways on one run:

1. `hud center='The nisse takes 2 stack(s).'` — the order was accepted.
2. The server files them: `[Flow] trip deposit moved=1 target=piece_chest_wood(Clone) at=(64,-11)
   why=cat:wood` and `[Flow] trip deposit moved=2 target=… at=(64,5) why=cat:metals`.
3. The client's `containers` dump shows `stacks=0` in both chests — and still `0` after a full
   client relog (a fresh `Container.Awake` reading the world's ZDOs), which rules out a stale cache.

Root cause: `Container.Save` is owner-gated (`m_nview.IsOwner()`), and the mod writes the deposit
straight into the container's inventory from the server without claiming the ZDO. On this rig every
harness-spawned container has an unowned ZDO — `zdoset piece_chest_wood … 0` answers
`zdoset: not the ZDO owner` — so the write stays in the server's in-memory `Inventory` and is never
serialised. The same thing happens in a real game to any container whose ZDO belongs to a player:
opening a chest claims its ownership, which is exactly what a player does when they configure it.

**Verdict on routing (the part item 9 was written to prove): it is correct.** The timber went to the
`cat:wood` chest; the copper went to the **copy target** of p9, not the `cat:metals` chest, because
copying the metals rule in item 8 gave that nearer-to-the-bowl chest the same claim on metals and the
documented tie-break is *nearest the bowl*. The pressed-on chest received nothing.

Follow-up: card `t_ab336800` — *"Deposit-all: the nisse's landed stacks vanish when the container ZDO
is not server-owned"*.

### Fixed, and re-run on the client (2026-11-15)

`Storage/ContainerWrite` (commit `bf32ae0`, build `cd1f5f54…`) takes the target's ZDO for the server
before the write and verifies the packed inventory reached it; `Trip.Land`, `DumpPockets` and the sulk
tidy-up all go through it. The headless proof is in `../0.3c/RESULT.md` (`husnisse-03c-deposit-persist`:
FAIL 7/18 on `d35096e2`, PASS 18/18 on `cd1f5f54`).

The client re-run: `driver.sh stopclient install launch scene p10 p10relog p11 p12` on
`V:\ValheimTest`, world `MTTest` regenerated, client `Stranger` and build `cd1f5f54…`/probe `6dd07418…`
hash-checked by `install`. Item 9's row, from `p10-containers.txt` and the relog `p10-relog.txt`:

```
idx=0  metals  net=True rule='v1;k=cat;c=metals' stacks=1 items=5  [copperscrapx5] zdosts=1   ← after relog too
idx=4  wood    net=True rule='v1;k=cat;c=wood'   stacks=1 items=10 [woodx10]       zdosts=1   ← after relog too
idx=5  plain   net=True rule=''                  stacks=0 items=0                 zdosts=0   ← the chest the button was pressed on
```

`[Flow] landed moved=1 … why=cat:metals zdo=1` and `moved=2 … why=cat:wood zdo=1` on the server side.
The wood chest reads `woodx10` because that trip carried **two** wood stacks (`[Flow] trip stacks=2
pickups=1`) — the order queued one and a second joined it as a ground pickup — not because the fix
duplicated anything; the pockets ended at `stacks=0` and the world's wood came from both tickets.
p11 (off-network dump: `Deposited 3 stack(s).`, all three in the chest's ZDO, still there on reopen) and
p12 (one fits: `Deposited 1 stack(s); 2 would not fit.`, the two stay in the pockets) also pass.
`verify.sh`: **21 pass / 0 fail over sections 10-12**; the 10 rows it still prints for sections 8-9 are
"no log for this run" — those phases were not re-run here (they were proven on `d35096e2` in the
previous item and their logs are archived under `client/logs/pre-deposit-fix/`). p13 stays open.
Screenshots `gate-03b-deposit-offnet.png` / `-full.png` are the earlier run's; the re-run's `shot`
batches printed their paths but did not rewrite the PNGs.

Two gate traps this re-run paid for, now handled in `driver.sh`:

- **The character's inventory is saved in the profile, not the world.** A "fresh world" still starts
  with the last run's pockets, so p10 asked for 2 stacks and the nisse took 4, and p11/p12's counts
  were off. The probe grew `clear` (empty the pockets, `Inventory.RemoveAll`) and p10/p11 start with it.
- **The client's copy of a target ZDO follows one revision behind.** p10's first dump read `zdosts=0`
  for the timber the server had already written (zid matched, `zrev` one behind the server's). p10 now
  polls the dump (10 × 3 s) until both destinations show the deposit, and `p10relog` reads it again
  from a fresh client process.

`driver.sh` also pins the join task's port (`GATE_PORT`, default 2556) before launching — the task is
one global scheduled task and the 0.3.1 gate had pointed it at its own tree — and grew `stopclient`
(install needs the client stopped, its plugin DLLs are locked while it runs).

## Defects the earlier panel gate found (all fixed in `t_3f054844`)

1. **The rule panel covered the container grid.** Centred on the window, `470×252` covered 83% of the
   grid at 1080p (`grid-overlap 102558` of `103379` px²) and the whole grid at 1024×768. Fixed by
   anchoring both panels to the window's top-right corner (`ContainerRulesUi.BuildPanel`). After the
   fix: `grid-overlap 0.0` at both resolutions.
2. **`Save contents` stayed disabled when the chest's contents changed while the panel was open.**
   Now gated on the container's stack count, and the preview re-reads on a contents signature change
   (keeping the player's ticks — `CustomKnown`).
3. **Clicking a saved-list row did nothing.** `OnCustomRowClicked` indexed `CustomRowNames`, never
   filled; now indexes `CustomNames`. Before/after on the client: row 1 `[x]` → `[ ]`, Apply →
   `Save contents (2)`.

## What is still open

- **Item 12b (no claim → no strip).** The readonly scene puts the no-claim chest 28 m from the only
  bowl, and this rig destroys harness pieces that leave the client's radius before a window-open
  reading can be taken. The structure is the same `claim == null → Hide()` branch that item 12a and
  the earlier cart case exercise, but it is not client-proven in this session.
- **`verify.sh` is keyed to the wide fixture.** Its p8–p13 expectations hard-code the `scene.sh`
  positions (`$(pos 4 2)` …) and one message that this build sends to the HUD instead of the panel
  status line. Against the compact fixture it reports `verified=47 failed=32`; the new rows above are
  read straight from the logs. The verifier needs the role→index map instead of coordinates.
- Item 9's persistence path cannot be proven on this rig until the deposit fix lands **and** the
  harness spawns server-owned ZDOs (see Rig notes).

## Rig notes for the next session

- **Harness ZDOs are now server-owned** (changed for t_ab336800): `SpawnPrefab` calls
  `zdo.SetOwner(ZDOMan.GetSessionID())` and `ZNetView.ClaimOwnership()`, so a spawned piece behaves
  like the normal server-owned chest, `Container.Save` writes, and `cleanup` can remove it. Before
  that, `Container.Save` never wrote for a harness piece, `ZNetScene.Destroy` never removed its ZDO
  either, and every long session ended with several overlapping scenes and unusable
  `piece_chest_wood <index>` addressing. **Fresh world per run** is still the rule: a world that has
  been saved by a client carries those chests' ZDOs (and their owner keys) into the next boot, and a
  leftover chest shifts the position-sorted `piece_chest_wood <index>` the scenarios address. Rig
  procedure: `rig.sh down` → rename `V:\ValheimTest\saves\worlds_local\MTTest` to
  `MTTest.pre-<tag>` (kept, not deleted) → `rig.sh up` → `driver.sh launch`.
- **ZNetScene culls harness pieces aggressively.** A chest 5 m from the client vanished once, and a
  chest 11.9 m and the cart went within a minute of a scene build; nothing inside ~5 m was ever lost.
  Keep every piece within a few metres (`scene-compact.sh`), and read anything far *first*.
- **One scenario per world.** Each scenario load runs `Cleanup()` (which sets `HoldObjects = false`),
  so a scenario wipes the standing scene — including a read-only `husnisse status` scenario.
- **A strip click needs its own batch.** Opening a container and clicking one of its buttons in the
  same probe batch can run before `Refresh` has attached the chest (`_container` is cleared by
  `Hide()`); the click then reports `invoked` and does nothing. `p8` now opens, then clicks.
- **Do not interleave probe batches with a running driver phase** — two writes to
  `C:\Users\Hermes\stage\cmd.txt` race and the phase's later reads are lost.
- **`:watch` blocks the probe.** The next batch's commands are not processed until the watch ends;
  give the watch its full duration before the following batch.
- **`Player.TeleportTo` is unusable** (it hides the container window for ≥2 s); the probe's `stand`
  writes the transform and the rigidbody. `setpos` into a new area can bounce for a minute while the
  client's terrain settles — check `gc where` twice before building a scene there.
- **A running client locks its plugin DLLs.** Install only while `valheim.exe` is stopped, and read
  the hash back (`tools.ps1` now reports `COPY FAILED`). This run confirmed the deployed build was
  `d35096e2…`/`d1804ced…` before every phase.
- The client is a single shared resource; post “taking the client, N minutes” on the card.

## Files

`client/logs/*.txt` — one file per probe batch, exactly as printed (`aborted-1021/` holds the aborted
re-run's p0/p1 logs and must not be read as results); `client/logs/server-*.txt` — harness scenarios;
`client/verify.txt` — the verifier's line-per-check output; `client/layout.txt` — panel geometry at
both resolutions; `client/client-bepinex.log` — the client log; `client/gate-03b-*.png` — screenshots;
`scene.sh`, `scene-compact.sh`, `readonly-scene.sh`, `driver.sh`, `verify.sh`, `analysis.py`,
`collect.sh`, `boot-tree.sh` — the reproducible rig.
