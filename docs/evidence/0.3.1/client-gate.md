# 0.3.1 client gate — what ran, and how to run it again

The gate is **recorded**: `RESULT.md` §"Client gate" has the ten checks and their results, and
`client/captures/` has the screenshots and the client log of the session. This file is the recipe that
produced it, kept so a rerun is a script rather than a rediscovery.

## The shape

One dedicated tree, one fixture, one client session, everything driven from the client process:

- Tree `V:\Gate031` on port **2956** — `RIG_DST='V:\Gate031' RIG_PORT=2956 rig.sh provision`, then the
  four plugin folders copied from the canary tree (Husnisse `31ddbd35…`, TestHarness `57c4e48f…`,
  ConditionalConfigSync, ValheimModding-YamlDotNet; Jotunn comes from the live install as a junction).
  A dedicated tree was needed because `rig.ps1`'s `Get-TestProc` matches the tree path as a substring:
  while another card drives `V:\ValheimTest`, its `rig down/up` also kills `V:\ValheimTest2`. The host
  firewall needs an inbound UDP rule per tree (`Valheim canary rig UDP 2656` 2656-2657,
  `Valheim gate031 UDP 2956` 2956-2957) — without it the client fails with
  `Lost connection to server:ErrorConnectFailed`.
- Scenario driving without the host rig lock: `client/s031.sh <file.cmd>` copies the scenario to
  `V:\Gate031\BepInEx\config\harness.cmd` and polls `Harness.log`. Safe only because this tree is
  written by this card alone.
- Client: `client/gate.sh install` (kill the client, push the two DLLs to `C:\Users\Hermes\stage\`,
  hash-verify the installed copies), then `gate.sh launch` — it points the `HusnisseLegsLaunch`
  scheduled task at the gate port, starts it, waits for `gc ready` and saves the player id. The task is
  restored to 2556 at the end (`gate.sh collect`).
- Fixture: `client/scene-031.sh <x> <deckY> <z> <playerId> <out.cmd> <out.env>` — a bowl with the
  joining player as creator and four chests within 4 m. Built **after** the client is in world (the
  zone has to be loaded or the pieces are gone within a minute), and the scenario calls `nostruct`
  first: harness pieces are spawned into thin air on a server with no terrain colliders, so
  `WearNTear` damages them away in ~20 s otherwise (`client/harness-nostruct.patch`).
- Phases: `gate.sh scene`, `gate.sh teach`, `gate.sh ledger`, then one server-side `final-state.cmd`
  (a scenario load wipes the standing scene, so it is last), then `gate.sh collect` — client evidence
  zip + server logs, quit the client, restore the task.

## What each check needs

| Check | Probe commands |
|---|---|
| teaching | `alt husnisse_nisse`, `give <item> <n>`, `stand piece_chest_wood <idx>`, `open …`, `move …`, `close`, `containers` |
| ledger open | `close`, `ledger`, `ledgerpanel` |
| classify | `click HusnisseLedgerRow<idx>`, `click HusnisseLedgerCatMetals`, `ledgerpanel`, then `close`, `open …`, `ui` |
| network | `click HusnisseLedgerNet` — read it back on the **chest strip**, not in the panel: the panel redraws only after the server's ack (about a second) |
| blacklist | `click HusnisseLedgerBlacklist`, then `open …` → `ui` must show `strip=False` with `win=True` |
| pickup | `click HusnisseLedgerPickupMetals` → `[Ledger] pickup …` in the client diagnostics, and `pickup=` in the server's `husnisse status` |

## Traps that cost time

- A `close` while a container is open leaves the inventory in a state where the next `ledger` opens and
  is hidden again within a second. Close first, wait ~5 s, then `ledger`, and read the panel in the
  same batch as the click you want to verify.
- Anything spawned before the client is in world is thrown away by the zone system. Build the fixture
  after `gc ready`.
- The probe's `give` can answer `item=False` when the slot it is told to use is occupied; carry a
  sample out of a chest (`take`) or pick a free slot when a phase needs an item in the pockets.
