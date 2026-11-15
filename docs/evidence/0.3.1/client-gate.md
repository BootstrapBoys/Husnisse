# 0.3.1 client gate — the runbook

The one thing the headless runs cannot prove: that the ledger panel and the teaching gesture work on a
real client's screen. This is the exact sequence; it is the same rig pattern the 0.3b panel gate uses
(`../0.3b-client/driver.sh`, `run.sh`, `pc.sh`, `tools.ps1`), pointed at the spare tree.

## Preconditions

- The client rig (`Hermes@192.168.1.172`, key `~/.ssh/husnisse_client_ed25519`) is **free**: no
  `valheim.exe` running, no other card's probe writing `husnisse-probe.cmd`.
- A server is up on `V:\ValheimTest2` (port **2656**) with the gate build deployed:
  `Husnisse.dll` sha256 `31ddbd35…` from `~/projects/valheim/nisse-031-gate/artifacts/debug/`.
- The probe DLL from the same worktree (`probe/gate-client/artifacts/`, sha256 `367cbd99…`).

## Steps

1. Install: copy the two DLLs to `C:/Users/Hermes/stage/`, then `run.sh install` (verify the hashes it
   prints) and `run.sh reset` (the probe replays its command file on launch).
2. Launch the client against **2656**, not the default 2556 task: start
   `valheim.exe -console -joinserverwithcharacter 192.168.1.136:2656 nissetester -password mtest`
   (kill any running client first). Wait for `gc ready`.
3. Scene: build the fixture at the client's own position — one bowl with the joining player's id as
   creator and four chests on a deck around it (the harness `spawn` at `ground` needs a surface; the
   0.3b `scene.sh` does exactly this and writes the role → index map). Then `container piece_chest_wood 0..3`
   to confirm `claim=True ui=True`.
4. **Teaching, on the client:**
   - `give CopperScrap 5` (the probe's `give` puts it in the player's pockets),
   - `alt husnisse_nisse` → the log must show `[Teach] watching` on the server and the nisse's hover
     must read "The nisse is watching…",
   - `open piece_chest_wood 3` then `move piece_chest_wood CopperScrap 5` (the probe's `move` is a real
     `MoveItemToThis`, the same call a drag makes),
   - `close`, then `husnisse container piece_chest_wood 3` on the server must read
     `claim=True … ui=True` and the client's `containers` dump must show `net=on rule=custom:1`.
   - `alt husnisse_nisse` again, `move` a stack into chest 2: its rule must stay `none` (the control).
5. **The ledger, on the client:**
   - `close`, `ledger` (the probe opens the inventory and calls `LedgerUi.Toggle`), `ledgerpanel` —
     assert `active=True`, the claim line carries `container(s)` and `unclassified`, and one row is
     marked `?` (the unclassified chest),
   - `click HusnisseLedgerRow0` (select the first chest), `click HusnisseLedgerCatMetals` — the row
     label becomes `cat:metals`, and `open piece_chest_wood <same>` + `ui` must show the container
     window's rule label reading `Metals` (one writer, two surfaces),
   - `click HusnisseLedgerNet` → the row's dot fills and `husnisse_rule list` agrees,
   - `click HusnisseLedgerBlacklist` → `husnisse container … blacklisted=True ui=False`,
   - `click HusnisseLedgerPickupMetals` (twice: off, on) → `husnisse status` `pickup=` changes,
   - `shot ledger` for the panel, `shot ledger-rule` for the chest window after the ledger edit.
6. Copy the evidence: `run.sh stage` then `scp` the zip; keep the client's `LogOutput.log` gate lines,
   the server's `Husnisse.log`, and the screenshots next to this file.
7. Record the results here (a table like `RESULT.md`'s headless one) and update `docs/ROADMAP.md`
   0.3.1's "Met …" sentence with the client proof.

## What a pass looks like

- The ledger opens and closes with the inventory, lists the claim's containers, and the buttons change
  the same rule the chest's own window shows.
- Alt on the nisse + a sample in a chest writes `net=on rule=custom:1`; the same move with nobody
  watching writes nothing.
- Screenshots of the panel and of the chest window carrying the ledger's edit.
