# 1.0 — parity: evidence

Build: **Husnisse 1.0.0**, `artifacts/debug/Husnisse.dll` sha256
`848138e3a309d21bc2c6261e54ca9e6b16c6e7d28410ee003db1cd37b53e1e22`.
Rig: `V:\ValheimTest` (dedicated server, port 2556), bare — Jotunn, ConditionalConfigSync,
ValheimModding-YamlDotNet, BootstrapBoys-ServerSoftStop, BootstrapBoys-TestHarness and Husnisse.
One run adds TastyChickenLegs-DropMoreLoot 1.2.9; one run switches the two new toggles off.

## Gates

| Scenario | Assertions | Result |
|---|---|---|
| `testrig/scenarios/husnisse-10-parity.cmd` | 23 | **PASS** |
| `testrig/scenarios/husnisse-10-switches-off.cmd` | 6 | **PASS** |
| `testrig/scenarios/husnisse-10-dml.cmd` | 5 | **PASS** |

All three ran on the 1.0.0 build above. The parity scenario's console output is `parity.log` beside
this file.

## 1. Stack sizes — a configured ceiling

`stacksize Wood` → `stacksize Wood max=1000 weight=2`; `stacksize Club` → `max=1` (single-item
equipment untouched). `husnisse status`:

```
[stacks] stacks=husnisse max=1000 items=547
```

547 stackable item prefabs took the ceiling. With `Enable Stack Sizes=false` and a reboot, the same
commands read `stacksize Wood max=50 weight=2` and `[stacks] stacks=disabled` — the game's own
value, so the number really comes from the mod.

**When the write lands.** `ObjectDB.m_items` is empty inside Jotunn's `OnVanillaPrefabsAvailable`:
the event fires from the database object's Awake, before the game copies the item list in. The first
attempt from content registration therefore changed 0 items (`[Stacks] maximum stack size 1000
applied to 0 stackable item(s)` in `LogOutput.log`). The write now runs from an
`ObjectDB.CopyOtherDB` postfix — the copy that fills the list — with a one-shot retry from the flow
pump:

```
[Stacks] maximum stack size 1000 applied to 547 stackable item(s); item weight unchanged
```

**DropMoreLoot handover.** With DropMoreLoot 1.2.9 deployed, `[compat] … stacks=dropmoreloot on x3`
and `[stacks] stacks=dropmoreloot`; `stacksize Wood` stayed `max=50`. Husnisse wrote nothing, which
is the assertion that matters. Note for the record: on this **dedicated server** DropMoreLoot's own
`ItemDrop.Awake` prefix does not raise the database prefab's shared maximum (it stayed native 50);
the gate therefore proves the policy and the absence of Husnisse's 1000, not a multiplied value. A
client-owned item instance is where DropMoreLoot's prefix is normally observed.

## 2. Building coverage — the claim is the range

A workbench at x = −12 inside the 20 m claim, queried at x = 18 (30 m away, still inside the claim):

```
coverage name="$piece_workbench" at=18,0 found=$piece_workbench@-12,0
[build] coverage=on stations=1 extensions=1
```

Controls, same run:

```
coverage name="$piece_workbench" at=30,0 found=none     (10 m outside the claim)
coverage name="$piece_forge"     at=18,0 found=none     (wrong station kind)
```

With `Extend Station Coverage=false`, the first query read `found=none` and `[build] coverage=off`.

**Measured detail.** A query 17 m from the same workbench answered **natively** on this build, so the
workbench's own build range is greater than 17 m and less than 30 m; the 30 m point is what the
claim extension answers. The extension is a postfix on `CraftingStation.HaveBuildStationInRange` —
the one static gate `Player.HaveRequirements`, the placement-cost check and
`Player.CheckCanRemovePiece` (repair and dismantle) all ask — so one patch covers placing, repairing
and dismantling. Crafting and item repair do not call it and still need the station.

## 3. Ship unloading — the hold is a source

A `VikingShip` at x = 12 inside the claim, its hold filled with 6 wood and 5 feathers; a chest at
x = 2 with `cat:wood` on the network. After one trip:

```
contents piece_chest_wood stacks=1 items=6 types=$item_wood
contents VikingShip      stacks=1 items=5 types=$item_feathers
[Flow] landed moved=1 target=piece_chest_wood
[ship] unload=on holds=1 queued=2 last=piece_chest
```

The matched cargo moved; the unmatched feathers stayed aboard; the ship never became a destination
(`[rules] containers=1` counts only the chest). The hold is exempt from the "Skip Carts and Ships"
class blacklist only for this source role, and a per-container blacklist still refuses a boat.

**The engine fact that made it work.** A ship's hold is a child `Container` with **no `ZNetView` of
its own**; vanilla's `Container.Awake` resolves the inventory's owner through
`m_rootObjectOverride`, which points at the ship root's ZNetView. `container.GetComponent<ZNetView>()`
is null on the hold, so the first run was refused at the source gate on every trip:

```
[Flow] trip: source piece_chest is refused; the stack stays where it is
[Flow] landed moved=0 …
```

`Storage/ContainerNet.View` now returns `m_rootObjectOverride` when set, and every container-to-ZDO
lookup (handover, sync/persist, rules, ledger, `[chest]`) goes through it. A boat that leaves the
claim mid-trip fails `FlowTicket.IsValid` (the ship's position is re-checked) and the stack stays
aboard; the next reconcile queues it again only if the boat returns.

## What this does not prove

- **The client's visual pass.** The rig has no game client. Building coverage is the same static
  method a client calls, and ship unloading is server-side work, but seeing the nisse walk to a boat
  and the build ghost stay valid at 30 m is a client check (card `t_3298d461`'s world; the playtest
  checklist in `docs/PLAYTEST.md`).
- **A client-owned ship's hold.** On a live server the ship's ZDO belongs to the pilot, so unloading
  goes through the 0.7 ask/release handover; the gate's ship is server-owned. The handover's client
  leg is the open item carried by `t_3298d461`; a hold whose ZDO cannot be taken stays aboard by
  design.
- **Stack sizes on a joining client.** The code path is the same, but the client copy applying 1000
  at the menu was not measured in this run; it is a one-line `Ensure()` on both peers.

## New rig commands

`stacksize <item prefab>`, `coverage <station name> <x> <z>`, `ship <prefab> <x> <z> [item amount]`
and `tree <prefab>` (component tree with each `ZNetView`'s validity and ZDO id) were added to
`testrig/harness` for this gate. The `tree` command is what identified the ship hold's missing
`ZNetView`.
