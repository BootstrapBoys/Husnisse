# Client gate runbook — drain, Alt, orders, raid, relog

Card: `t_a9aba3e7`. Five checks on a real client against a settled native claim.

| | |
|---|---|
| Server | `V:\ValheimTest`, port 2556, world `MTTest` |
| Client | `DESKTOP-PAOVLP7` 192.168.1.172, Steam install, BepInEx 5.4.23.6 |
| Client driver | `probe/gate-client` (`BootstrapBoys-GateClientProbe`) |
| Server driver | `testrig/artifacts/harness/TestHarness.dll` (`event`, `damage`, `fill`, `spawn`, `husnisse`) |
| Build under test | `Husnisse.dll` — hash recorded in RESULT.md |

## Rig shape

Both sides run a minimal, matching set: Husnisse, Jotunn, ConditionalConfigSync,
YamlDotNet, JsonDotNET. Server adds `ServerSoftStop` and `TestHarness`; the other 12
plugin folders are parked in `plugins.disabled` for the session and restored afterwards.

Config (server, while stopped):

- `2. Nisse / Feeding Required = false` — per-bowl opt-in, which is what the Alt check needs.
- `2. Nisse / Bowl Drain Minutes = 1` — one item a minute instead of twenty.
- `1. General / Debug Logging = true`, `2. Nisse / Allow Orders = true`.

## Order of checks

1. **Scene** — 3x3 wood floor at the client's feet, bowl with creator = the joining
   player, chest A (6 coal) and chest B (empty) inside the 20 m claim.
2. **Alt feeding** — `alt` the bowl: hovered line says the nisse asks for nothing; after the
   Alt the ZDO flag flips and the hover line shows satiety. Toggle off and on again.
3. **Drain** — 3 cooked meat in the bowl, watch 90 s: one item eaten at 60 s, satiety rises.
4. **Orders** — `alt` chest A (Tidy) and `use` the nisse (Empty Pockets); the server queues,
   the nisse walks, items arrive in the target chest.
5. **Raid** — `event foresttrolls` at the player, `damage husnisse_nisse 500 3` during it;
   the nisse's health does not move and the mod logs the ignored damage.
6. **Bowl UI + relog** — open the bowl (pantry UI), photograph it, close, quit the client,
   rejoin: feeding flag, satiety and pantry contents are the server's, not the client's.

## Evidence

`docs/evidence/client-gate-2026-11-15/`: client BepInEx log, server BepInEx log,
`BSBLogs/Husnisse.log`, harness log, screenshots, `RESULT.md`.
