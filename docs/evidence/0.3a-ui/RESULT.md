# 0.3a — container UI and rule storage

Card `t_fed257f1`. Two buttons in the top-right of a container window, a rule panel, and the rule as
world state in the container's ZDO, written by the server.

| | |
|---|---|
| Build | Husnisse 0.3.0 (`PluginVersion`/csproj/manifest), sha256 `e92f919a…` on both server and client (the ack build that ran the UI gate; `551459c1…` was the position-fix build before the ack, tested once and superseded) |
| Tree | `~/projects/valheim/nisse` at `8be7f7a` + the ack fix; the checkout also carried the sibling card's in-progress order-channel edits (one checkout, two runs) |
| Server | `V:\ValheimTest`, port 2556, world `MTTest`, minimal set (Husnisse, Jotunn, ConditionalConfigSync, JsonDotNET, YamlDotNet, ServerSoftStop, TestHarness) |
| Client | `DESKTOP-PAOVLP7`, Steam Valheim, character `Stranger`, player id `-202584743`, probe `BootstrapBoys-GateClientProbe` |
| Drivers | `probe/gate-client` (new `ui`, `click <name>`, `stand`, `open <prefab> [index]`, `containers` with ZDO values) and `TestHarness` (new `console <text>`) |

## What was built

- **UI** — `src/UI/ContainerRulesUi.cs` + `src/Patches/ContainerUi.cs`: a postfix on
  `InventoryGui.UpdateContainer` draws a two-button strip (`HusnisseNetworkStrip`) and a rule panel
  (`HusnisseRulesPanel`) as children of the container window. Categories: Metals, Wood, Food,
  Trophies, Valuables, Building, Consumables, Tools; roles: Overflow, Deposit; plus No rule and Close.
  The strip shows only inside a claim, never on a blacklisted container (carts, ships, display piles
  by default), and is disabled with a reason for a player the claim does not permit.
- **Storage** — `husnisse_net` (bool) and `husnisse_rule`, one versioned string
  (`v1;k=cat;c=metals;p=1;i=…`). `husnisse_blacklist` is read for the hidden case. Unknown versions
  decode to "no rule" and leave the stored value untouched.
- **Write path** — `src/Net/RuleRpc.cs` (`Husnisse_Rule` / `Husnisse_RuleAck`): the client asks, the
  server resolves the container **by position** and the claim from its own world, validates
  `ContainerFilter.CanUse` and `Claim.Allows(sender)`, writes the ZDO and acks the accepted value; the
  asking peer applies it to its own copy when it owns that ZDO (it does while the chest is open).
  Identity comes from the peer's character ZDO (`PeerIdentity`), which works with no `Player` objects.
- **Headless/admin** — `husnisse_rule list | net <prefab> <on|off> [index] | set <prefab> <rule> [index]`
  plus `[rules]` and per-container lines in `husnisse_status`.

## Headless gate — PASS

`testrig/scenarios/husnisse-03a-rules.cmd`: 20 chests in a claim, rules written through the mod's own
console path, read back from the ZDO.

```
08:33:35 scenario done: PASS (16 assertions, 0 failed)
```

Covered: a rule written as `v1;k=cat;c=metals` and read back (`zdo piece_chest_wood.husnisse_rule =
str='v1;k=cat;c=metals'`), the opt-in (`husnisse_net = int=1 bool=True`), all 20 containers configured
(`[rules] containers=20 ruled=20`, `husnisse_rule list` → `husnisse rules listed=20`), a cleared rule
(`v1;k=none`, counts drop to 19), an unknown `v2` encoding ignored (`ruled=18`, stored value untouched),
and the blacklist flag (`bool=True` on a flagged chest, `absent` on a clean one).

## Client gate — UI works; 20-container sweep signed off in `RESULT-SWEEP.md`

Real client, real container windows, every click through the button's own `onClick`:

```
gc open gui=piece_chest_wood(Clone) … inUse=True owner=True
gc ui ui container=piece_chest_wood(Clone) strip=True net='Off the network' netClickable=True rules='Rules: No rule' rulesClickable=True panel=False
gc click HusnisseNet invoked
gc after HusnisseNet ui … net='On the network' … ; gc after HusnisseNet zdo net=True rule=''
gc click HusnisseRules invoked        -> panel=True
gc click HusnisseCatMetals invoked
gc after HusnisseCatMetals ui … rules='Rules: Metals' … ; zdo rule='v1;k=cat;c=metals'
```

Server side, for the same clicks:

```
[debug][Rules] network on piece_chest_wood by player -202584743
[debug][Rules] network on piece_chest_wood claim=-1546731837:39829 player=-202584743
[debug][Rules] rule written piece_chest_wood v1;k=cat;c=metals by player -202584743
[debug][Rules] rule piece_chest_wood cat:metals claim=-1546731837:39829 player=-202584743
```

Persistence across a client relog, read back from the world by the same UI:

```
gc ready player=Stranger id=-202584743 pos=… world=MTTest peers=1
gc ui ui … net='On the network' … rules='Rules: Valuables' … ; zdo rule='v1;k=cat;c=valuables'
```

Screenshots: `client/gate-03a-chest-window.png` (two buttons at the top-right of the chest window on a
20-chest deck), `client/gate-03a-rule-panel.png` (the panel: eight categories, Overflow/Deposit/No
rule, Close). Client log: `client/client-bepinex.log`.

### The 20-container UI sweep — signed off (card `t_d3176117`)

The literal gate sentence — a 20-container base opened, toggled and ruled one chest at a time through
the UI, in one clean run — is proven in `RESULT-SWEEP.md`: a restored fixture, one built scene (bowl
`-202584743` + 20 chests), the 20 chests clicked through in order, and the client's own line

```
gc containers total=24 net=20 ruled=20
```

before and after a full client relog (one chest read back as `v1;k=cat;c=valuables`). The three failed
attempts lost the run for the reasons now documented there: a position-sorted index pointed at fixture
chests, earlier scene builds survived a second `cleanup`, and — the decisive one — `Player.TeleportTo`
holds `IsTeleporting()` true for at least 2 s while `InventoryGui.Update` hides the container window
every frame, so a chest opened right after a teleport closes before it can be clicked.

## Not proven / known limits

- Buttons on a container outside a claim: the branch (`claim == null → hide`) is shared with the
  no-container case that was observed (`strip=False`), but a client-side chest outside the claim could
  not be opened on this rig (a server-owned chest cannot be opened without a `Player` object on the
  dedicated server). The claim for (60, -4) is not covered by the bowl at (26, -8), r=20.
- A blacklisted container's hidden buttons were not clicked through; the flag read path is headless-proven.
- Two clients on one chest; a client that does not own the ZDO when the rule changes (the ack is
  skipped and the owner's copy wins — by design, but unexercised).
- A controller or narrow screen (the strip autosizes, but unverified).

## Rig state at hand-off

Server left running on `V:\ValheimTest` with the minimal set (my `setup-rig.ps1` parked 13 folders;
`restore-rig.ps1` + `restore-disabled.ps1` put the 19-folder baseline back, config reset by the same
script). Client left at its base set with the 0.3a build and the probe installed; stage
`C:\Users\Hermes\stage` still holds `Husnisse-03a.dll`, `cmd.txt`, `client-evidence.zip`.
