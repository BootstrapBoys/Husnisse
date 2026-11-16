# 0.7 — multiplayer and migration: evidence

Build: **Husnisse 0.7.0**, `artifacts/debug/Husnisse.dll` sha256 `e93a777642ccc1c4` (the versioned build;
both gates below were re-run on it).
Rigs: `V:\ValheimTest` (dedicated server, port 2556) for the ownership and ten-nisse gates;
`V:\Migrate` (port 3056) for the migration, on a **copy** of the live world
(`C:\Users\WinGSM\AppData\LocalLow\IronGate\Valheim\worlds_local\Valheim`, 41 files, 9.4 MB — copied
once, read-only on the source); the Windows client guest `DESKTOP-PAOVLP7` for the client half.

## Gates

| Scenario | Assertions | Result |
|---|---|---|
| `testrig/scenarios/husnisse-07-ownership.cmd` | 12 | **PASS** |
| `testrig/scenarios/husnisse-07-ten-nisses.cmd` | 7 | **PASS** |

Both gates run on a bare dedicated server. With a client connected they do not: a live player's own
chests and inventory join the world, and the ten-nisse fixture's claims are disturbed by the client's
streaming — recorded here so a future run does not read that as a regression.

### ownership — the stale copy and the unowned chest

Two rules, each with a control that would fail if the rule were removed:

* **A stale copy must not overwrite the ZDO.** The chest's ZDO is given 5 wood directly
  (`zdofill`, which writes the ZDO exactly as another peer's save would) while this machine's live
  `Inventory` stays empty. A deposit-all of 8 stone then produces `stacks=2 items=13` — both stacks —
  and the pockets end empty. Without the re-read before the write, the same deposit persists the
  server's stale inventory (stone only) and the 5 wood are gone.
* **A chest nobody owns is claimed, verified and filed into once.** With the chest's ZDO owner set to
  `0`, an Empty Pockets order files the 6 wood stack into it, the ZDO ends `stacks=1 items=6
  serverOwns=True`, and the pockets end at zero — the stack exists in exactly one place.
* `husnisse status` carries a `[chest]` line (`chest_waits=… rejected=…`) in the same dump as the
  claim lines, so a handover that is waiting is visible without debug logging.

### ten nisses — the 0.7 benchmark

Ten claims 40 m apart, each with a deposit chest and a receiving chest with a wood rule, ten nisses
working at once. A dedicated server with no client ticks at ~30 Hz, so the average frame is pinned to
the tick and the tail is the honest number.

| Window | avg | p95 | max | over50 | over100 |
|---|---|---|---|---|---|
| idle (10 claims, 20 chests) | 33.60 ms | 33.69 | 46.5 ms | 0 | 0 |
| working (10 trips) | 34.52 ms | 33.75 | 178.6 ms | 10 | 1 |

`perf` sampling: 20 s per window, final run. The working window's tail varies between runs — an
earlier run on the same build peaked at 93.7 ms with no frame over 100 ms — so the gate asserts a
ceiling (over100 ≤ 10, max < 250 ms) rather than the observed maximum. A separate 25 s `prof` window
attributed 1240 ms of 2169 ms of mod time to `FlowScheduler.Update` (1.71 ms per call over 725 calls);
the stalls that do appear while ten nisses are tripping arrive in bursts and are attributed to
navmesh tile rebuilds over the rig's terrain-less pieces — an artefact of this rig, recorded rather
than asserted.

## Migration — a copied live world

`husnisse migrate scan` then `apply`, on `V:\Migrate` loaded from the copy of the live world
(246 157 ZDOs, **524 containers with contents**). Husnisse has Quartermaster *removed* for this: it
reads Quartermaster's own per-container record out of the save.

| Reading | Result |
|---|---|
| records found | **154** (deposit chests, overflow chests, remembered item lists, supply restrictions) |
| already migrated | 0 → then **154** after apply |
| orphans (record's prefab not in this world) | **39**, of which **37 still hold contents** |
| items that could not be translated | **92** prefab names from mods not installed in the migration tree |
| `apply` | `applied=154 skipped=0 failed=0` |
| contents before vs after apply | `contents_before=484434786849441161` / `contents_after=484434786849441161` — **identical** |
| second `apply` | `applied=0 skipped=154 failed=0` (idempotent) |
| after a graceful save and reboot | `migrated=154 pending=0`, container fingerprint still `3ff79f8b482581ec` |

The harness's independent `zdoworld` fingerprint (a hash of every container's packed inventory,
straight out of the ZDO map) is byte-identical before the apply, after it and after the reboot.

What maps, exactly: the two roles (`deposit`, `overflow`), the four supply permissions, the priority
(QM "Preferred" → priority 5) and the remembered item list translated from prefab names to the game's
shared item names. What does not: QM's base groups (`BaseName`/`Group` — Husnisse's unit is the
claim), its watch-and-learn flags, per-chest pickup/feeding ranges and auto-sort. Every one of those is
reported per container as a note, and an item from a mod the tree does not have is reported by prefab
name rather than guessed at.

The 39 orphans are Quartermaster's own chest prefabs: with Quartermaster removed they have no live
piece, and their contents sit untouched in the save. The report says so, with the count and how many
still hold stacks — a migration that dropped them would be worse than one that names them.

## Client half — what a real client proved, and what it did not

The client guest joined the dedicated server with the 0.7 build (`gc ready player=Stranger
id=-202584743 … peers=1`), and one thing was proven end to end: **with a client-owned, open chest in
the claim, the nisse never routed to it.** `zdopos` read `inuse=1`, the ZDO's contents stayed
`bytes=0 stacks=0 items=0` while an Empty Pockets order was queued, and no deposit was attempted — the
chest could not lose or duplicate a stack because nothing was written to it.

The ask/release path itself — the client answering the server's `Husnisse_Chest` ask, saving, and
handing the ZDO over — **was not exercised on the client in this run.** The rig's test world
accumulates harness-spawned chests, so the probe's nearest-`piece_chest_wood` selection kept landing
on a pre-existing world chest instead of the fixture; and one fixture chest that the client opened
disappeared from the server's live object set inside the same window (a ZDO with no live instance),
ending the run before the handshake could be timed against it. The protocol is implemented and
logged (`ChestRpc`, `ChestHandover`, `[Chest]` lines), and its server half is covered by the headless
gate; the client half needs a clean fixture world and one more pass. This is the open item on 0.7.

Two-client duplication was **not** tested: the rig has one client target (one Steam library, one GPU
guest), and a second client needs a second target. The design removes the case that needs two — the
server never writes a chest a player has open, and a client that loses ownership closes its window
and reloads rather than writing a stale grid — but that is an argument, not a measurement, and it is
recorded as such.

## Engine facts measured on the rig

1. **A container's live `Inventory` and its ZDO's `items` bytes diverge.** `Container.Load` re-reads
   the ZDO at most once per second (`Container.CheckForChanges`) and returns immediately while
   `m_inUse` is set, so a write that trusts the live inventory can silently drop what the ZDO already
   had. Every server-side move now re-reads the ZDO first (`ContainerWrite.Sync`).
2. **A client that loses ownership of an open chest keeps a stale grid forever.** `Container.SetInUse`
   is a no-op unless the caller owns the ZDO, so `m_inUse` stays true after the handover and
   `Container.Load` never runs again — reopening the chest shows the stale contents and the next move
   writes them back. `Patches/StaleChest.cs` closes the window on loss of ownership and clears the
   stuck flag.
3. **Ownership is a revision race.** `ZDO.SetOwner` bumps `OwnerRevision`, and a receiving peer keeps
   the copy with the higher revision (`ZDOMan.RPC_ZDOData`); a server that claims a chest a client
   opened last can lose that race, after which its writes go to a copy the client overwrites. Only the
   owner's own handover is guaranteed to win, which is why the server asks.
4. **A chest whose recorded owner is a session that no longer exists cannot be opened by anyone.**
   `Container.Interact` asks the *owner* for the chest (`RPC_RequestOpen`); with a dead owner id in
   the ZDO nothing answers and the window never opens. Seen on the copied world (`owner=-907393703`).
   The nisse is immune because `ChestHandover` claims a chest whose owner is not a connected player.
5. **A dedicated server holds every container ZDO even though it loads no zones.** `zdoworld` counted
   524 containers with contents and 246 157 ZDOs while `zdosum` (live objects) saw none. Migration
   therefore reads and writes the save, not the scene.
6. **Quartermaster's per-container record is one JSON string in a ZDO string slot**, recognised by
   its own shape (`"Version"`, `"Remembered"`), so the migration needs none of its key names.
7. **`nostruct` strips `WearNTear` from every object in the world**, not just the fixture's — useful
   on a disposable tree, and worth knowing before running a scenario against a world you want to keep.

## Not done

* The client half of the handshake (ask → save → release → ack) and any two-client test: see above.
* `husnisse migrate` was not run with the server pack installed, so 92 item prefabs stayed
  untranslated; with the pack present they resolve, which is the operator's route.
* Nothing was released: no zip, no tag before this card, no Hexium publish.
