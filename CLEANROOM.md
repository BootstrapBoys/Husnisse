# Clean-room record — Husnisse vs Quartermaster

This file exists so the independence of Husnisse can be shown, not just asserted. It records where every
requirement came from and what we are not allowed to look at while building.

## The licence position

Quartermaster is published under **The Ask Astra License**:

> "Want to reuse, modify, repackage, sell or redistribute the original code or artwork? Ask Astra for
> permission first."

That forbids reusing its code or art. It does **not** forbid:

- writing our own mod that does the same job — functionality is not copyrightable;
- reading its public README, config files and manifest, which ship inside the package;
- observing its behaviour in game;
- reading its runtime data (ZDO custom keys) to understand a save format.

It does forbid copying code, translating its IL, shipping a modified DLL, or forking the repo.

## What "clean room" honestly means here

The textbook clean room is two teams: one reads the original and writes a specification, a second
implements from the specification alone, never seeing the source. We are one person, so we cannot claim
that.

What we can honestly claim, and what this file records:

1. The specification was written from **public documentation and observed behaviour**, and every
   requirement below is traceable to a public source.
2. No Quartermaster code, decompiled output, or translated method bodies exist in this repository.
3. The architecture is ours: the creature model is a different mechanism, not a renamed copy of a chest
   configuration system.

That is a defensible position. It is weaker than a two-team clean room, and this file says so rather
than pretending otherwise.

## The rules

**Allowed**

- Reading `README.md`, `manifest.json` and the shipped `.cfg` inside the Quartermaster package.
- Playing with Quartermaster installed and observing what it does, including timing and edge cases.
- Inspecting **runtime ZDO data** in a test world to understand what a save contains.
- Reading community documentation, changelogs and issue reports.
- Reading the game's own assemblies — that is how every Valheim mod is written.

**Not allowed**

- Decompiled Quartermaster source anywhere in this repository, including `refs/`, `docs/` or commit
  history. If it is inspected at all, it happens outside the tree, on a scratch copy, and nothing is
  carried over.
- Translated method bodies, renamed variables, or a file that is Quartermaster's structure with
  different names.
- Its ZDO custom-data key names. Choose our own — even where the meaning is similar, the format is ours.
- Its config key names, section names, or the owl's dialogue text.
- Any of its art.

**The structure trap.** The real clean-room risk is not a copied line; it is a copied shape. If Husnisse's
classes map one-to-one onto Quartermaster's (`DepositChest`, `StorageRules`, `SupplyPermissions`,
`MachineControl`), the code is a derivative in substance even if every line was retyped. Husnisse's shape
comes from the creature model — claim, memory, round, bowl, mood — which is why the architecture section
of `README.md` reads nothing like a chest configuration system.

## Provenance

Every requirement in the parity table, traced to where it came from.

| Requirement | Source |
|---|---|
| Deposit chests sort incoming items into matching storage | Quartermaster package README, "Storage & Inventory" |
| Chests remember assigned types when empty | Same README, "Remembered items" |
| Preferred destinations, overflow chests, base groups, copy settings | Same README, "Storage rules" |
| Supply permissions for crafting, production, fuel, animal feed | Same README, "Supply permissions" |
| Deposit All, sort inventory, sort chests, combine stacks | Same README, "Inventory tools" |
| Pickup controls by item type | Same README, "Pickup controls" |
| Stack sizes without weight change | Same README, "Stack sizes" |
| Craft, build and upgrade using nearby stored materials | Same README, "Craft from storage" |
| Station coverage extended through the deposit chest area | Same README, "Building coverage" |
| Processors, fermenters, cooking racks supplied; finished goods collected | Same README, "Production" |
| Production caps, pause, protect valuable wood | Same README, "Machine controls" |
| Fires, torches, animal feeding, taming feed | Same README, "Refuelling & feeding" |
| Ship cargo unloaded into the base | Same README, "Helmsman integration" |
| Searchable chest settings, range previews, controller navigation | Same README, "Accessible menus" |
| Automation only in loaded areas | Same README, "Quick Start" note |
| Config section layout | Our own design. Numbered sections are a pack-wide habit (`1. General`, `2. Debug`, …), not copied from Quartermaster |
| Wards block access to containers | Observed behaviour, plus `CONFLICTS.md` notes on ward supply |

The **creature model itself has no source in Quartermaster**. It comes from the folklore and from the
design constraint in `README.md` — nothing moves unless the nisse moves it. That is the part that makes
this a different mod rather than a reimplementation, and it is the part we should be able to defend
without looking at anyone's code.

## Behaviour we deliberately do not copy

| Quartermaster does | Husnisse does |
|---|---|
| Configure a chest by menu | Teach by demonstration |
| Automation is instant and free | Automation happens in rounds, by a creature that must be fed |
| Owl is a UI assistant | The creature is the actor; there is no separate mascot |
| Global base network | Per-claim radius, non-overlapping |
| Static configuration | Trust and mood change what the nisse will do |
| Data lives on the chest | Memory lives on the nisse |

If a reviewer can map our feature list onto Quartermaster's one-to-one, we have built the wrong mod.

## If the licence changes

If Vassteel later grants permission — or the project is transferred — this file stops being a
constraint and becomes history. Until then, no Quartermaster code enters this tree, and the migration
tool (1.0) reads runtime ZDO data only, never source.
