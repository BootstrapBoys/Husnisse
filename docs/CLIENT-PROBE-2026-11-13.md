# Client probe — 2026-11-13

First real client-side run of Husnisse 0.1.2 on the Windows client VM
(`192.168.1.172`, Steam Family Sharing install). The probe was injected as
`BepInEx/plugins/HusnisseClientProbe*/HusnisseClientProbe*.dll` and removed
after the run.

## Result

| Check | Result | Evidence |
|---|---|---|
| Client reaches in-world | pass | `clientprobe: player=NisseTester pos=(-3.06, 34.07, -0.65) server=True` |
| Bowl UI opens | pass | `clientprobe: interact=True`; screenshot `/home/hermes/.hermes/cache/scratch/husnisse-client-probe.png` |
| Hover text localised | pass | `clientprobe: hover=Porridge Bowl` |
| Pantry dimensions | pass | `clientprobe: pantry slots=4x1 items=1` |
| Non-food refused | pass | `coal can=False added=False items=0` |
| Food accepted | pass | `food=AsksvinEgg can=True added=True items=1` |
| Food guard registration | pass | `guard isBowl=True coalIsFood=False` |
| Client startup | **fail** | `Awake` aborts before `core ready`; `Player.log` stack below |

## Root cause found

`BepInEx/LogOutput.log` and
`AppData/LocalLow/IronGate/Valheim/Player.log` show:

```
InvalidOperationException: Steamworks is not initialized.
  at Husnisse.Localization.Loc.T (...)
  at Husnisse.Localization.Loc.Initialize ()
  at Husnisse.HusnissePlugin.Awake ()
```

`Loc.T` evaluates `global::Localization.instance` in its null check. That
getter throws before Steamworks is initialised on a client. The exception
aborts `HusnissePlugin.Awake` before `Loc.Initialize` completes, before
`Harmony.PatchAll`, and before `RegisterContent` — so the shipped 0.1.2
client has no patches, no bowl prefab, no order RPC and no nisse factory.

The probe worked around this by calling `RegisterContent` and applying the
assembly's Harmony patches itself. It also used a local cauldron clone because
the client never received the `husnisse_bowl` prefab in `ZNetScene`; the
server-spawned bowl was not replicated to this client either.

## Server side

The same run proved the server half on `MT-Test`:

- `bowl fields: category=Misc station=none resources=1 enabled=True table=_HammerPieceTable`
- `bowl registered as husnisse_bowl`
- `nisse settled` after the claim
- `bowl: pantry ready 4x1 (4 slots)`

## Not proven yet

- drain timer and `Bowl Drain Minutes`
- Alt feeding toggle under `Feeding Mode=Optional`
- order RPC from a properly registered real client
- invulnerability under a raid
- persistence across relog
- server-spawned bowl replication to a client

## Cleanup

- Server plugins restored from `plugins.disabled`; `plugin_count=19`; rig down.
- Client probe plugin folders removed; game process stopped.
- No firewall or host configuration was changed.
