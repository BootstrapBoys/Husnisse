# Husnisse

A household spirit that sorts, stores and keeps a Valheim base. Place a **Porridge Bowl**, stock it
with food, and a nisse settles beside it. It files what the base owns into the chests that hold it,
remembers where each item lives, fetches what you are short of, feeds the machines — and refuses to
work when its bowl is empty.

**This is 1.0.0.** Husnisse replaces Quartermaster and Quartermaster_Integrations: install it alone,
and remove any other storage automation that manages the same chests. Two storage systems on one
chest lose items.

## Install

1. Install **BepInEx 5.4.x**, **Jotunn 2.30.2**, **ConditionalConfigSync 1.0.11** and
   **YamlDotNet 16.3.1** (`ValheimModding-YamlDotNet`) on the server and every client.
2. Remove **Quartermaster** (`Vassteel-Quartermaster`) before installing. Husnisse refuses to load
   beside it; Quartermaster_Integrations will then report its missing dependency and can be removed.
3. Extract the package at the Valheim root so
   `BepInEx/plugins/BootstrapBoys-Husnisse/Husnisse.dll` appears, and start the world. The log should
   say `Husnisse 1.0.0 starting`, `content registered`, and the stack line
   `[Stacks] maximum stack size 1000 applied to … stackable item(s)`.

Gale and other Thunderstore managers install the declared dependencies automatically. The server is
authoritative; a client without the mod will not get working container rules, craft counts or the
bowl's behaviour.

The first start writes `BepInEx/config/com.bootstrapboys.husnisse.cfg` and an English
`BepInEx/config/Husnisse/localization/en.yaml`. `BepInEx/BSBLogs/Husnisse.log` is rewritten each
session and holds the diagnostics dump.

## First steps

1. Build the **Porridge Bowl** from the hammer's Misc tab. It needs no workbench.
2. Open it and put food in its four slots. The nisse eats one item every `Bowl Drain Minutes` while
   it is below full; an empty bowl stops all work.
3. Open a chest and use the **network toggle** at the top right to make it part of the claim's
   network. The first toggle suggests the category most of its contents belong to.
4. Either put items in a chest marked **Deposit** (the nisse carries them to where their rules say),
   or use the bowl's radius as the base: ground drops and machine output are collected inside it.
5. `H` while the inventory is open opens the **ledger**: every container in the claim, its rule, the
   unclassified ones, the pickup filter and the blacklist.

## What it does

### The claim

- A bowl claims a **20 m radius** (configurable); a second bowl inside a claim is refused, and two
  bowls in one ward are refused. Claims never overlap.
- Inside a **WardIsLove** ward the ward's centre, radius, owner and permitted players become the
  claim; outside every ward the bowl's own radius and owner stand. A nisse never touches a chest
  standing in a ward it does not own.
- The nisse is invulnerable and enemies ignore it. It **walks** to its work, stands at the chest while
  it files, and fades across a threshold it cannot path across.

### The container network

- Containers opt in from the chest's own window. A rule is a **category** (Metals, Wood, Food,
  Trophies, Valuables, Building, Consumables, Tools) with a priority, a saved **custom list**, or a
  special role: **Overflow** (what no rule claims, or a full category) and **Deposit** (a source the
  nisse empties).
- **Deposit All**, **Empty Pockets** and **Tidy** are orders given to the nisse; they queue at the
  configured rate and are seen as trips. A deposit-all at an off-network chest dumps the pockets there
  unsorted, immediately.
- The queue is fair between task types and runs at 2, 5 or 10 items per second, with a five-second
  reconciliation tick and a bounded catch-up on load.
- Carts, ships (as destinations), display piles, backpacks and machine bins are never storage. A
  per-container blacklist adds to those class rules.
- Teaching a chest by putting a sample back, or the ledger, writes the same rule. The ledger also
  holds the per-claim pickup filter and the rules-file export/import.

### Crafting and machines

- **Craft from storage**: a recipe the pockets cannot pay for becomes a visible errand — the nisse
  takes the missing materials from the claim's chests and puts them in your hands; the game's own
  craft then spends them. AAA Crafting batches, EpicLoot and PlanBuild costs go through the same
  fetch. Read-only counts appear in the usual crafting and build rows (AAA Crafting, AAABuildMenu,
  PlanBuild, MyLittleUI) and in the storage contract.
- **Machines**: smelters, blast furnaces, charcoal kilns, eitr refineries, windmills, fermenters,
  cooking stations, ovens, beehives and sap extractors are fed their own fuel and input and emptied
  through their own interact RPC, within per-machine caps. Fires and torches are refuelled, tameable
  animals get a portion of food set down beside them.
- Four supply permissions per chest — **crafting**, **production**, **fuel**, **feed** — decide what a
  chest may give to which consumer. The ledger pauses machines per claim; `husnisse_machines` does one
  machine by hand.

### 1.0 surfaces

- **Stack sizes**: one configured ceiling (default 1000) for every stackable item; item weight is not
  changed and single-item equipment stays at one per stack. A stack saved above the ceiling is left
  alone until it is split. When **DropMoreLoot** is installed it owns stack sizes and Husnisse writes
  none.
- **Ship unloading**: any container on a ship inside the claim is emptied into the chests whose rules
  claim its contents, at the queue's rate and one visible trip at a time. Unmatched cargo stays
  aboard; a boat that leaves the claim mid-trip is not touched.
- **Building coverage**: a workbench, forge, stonecutter or other station standing anywhere inside a
  claim supports building, structure repair and dismantling throughout that claim. Crafting, item
  upgrades and item repair still require walking up to the station.
- **Quartermaster migration**: `husnisse migrate scan|apply` reads Quartermaster's per-container
  record out of a **copied** world (with Quartermaster removed), reports what it found and writes the
  equivalent Husnisse rules without moving an item. Run it on a copy, never the live world.

## Commands

| Command | Does |
|---|---|
| `husnisse_status` | Config, compat, stack policy, coverage, ship state, contract answer, one line per claim |
| `husnisse_preview [x z] [radius] [player]` | The claim a bowl would get at a point, with the ring on a client |
| `husnisse_prefabs` | Dump every container prefab to `BepInEx/BSBLogs/Husnisse-prefabs.txt` |
| `husnisse_contract [item]` | The storage contract's answer for an item |
| `husnisse_rule net\|set\|supply\|blacklist …` | Read or write one container's rule by prefab and index |
| `husnisse_categories dump\|check` | The category table, and gaps/overlaps in the presets |
| `husnisse_ledger` | The ledger as text |
| `husnisse_rules export\|import` | The claim's rules as YAML |
| `husnisse_machines feed\|collect\|pause` | One machine errand, or pause automation |
| `husnisse_migrate scan\|apply\|status` | Quartermaster migration on a copied world |
| `husnisse_bench` | Time one reconcile and one routing answer |

## Config

`BepInEx/config/com.bootstrapboys.husnisse.cfg` is grouped into numbered sections:

| Section | What lives there |
|---|---|
| 1. General | master switch, debug logging, diagnostics file, ledger key, lock configuration |
| 2. Nisse | catch-up, feeding required (world policy), orders, bowl drain, claim radius, LOD |
| 3. Flow | items per second, reconcile interval |
| 4. Storage | sulking, carts/ships blacklist, display piles, **Unload Ships**, **Extend Station Coverage**, **Enable Stack Sizes**, **Max Stack Size** |
| 5. Contract | requirement readouts, split counts, craft from claim |
| 6. Ward | respect WardIsLove, require ward ownership |
| 7. Machines | tend machines, fuel/input caps, collect, fires, animals, valuable wood |

`Feeding Required` is the one policy the world owns: the host writes it into the world on load, so a
client's setting cannot disagree with what actually runs. `Lock Configuration` makes the host's other
values authoritative on clients through ConditionalConfigSync. Stack sizes and the two coverage
switches take effect on the next start.

## Compatibility

- **Soft dependencies** (absent-safe, declared): WardIsLove, ExtraSlots, AdventureBackpacks,
  EpicLoot, AAA Crafting, AAABuildMenu, PlanBuild, MyLittleUI, DropMoreLoot.
- **Ownership hands over, it does not stack**: a mod that owns a function answers it and Husnisse
  consumes or steps aside. WardIsLove takes the claim inside a ward; DropMoreLoot takes stack sizes;
  the crafting UIs read the claim.
- **Never** file into a backpack, a cart, a display pile or a machine's own bin.
- **One storage system per base.** Remove Quartermaster and any other mod that sorts the same chests.

## Known limits

- The server is required; a client without Husnisse will not get the full behaviour.
- Building coverage and ship unloading are proven headless; seeing them on a live client is part of
  the playtest checklist (`docs/PLAYTEST.md` in the repository).
- The client half of the 0.7 chest handover is implemented on both peers and gated on the server;
  its end-to-end client run is still open.
- The nisse works in loaded areas only, like every other base system; a world with nobody present
  does not get sorted.

## Feedback

Bugs and suggestions: <https://github.com/BootstrapBoys/Husnisse/issues>. Please include
`LogOutput.log` and `BepInEx/BSBLogs/Husnisse.log`.

## Credits

BootstrapBoys. Original code and assets, built clean-room on Jotunn.
