# 0.2.2 — the nisse's warning line reaches the client HUD

The 0.1.4 client gate found a defect it could not explain: the threshold fade played on the
client, but "The way is shut. The nisse steps through." never appeared on the client's HUD in
40+ frames around three fades. This is the run that found the cause and proved the fix.

**Short version:** the line was addressed to `Player.GetAllPlayers()`, and a dedicated
server has **no `Player` objects at all** — the list is empty, so the loop that was supposed
to shout to the room iterated zero times. The line was built, throttled, and handed to nobody.
`Player.Message` was never even called. The audience is now the connected peers, each addressed
through the game's own `ShowMessage` handler.

## Environment

| | |
|---|---|
| Build under test | `Husnisse.dll` 0.2.2 sha256 `3f48f75371858475e4cd0e388b3d368898f24d695c7b960c3fe7cbbdc52e74ca` (`artifacts/debug`) |
| Server | `V:\ValheimTest`, port 2556, world `MTTest`, `build_parity: MATCH E5E761C58099`, 9 plugins loaded |
| Server plugins | Husnisse 0.2.2, MineTogether 0.2.1, ConditionalConfigSync 1.0.11, Jotunn 2.30.2, JsonDotNET, YamlDotNet, ServerSoftStop, TestHarness, `BootstrapBoys-LegsVisual` (scene driver). The other 11 pack folders were parked in `plugins.disabled` for the session and restored afterwards. |
| Client | Windows guest `DESKTOP-PAOVLP7` (192.168.1.172), Steam-owned Valheim 1.0.21, BepInEx 5.4.23.6 |
| Client plugins | the same six mods as the server, plus `BootstrapBoys-LegsClientProbe` |
| Character | `Stranger` (a fresh character; the probe reports which one joins) |
| Scene | 3×21 `wood_floor` deck at y = 55, bowl at x = 0, chest at x = 15, 3×3 `wood_wall` room around the chest with one 2 m doorway, doorway walled shut, 7 fuel-free green torches (`light-55.cmd`) |

The client carried the server's mod set because Jotunn and WardIsLove disconnect a peer whose
mod list does not match. Mine Together is a hard ConditionalConfigSync requirement, so it was
installed on the client for the session rather than exempted server-side.

## The cause, measured

Two lines from the scene driver settle it, before any mod code is involved:

```
route players=0 peers=1 isServer=True
route Player.GetAllPlayers() is empty on this peer
route peer uid=265214798 name="Stranger" ready=True refpos=(2.01, 33.84, 2.01) character=265214798:1
```

`Player.GetAllPlayers()` returns nothing on a dedicated server: there are no player characters
to simulate, so `NisseVoice.Say`'s audience loop ran zero times. The old code was:

```csharp
foreach (var player in Player.GetAllPlayers())      // empty on a dedicated server
{
    if (Vector3.Distance(player.transform.position, claim.Center) > claim.Radius + 15f) continue;
    player.Message(MessageHud.MessageType.TopLeft, …);
}
```

The recorded hypothesis was that `Player.Message` writes to the server's local HUD. It does —
but only when the server's copy of the character *is* the owner, and here the call was never
reached at all. What `Player.Message` does on this build (`assembly_valheim`, 1.0.21):

```csharp
if (m_nview.IsOwner()) { MessageHud.instance.ShowMessage(...); }   // local HUD
else                  { m_nview.InvokeRPC("Message", ...); }        // to the ZDO owner
```

and the handler is registered **only on the owner**: `Player.Awake` does
`if (m_nview.IsOwner()) m_nview.Register<int,string,int>("Message", RPC_Message)`. So the route
is real, and a dedicated server would have taken the `else` branch — but there was no `Player`
to take it on. Fixing the audience was the whole fix; the per-line delivery then had to exist.

## What was tried before the fix

A controlled A/B was set up: send `PROBE-A` through `Player.Message` and `PROBE-B` through
`ZRoutedRpc` "ShowMessage" to the same player, and read which arrived in the client's
`MessageHud.GetLog()`. Route A never fired, because the target list was empty. Route B arrived:

```
client: lc hud text "PROBE-B ShowMessage peer"
client: lc hud msg  "PROBE-B ShowMessage peer"
```

`MessageHud.instance.MessageAll()` proves the handler is the game's own: it is exactly
`ZRoutedRPC(Everybody, "ShowMessage", …)`. Addressing one peer is the same call with that
peer's uid.

## The fix

`NisseVoice` now derives the audience from `ZNet.instance.GetPeers()` — the only player-like
thing a dedicated server has — and delivers each line with
`ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.TopLeft, text)`.
Earshot is the claim radius plus 15 m, measured across the ground, matching how a claim decides
what it contains (a peer's reference position is exact in x/z). The host's own player is still
served locally, so a listen server and a single-player game behave as before.

The 60 s per-claim throttle is kept, and a line about something that just happened now has its
own slot: an idle remark cannot take the slot an event needs.

## Verification

| Check | Result | Evidence |
|---|---|---|
| `Player.GetAllPlayers()` is empty on a dedicated server | **CONFIRMED** | `legs-visual.log`: `route players=0 peers=1`; `route Player.GetAllPlayers() is empty on this peer` |
| Peer-addressed `ShowMessage` reaches a client HUD | **PASS** | `client-bepinex.log`: `lc hud msg "PROBE-B ShowMessage peer"` |
| The line is sent to the peer in earshot | **PASS** | `server-bepinex.log`: `voice: sent to Stranger uid=265214798 at 8.8 m`, `heard=1 reach=35` |
| The line appears on the client HUD, exact text | **PASS** | `client-bepinex.log`: `lc hud msg "Nisse: The way is shut. The nisse steps through."` — twice, two separate trips |
| The line is visible in a client frame | **PASS** | `hud-line-dissolve.jpg` (line + the nisse translucent mid-dissolve at the doorway), `hud-line-dawn.jpg` |
| The fade still plays on the client | **PASS** | `lc fade out start t=15.04 pos=(9.00, 55.10, 0.00) alpha=0.83` and `t=16.85 pos=(13.65, 55.10, 0.00) alpha=0.84`; `watch done … min_alpha=0.00 fades=2` |
| Items still reach the closed room's chest | **PASS** | `state … fades=2 chest=2` with the doorway walled |
| The 60 s throttle still holds | **PASS** | one `voice: Event` line per trip while two threshold legs were faded (`leg blocked` twice, one line) |

`Nisse:` in front of the sentence is the speaker prefix (`Loc.T(Loc.HandName) + ": " + line`),
not part of `Loc.VoiceUnreachable`.

## What this run did not prove

- **The ambient/event slot split is a guard, not a proven fix.** With `feeding_required=True`
  and an empty bowl, `FlowScheduler` stops the nisse (`Hungry` and `continue`) — so an idle
  remark and a threshold fade cannot currently contend for the slot. The split keeps that true
  if the scheduler ever lets them overlap.
- **A listen server and single-player were not run.** `DeliverLocally` (the host's own player)
  is the unchanged `Player.Message` path, exercised only by source reading here.
- **More than one player in earshot was not exercised.** The peer loop and the out-of-earshot
  branch are written but only a single peer was connected.
- The world ran into night, so the frames are lit by green standing torches and read green.

## Repro

```bash
cd ~/projects/valheim/nisse && bash tooling/build.sh
cd ~/projects/valheim/testrig && ./rig.sh down
./rig.sh deploy ~/projects/valheim/nisse/artifacts/debug/Husnisse.dll BootstrapBoys-Husnisse
./rig.sh deploy ~/projects/valheim/nisse/probe/legs-visual/artifacts/HusnisseLegsVisual.dll BootstrapBoys-LegsVisual
./rig.sh up

# client (guest, interactive session): Husnisse 0.2.2 + the six pack mods + the client probe
#   valheim.exe -console -joinserverwithcharacter 192.168.1.136:2556 nissetername -password ***

# server scene (BepInEx/config/legs-visual.cmd)
build 0 55 0 x
room
close
route                          # the routing experiment: players/peers + a marked HUD line
state

# client commands (%USERPROFILE%\husnisse-probe.cmd)
teleport 8 55.4 2              # only after the deck exists
cam 4 58 -7 12.5 55.3 0        # park the camera; the game camera drifts
watch 40 0.1

# coal at the closed doorway, so the chest leg has to fade:
./rig.sh scenario ~/projects/valheim/nisse/docs/evidence/0.1.4-client/door-coal.cmd
./rig.sh scenario ~/projects/valheim/nisse/docs/evidence/0.2.2-voice/light-55.cmd   # if it is dark
```

Two trip runs were made, the second more than 60 s after the first, so the throttle was seen
to hold and then release.

The probes are test-only: `probe/legs-visual` (server scene driver, `husnisse`-free) and
`probe/legs-client` (client observer: position steps, animator blend, fade alpha, the HUD's own
line log, screenshots). Neither ships.

## Files

- `hud-line-dissolve.jpg` — the line on the HUD with the nisse translucent mid-dissolve at the closed doorway
- `hud-line-dawn.jpg` — the same line on the lit deck as dawn comes up
- `client-bepinex.log` — client log: the `lc` trace, `lc hud msg` for both deliveries
- `client-husnisse.log` — the mod's diagnostics on the client
- `server-bepinex.log` — server log: `voice:` delivery lines, `leg blocked`, `fade across`
- `server-husnisse.log` — the mod's diagnostics on the server (claim radius, satiety, feeding policy)
- `legs-visual.log` — the scene driver's own log, including the `route` experiment
- `light-55.cmd` — the deck lighting used for the frames
