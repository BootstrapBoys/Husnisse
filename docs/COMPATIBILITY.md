# Compatibility

What Husnisse does about the other mods in the Bootstrap Boys pack. The rule behind every row:
**soft dependencies only** — nothing here is referenced in the csproj or shipped as a DLL. Anything
that needs another mod's types is reached by reflection, and anything that needs another mod's
objects is detected by component type name.

## Blocking

| Mod | Behaviour |
|---|---|
| **Quartermaster** | Hard refusal at startup. Two storage systems managing the same chests lose items. Migration to Husnisse arrives at 0.5 and reads runtime ZDO data only. |

## Access control

| Mod | Behaviour |
|---|---|
| **WardIsLove** | Every container is filtered through `WardIsLove.Util.CustomCheck.CheckAccess(ownerId, point, radius, flash)`. A container in a ward the claim owner cannot use is never targeted. A bowl placed inside such a ward **refuses to settle**: the claim registers as suppressed, does nothing, and says why. `6. Ward / Respect WardIsLove` turns the filter off; `6. Ward / Require Ward Ownership` (default off) additionally restricts use to wards the claim owner placed, instead of any ward they are permitted on. |

## Storage semantics

| Mod | Behaviour |
|---|---|
| **AdventureBackpacks**, **ExtraSlots** | Personal containers are never claim storage. AdventureBackpacks turns a backpack into a proxy `Container`, and those are detected by the mod's own `IsBackpackProxy` method (found by shape, not by type name) and skipped — the nisse must never file loot into someone's pack. The storage contract's source order (pockets → extra slots → backpack → scoped storage) applies to craft counting at 0.3; the nisse only ever moves items in the base. |
| **EpicLoot** | Item identity is the shared name, per the contract. Two differently enchanted materials therefore share one home. Accepted for 0.1 and documented rather than hidden. |
| **AzuContainerSizes** (and any slot-count mod) | Slot counts are read from `Inventory.GetEmptySlots()`, never assumed. |
| **OdinsFoodBarrels** | Ordinary containers; valid targets. Food ends up there only if the nisse learned it or the barrel already holds that item. |
| **DynamicStoragePiles**, **Muji DynamicStorage\*** | Display piles, excluded. They look like containers but exist to show items; filling them fights the mod that owns them. `4. Storage / Skip Display Piles`. |
| **BetterCarts**, **CraftyCarts**, **OdinShip**, **AtlasPirateShips**, **Moorings** | Mobile containers (`Vagon`, `Ship`) are excluded: filing goods into a cart or hold lets them sail away. Ship unloading is a deliberate 1.0 feature, not an accident. `4. Storage / Skip Carts and Ships`. |

## Later, by design

| Mod | When |
|---|---|
| **Wizardry** — custom stations | 0.4, as machine targets with standing orders. |
| **PlanBuild** — blueprints | The bowl is an ordinary `Piece` on the Hammer table, so blueprint copy should work. Verify at 0.2 when there is a bowl worth copying. |
| **Quartermaster_Integrations** | 0.3, when craft-from-claim exists: our storage surface replaces its Quartermaster query, and QI ships a final release pointing here. |
| **MyLittleUI** | 0.2, with the ledger: claim knowledge should surface in its tooltips and panels rather than competing with them. Until then, no interference — MLUI is UI-only and touches no container. |
| **AAA Crafting** | 0.3, with craft-from-claim: its crafting UI must read the claim the same way QI makes it read Quartermaster today. The patch target is the same; only the data source changes. |
| **OdinPlus Basements** | 0.1.1, movement: basements are exactly the indoor space where the doorway finding bites. Walking stays room-scale and outdoor; thresholds fade. |
| **Forge of Potential Redux** | 0.4, as a machine target if it exposes input/output storage; verify its station surface before promising it. |
| **Ross QoL** | Verify before 0.2: if it sorts or stacks inventories, two systems must not fight over the same containers. Check its patches for `Container`/`Inventory` before the ledger work. |
| **Odin Campsite** | Verify before 0.2: tents and camp pieces may add containers. If they do, they are ordinary storage; if they are display or portable, they join the excluded list. |

## No interaction needed

- **DropMoreLoot**, **CreatureLevelAndLootControl** — more ground items; the round budget bounds the work.
- **Seasonality**, **PlantEverything**, **MyDirtyHoe**, **HoneyPlus**, **PotionPlus** — farming and consumables; no storage surface.
- **ImmersiveTrader**, **SellTrophies**, **OttoPay** — economy and trading; they price items, they do not store them.
- **AdvancedPortals**, **RossPortals**, **TheGreatestMap**, **Get Up u Coward** — travel, map and animation.
- **AAA Crafting** is a 0.3 target, not a no-op; **AAA Build Menu**, **MyLittleUI**, **HipLantern**, **FirstPersonMode**, **Build Camera** — UI and client-side only.
- **Server_devcommands**, **AdvancedAdminPanel**, **ServerSideSave**, **SmoothServer**, **Upgrade_World** — admin and server maintenance.
- **Jotunn**, **ConditionalConfigSync**, **JsonDotNET**, **YamlDotNet**, **Zen ModLib**, **ConfigurationManager** — libraries. Jotunn and ConditionalConfigSync are the only declared dependencies.

## Rules this file enforces

1. **No assembly references to other mods.** Type detection by name, calls by reflection. A missing mod
   is skipped, never an error.
2. **Never bypass a ward.** The ward's permission list is the owner's decision; a storage mod that
   reads through wards is a theft mod with extra steps.
3. **Never take from a player's person.** Pockets, equipment slots and backpacks are outside every
   claim.
4. **Never file into something that moves.** Carts, ships and display piles are not storage.
