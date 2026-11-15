# Order-channel client gate — runbook

The headless proof is done (`tests/10-husnisse-orders.cmd` 15/15, `scenarios/husnisse-orders-toggle.cmd`
6/6, `scenarios/husnisse-orders-handover.cmd` 4/4 on build `2da40c5002da`). What is left is the same
checks from a **real client**, which needs the one Windows client machine (192.168.1.172). The steps
below are the exact sequence; every file they name is in this folder.

## What is already staged

| Thing | Where | Value |
|---|---|---|
| Build under test | `nisse-orders/artifacts/debug/Husnisse.dll` (clean worktree at HEAD + the identity refactor) | `2da40c5002da4e8e2b53b758f614687855fbef11c4f747cd5cf946ca4ac57e92` |
| Probe | `probe/gate-client/artifacts/HusnisseGateClientProbe.dll` (adds `setpos`) | `c00c6bd4c1968be65d436bd8160ee940df20859dca9bed47749ff6695f6c6199` |
| Server tree | `V:\ValheimOrders`, port 2856, `MTTest` fixture, `Feeding Required=false`, firewall rule `Valheim orders rig UDP 2856` | stopped, deployed with `2da40c50` |
| Client stage | `C:\Users\Hermes\stage\Husnisse.dll` and `HusnisseGateClientProbe.dll` | both uploaded |
| Launch task | `launch-orders.ps1` registers `HusnisseOrdersLaunch` → joins `192.168.1.136:2856` | not registered yet |

## Server

```bash
cd /home/hermes/projects/valheim/testrig
export RIG_DST='V:\ValheimOrders' RIG_PORT=2856
./rig.sh fixture restore && ./rig.sh up        # READY in ~60 s
```

## Client

```bash
cd /home/hermes/projects/valheim/nisse
# 1. install (refuses if valheim.exe is running - the DLL is mapped)
bash tooling/client-rig/psrun.sh docs/evidence/order-channel-2026-11-15/client/install-client.ps1
# 2. launch and join 2856
bash tooling/client-rig/psrun.sh docs/evidence/order-channel-2026-11-15/launch-orders.ps1
# 3. wait for `gc ready` in the client log; read the player id from that line
```

## Scene

The scene is built at the **client's** player id (the bowl's creator is the claim owner):

```bash
python3 docs/evidence/order-channel-2026-11-15/make-scene.py <playerId> \
  > docs/evidence/order-channel-2026-11-15/client/gate-orders-scene.cmd
cd /home/hermes/projects/valheim/testrig
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh scenario \
  ../nisse/docs/evidence/order-channel-2026-11-15/client/gate-orders-scene.cmd
```

Deck surface `38.56` at `(-30.17, -0.05)`, bowl `(-32.17, 38.56, -0.97)`, chest A with 6 coal
`(-28.17, 38.56, -0.97)`, chest B (target) `(-28.17, 38.56, 2.03)`. The bowl's pantry gets 3 CookedMeat.

## Client batches

Send one batch at a time and read the probe's log lines (`gc ...`) plus the server's
`BepInEx/BSBLogs/Husnisse.log`:

| # | Batch | Expected |
|---|---|---|
| b01 | `where`, `state` | the client's position and the bowl the mod sees |
| b02 | `setpos -30.17 39.5 -0.05` | player on the deck (`setpos ... now=` confirms) |
| b03 | `state`, `hover husnisse_bowl` | `feeding=False`, hover `This nisse asks for nothing` |
| b04 | `alt husnisse_bowl` | server `[Bowl] feeding=True by <id>`; HUD `The nisse will expect offerings.` |
| b05 | `state`, `hover husnisse_bowl`, `shot` | `feeding=True`, hover `Satiety <n>/100 ... [Alt] no offerings needed`; server `[Bowl] ate ...` |
| b06 | `alt husnisse_bowl` | server `[Bowl] feeding=False`; HUD `The nisse will work without offerings.` |
| b07 | `state`, `hover husnisse_bowl` | `feeding=False`, hover back to `This nisse asks for nothing` |
| b08 | `give Coal 6 1`, `give Wood 12 1`, `inv` | two non-hotbar stacks |
| b09 | `use husnisse_nisse` | HUD `The nisse takes 2 stack(s).`; server `[Orders] empty pockets queued=2`; handover log lines |
| b10 | `inv`, `containers`, `state`, `shot` | the player's row 1 is empty; a chest holds the 42 items |
| b11 | `containers`, `alt piece_chest_wood` | HUD `The nisse files N stack(s).`; server `[Orders] tidy queued=N` |
| b12 | `containers`, `state`, `where` | chest A empty, chest B holds the coal/items |

`probe.sh -f client/batches/bNN-*.cmd` sends a batch. The probe polls
`%USERPROFILE%\husnisse-probe.cmd` every 0.5 s and skips identical text.

## What the server log must show

`V:\ValheimOrders\BepInEx\BSBLogs\Husnisse.log` (rewritten every boot):

```
[Bowl] feeding=True by -202584743
[Bowl] feeding=False by -202584743
[Orders] empty pockets queued=2
[Orders] tidy queued=2
```

A client sitting in world is **not** evidence: the hover line, the HUD lines and these server lines are.
If a line is missing, read the server's `BsbLogs` and `LogOutput.log` for
`order RPC dropped: sender=... claimed=...` — that is the identity path refusing, not a UI problem.

## The scripted path (used on 2026-11-15, both checks PASS)

The manual batches above were replaced by two scripts; both are in this folder and both were run
against build `165f728b`:

```bash
# tree: reset to the fixture and booted first
cd /home/hermes/projects/valheim/testrig
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh fixture restore && \
RIG_DST='V:\ValheimOrders' RIG_PORT=2856 ./rig.sh up

# one unattended attempt: quit the client if it is up, install this card's build + probe,
# join 2856, build the scene at the reported `gc ready` position, run the batches
bash docs/evidence/order-channel-2026-11-15/client/full-pass.sh        # log: /tmp/full-pass.log
```

`full-pass.sh` refuses if the installed `Husnisse.dll` is not `165f728b` (the stage folder's canonical
file names are shared with other cards — install from `Husnisse-orders-165f728b.dll` and
`HusnisseGateClientProbe-orders-c00c6bd4.dll`). `run-tidy-gate.sh` is the inner half: generate the
scene from a position, run it, drive `batches/t01…t06`.

Two things the scripted path depends on:

- **the client must be free.** Two Valheim processes share `BepInEx/LogOutput.log` and only one
  survives, so check `(Get-CimInstance Win32_Process -Filter "Name='valheim.exe'").CommandLine` and the
  rig command lock first; another card's run on `V:\ValheimTest` (2556) will kill this one.
- **the chest contents are read on the server.** A client-spawned copy of a server-spawned chest
  reports `stacks=0` even after the server has the items; the scene therefore dumps
  `container piece_chest_wood 0/1` 120 s in (`make-scene-at.py`), and that dump is the evidence for
  "the source emptied / the destination gained the coal".

