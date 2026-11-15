# Compatibility

What Husnisse does about the other mods in the Bootstrap Boys pack. The rule behind every row:
**soft dependencies only** — nothing here is referenced in the csproj or shipped as a DLL. Anything
that needs another mod's types is reached by reflection, and anything that needs another mod's
objects is detected by component type name.

## Blocking

| Mod | Behaviour |
|---|---|
| **Quartermaster** | Hard refusal at startup. Two storage systems managing the same chests lose items. Migration to Husnisse arrives at 0.7 and reads runtime ZDO data only. |

## Access control

| Mod | Behaviour |
|---|---|
| **WardIsLove** | A bowl standing inside a ward uses that ward's ground: its radius, owner and permitted players become the claim. A bowl outside every ward never consults WardIsLove at all. Containers follow the ground, not permissions: a container inside a ward the claim does not own is never targeted, whatever permits the claim owner holds, so two nisses never share a chest. A bowl inside a ward its owner cannot use **refuses to settle** (suppressed, does nothing, says why), and a second bowl in the same ward refuses too. `6. Ward / Respect WardIsLove` turns the whole handover off; `6. Ward / Require Ward Ownership` (default off) additionally restricts a bowl to wards its own player placed, instead of any ward they are permitted on. |

### WardIsLove 4.0.9 — the API the seam uses

Reached by reflection; nothing from WardIsLove ships in this tree. Verified on the rig 2026-11-14:

- `WardMonoscript.m_wardInstances` (public static `List<WardMonoscript>`) is the ward list.
  `PrivateArea.m_allAreas` does not contain these wards: WardIsLove replaces the guard stone with
  `Thorward`.
- `IsEnabled()` = `General / WardEnabled` && ZDO `enabled` && `WILWardLimitCheck()`. The limit check is
  **false** unless the ward is flagged limited, so an unlimited ward carries `WILLimitedWard = true`
  and `WILLimitedWardTime = -1`. A spawned ward with only `enabled` set is not enabled.
- Radius: `WardMonoscriptExt.GetWardRadius` reads ZDO float `wardRadius`, default `WardRange`.
- Owner: the ward piece's creator, read from the ZDO key `creator`. `Piece.m_creator` is a copy taken
  in `Awake`, so it is stale for a piece whose creator was written later.
- Permits: `WardMonoscript.permissionsManager.IsPermitted(playerId)`.
  `CustomCheck.CheckAccess` inspects only the first enabled ward containing the point, which is not
  necessarily the ward the bowl selected, so the claim layer asks the ward it actually chose.
- Hover: WardIsLove denies the container hover to a player its ward does not permit, so that player
  sees the ward's `No access` instead of the bowl's `Claim …` line. That is correct access control;
  the claim is still computed and logged on the client, and a permitted player sees the claim line.

## Storage semantics

| Mod | Behaviour |
|---|---|
| **AdventureBackpacks**, **ExtraSlots** | Personal containers are never claim storage. AdventureBackpacks turns a backpack into a proxy `Container`, and those are detected by the mod's own `IsBackpackProxy` method (found by shape, not by type name) and skipped — the nisse must never file loot into someone's pack. The storage contract's source order (pockets → extra slots → backpack → scoped storage) applies to craft counting at 0.3; the nisse only ever moves items in the base. |
| **EpicLoot** | Item identity is the shared name, per the contract. Two differently enchanted materials therefore share one home. Accepted for 0.1 and documented rather than hidden. **0.4 detects it and does not register a provider**: EpicLoot's provider list is also how it pays, and its `InventoryManagement.RemoveItem` takes the player's pockets first, so a count-only provider would lose the pocket half of a cost it cannot finish. The provider lands in 0.5 with `TryConsume`. The refusal beside `EpicLootContainerBridge` is live now. |
| **AAABuildMenu** | 0.4: its pinned rows count the claim through a postfix on `PinnedRecipeTracker.CountItem`. Display only — the pinned row's number chooses a colour and never spends. |
| **AAA Crafting** | 0.4: `GetItemsInCloseContainers` and `CalculateMaxCraftAmount` read the claim, and `CfcLoaded` is set so AAA asks for container counts. The craft itself still spends pockets until 0.5; AAA keeps performing it. |
| **PlanBuild** | 0.5: its `GetInventories` list is used for both the cost display and a `void` take, so a read-only entry would place blueprints for free. Detected and logged at 0.4, ported at 0.5. |
| **MyLittleUI** | 0.4: its own pocket-only available-amount is switched off for the session and Husnisse writes the requirement row, so there is one number instead of two. Every other MyLittleUI feature is untouched. |
| **AzuContainerSizes** (and any slot-count mod) | Slot counts are read from `Inventory.GetEmptySlots()`, never assumed. |
| **OdinsFoodBarrels** | Ordinary containers; valid targets. Food ends up there only if the nisse learned it or the barrel already holds that item. |
| **DynamicStoragePiles**, **Muji DynamicStorage\*** | Display piles, excluded. They look like containers but exist to show items; filling them fights the mod that owns them. `4. Storage / Skip Display Piles`. |
| **BetterCarts**, **CraftyCarts**, **OdinShip**, **AtlasPirateShips**, **Moorings** | Mobile containers (`Vagon`, `Ship`) are excluded: filing goods into a cart or hold lets them sail away. Ship unloading is a deliberate 1.0 feature, not an accident. `4. Storage / Skip Carts and Ships`. |

## Later, by design

| Mod | When |
|---|---|
| **Wizardry** — custom stations | 0.6, as machine targets with standing orders. |
| **PlanBuild** — blueprints | The bowl is an ordinary `Piece` on the Hammer table, so blueprint copy should work. Verify at 0.2 when there is a bowl worth copying. |
| **Quartermaster_Integrations** | 0.4–0.5, when the contract exists: our storage surface replaces its Quartermaster query, and QI ships a final release pointing here. |
| **MyLittleUI** | 0.3.1, with the ledger: claim knowledge should surface in its tooltips and panels rather than competing with them. Until then, no interference — MLUI is UI-only and touches no container. |
| **AAA Crafting** | 0.4, with the read-only contract: its crafting UI must read the claim the same way QI makes it read Quartermaster today. The patch target is the same; only the data source changes. |
| **OdinPlus Basements** | 0.1.3, movement: basements are exactly the indoor space where the doorway finding bites. Walking stays room-scale and outdoor; thresholds fade. |
| **Forge of Potential Redux** | 0.6, as a machine target if it exposes input/output storage; verify its station surface before promising it. |
| **Ross QoL** | Verify before 0.2: if it sorts or stacks inventories, two systems must not fight over the same containers. Check its patches for `Container`/`Inventory` before the ledger work. |
| **Odin Campsite** | Verify before 0.2: tents and camp pieces may add containers. If they do, they are ordinary storage; if they are display or portable, they join the excluded list. |

## No interaction needed

- **DropMoreLoot**, **CreatureLevelAndLootControl** — more ground items; the round budget bounds the work.
- **MoreSlots** (Fang86) — player-inventory row expansion only; inventories are read dynamically, so no integration. Upstream is deprecated, so it is not a test target. Chest-size mods are the relevant case and are covered above.
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
