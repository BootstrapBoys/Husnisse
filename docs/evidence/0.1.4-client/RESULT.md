# 0.1.4 client visual gate — 2026-11-15

The 0.1.4 legs were headless-proven at implementation time. This is the pass a real client
made: it watched the replicated nisse, not the server's copy, so "it walks" and "it fades"
are statements about what a player sees.

## Environment

| | |
|---|---|
| Build under test | `Husnisse.dll` 0.2.1 sha256 `629a4614311a…` (`artifacts/debug/Husnisse.dll`) |
| Server | `V:\ValheimTest`, port 2556, world `MTTest`, `build_parity: MATCH` |
| Client | Windows guest `DESKTOP-PAOVLP7` (192.168.1.172), Steam-owned Valheim 1.0.21, BepInEx 5.4.23.6 |
| Client plugins | Husnisse 0.2.1, Mine Together 0.2.1, ConditionalConfigSync 1.0.11, Jotunn 2.30.2, YamlDotNet, JsonDotNET + the test observer `BootstrapBoys-LegsClientProbe` |
| Server mods | the same six mods, plus `ServerSoftStop`, `TestHarness` and the scene driver `BootstrapBoys-LegsVisual`; the rest of the 19-plugin pack was parked in `plugins.disabled` for the session and restored afterwards |
| Scene | 3×21 `wood_floor` deck at y = 55 (above the canopy), bowl at x = 0, chest at x = 15, a 3×3 `wood_wall` room around the chest with one 2 m doorway, later closed with one wall |

The client carried the server's mod set because Jotunn and WardIsLove disconnect a peer whose
mod list does not match; a seven-plugin client against the 19-plugin pack is refused at
`RPC_PeerInfo` (`Mods are not compatible`). Mine Together is a hard CCS requirement
(`ModRequired = true`), so it went onto the client rather than being exempted server-side.

## Results

| Check | Result | Evidence |
|---|---|---|
| Nisse walks the deck, continuous, ~2 m/s | **PASS** | client positions once a second: 1.37 → 3.06 → 4.94 → 6.87 → 8.77 (1.9 m/s); no step above 0.13 m in a frame |
| Walk animation plays (legs, not sliding) | **PASS** | animator `forward_speed` 1.77–1.92 while moving, 0.00 idle; `walk-nisse-0.2.1.jpg` mid-stride |
| Closed room: the nisse fades instead of sticking | **PASS** | client `NisseFade.Alpha` 1.00 → 0.24 → warp → 0.33 → 0.83 → 1.00; `fade-threshold-0.2.1.jpg` |
| Items still reach the chest through the fade | **PASS** | with the doorway closed the chest kept filling: 3 → 5 coal in the session at y = 41 and 2 → 4 at y = 55, with `fades=2` and `fades=4` on the server |
| Walkable doorway is walked, not faded | **PASS (0.2.0 build)** | doorway open, three coal delivered, client logged no fade; the walking code is unchanged in 0.2.1 |
| Debug log covers the legs | **PASS** | 13 `leg ->`, 12 `leg blocked`, 12 `fade across to`, 3 `leg waiting for navmesh` in `server-bepinex-live.log` |
| "The way is shut. The nisse steps through." on the client HUD | **NOT OBSERVED** | three separate fades photographed at 0.15–0.3 s intervals, no HUD line in any frame (see below) |
| `husnisse_status` trip phases in order | **NOT RUN** | status was polled only before and after trips, not during one |
| Relog with the nisse away from its bowl | **NOT RUN** | not exercised in this session |

## The defect this gate found

The fade played only on the server. `NisseMotion.FadeAcross` sent its RPC with
`_nview.InvokeRPC(FadeRpc, destination)`, which addresses the ZDO **owner** — on a dedicated
server that is the server itself. The server's body dissolved where nobody could see it, and the
client received only the position update:

```
0.2.0  lc t=9  pos=(2.46, 40.10, 0.00) alpha=1.00
       lc t=10 pos=(13.89, 40.10, -0.12) step_sum=11.53 alpha=1.00   <- visible teleport
       lc t=11 pos=(1.36, 40.10, 0.00)  step_sum=12.31 alpha=1.00
```

Fixed in 0.2.1 by addressing every peer (`BroadcastFade`). The same trace afterwards:

```
0.2.1  lc fade out start t=16.13 pos=(9.09, 55.10, 0.00) alpha=0.98
       lc t=16 pos=(9.11, 55.10, 0.00) step_sum=0.13 alpha=0.24 busy=True
       lc fade in t=16.74 pos=(12.64, 55.09, 0.00) alpha=0.33
       lc fade in t=16.90 pos=(13.01, 55.10, 0.00) alpha=0.83
       lc t=17 pos=(13.53, 55.10, 0.00) step_sum=4.59 alpha=1.00
```

The body reaches alpha 0.03–0.24 before the 4.5 m warp and is back to full within 0.9 s, so the
move reads as the nisse stepping through. Server side, `fades=4` and the chest kept filling.

## What is still open

- **The HUD line was never seen.** The fade, the warp and the delivery all happen; the
  "The way is shut…" line did not appear in any of the 40+ frames captured around three fades,
  with more than the 60 s per-claim throttle elapsed. `NisseVoice.Say` calls
  `player.Message(MessageHud.MessageType.TopLeft, …)` on the server's player object. That call is
  the next thing to prove or replace: it may only write to the local HUD, in which case the voice
  never reaches a player. This is a finding, not a pass.
- **Relog with the nisse away from its bowl** was not exercised.
- **`husnisse_status` phases during a trip** were not sampled mid-trip.
- **World time**: the session ran into night, which is why the deck was rebuilt above the canopy
  and lit with standing torches; the later frames are dawn/morning.

## Repro

```bash
# build and deploy
cd ~/projects/valheim/nisse && bash tooling/build.sh
cd ~/projects/valheim/testrig && ./rig.sh down
./rig.sh deploy ~/projects/valheim/nisse/artifacts/debug/Husnisse.dll BootstrapBoys-Husnisse
./rig.sh deploy ~/projects/valheim/nisse/probe/legs-visual/artifacts/HusnisseLegsVisual.dll BootstrapBoys-LegsVisual
./rig.sh up

# client (guest, console session)
#   plugins: Husnisse + the six pack mods the server runs, plus HusnisseLegsClientProbe.dll
#   scheduled task (LogonType Interactive) starts:
#   valheim.exe -console -joinserverwithcharacter 192.168.1.136:2556 nissetester -password ***

# scene, then the trip (server commands are written to V:\ValheimTest\BepInEx\config\legs-visual.cmd)
build 0 55 0 x      # deck, bowl, chest
room                # 3x3 room with a 2 m doorway
close               # wall the doorway
# client commands are written to %USERPROFILE%\husnisse-probe.cmd
#   cam 8 58.6 -9 8 55.5 0     fixed camera (the game camera drifts; `face` is only rough)
#   watch 32 0.15              per-frame alpha/anim sampling + screenshots

# coal at the closed doorway, so the chest leg has to fade:
cd ~/projects/valheim/testrig
./rig.sh scenario ~/projects/valheim/nisse/docs/evidence/0.1.4-client/door-coal.cmd
```

The probes are test-only: `probe/legs-visual` (server scene driver, `husnisse`-free) and
`probe/legs-client` (client observer: position steps, animator blend, fade alpha, screenshots).
Neither ships.

## Files

- `walk-nisse-0.2.1.jpg` — the nisse mid-stride on the deck (0.2.1)
- `fade-threshold-0.2.1.jpg` — the nisse mid-dissolve at the closed doorway (0.2.1, alpha 0.24)
- `walk-nisse-0.2.0.jpg` — the walk on the previous build, daylight, for comparison
- `client-bepinex.log` — client BepInEx log (the `lc` trace and the mod's own lines)
- `client-husnisse.log` — the mod's diagnostics dump on the client
- `server-bepinex-live.log` — server BepInEx log (debug `leg`/`fade` lines)
- `server-husnisse.log` — the mod's diagnostics on the server
- `legs-visual.log` — the scene driver's own log
- `light.cmd`, `door-coal.cmd` — the harness helpers used to light the deck and place the coal
