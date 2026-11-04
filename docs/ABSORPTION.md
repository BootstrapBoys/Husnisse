# Absorption — what belongs in Husnisse instead of Housecarl

Husnisse owns storage and base automation. Housecarl owns the pack's QoL layer. The boundary is
mostly clean, but three mods sit close enough to it that the wrong owner would create the
double-ownership this project exists to remove.

**The test for moving a mod into Husnisse:**

1. Is the feature about **containers, what goes in them, or machines that consume them**?
2. Can it be expressed without contradicting the design law — nothing happens unless the creature
   does it? (Settings that describe *where* and *what* are allowed; actions are not.)
3. Would Husnisse need the answer anyway, to run its rounds?

All three yes → move it. One yes → coordinate, don't move. None → leave it alone.

---

## Tier 1 — move from Housecarl to Husnisse

### `Azumatt-AzuContainerSizes` 1.1.8 — container capacity

Housecarl ring 4 currently lists it. It is the only mod in that list whose subject is a container.

| Test | Answer |
|---|---|
| Containers? | Yes — it changes how many slots a chest or ship hold, including modded containers. |
| Design law? | Yes — capacity is a *what*, not an action. It is a property of a container, like the nisse's claim radius. |
| Needed anyway? | Yes — Husnisse plans rounds against container free space and has to know a container's size. Today it reads the grid; if it owns the grid, the two can't disagree. |

**Why it is a better fit than Housecarl:** Housecarl has no container model at all. It would be
implementing a storage property inside a QoL mod that otherwise never touches a chest — and then
Husnisse would read a grid it does not own.

**Why not now:** it is a prefab-level change on vanilla and modded containers, and it is server-synced.
Husnisse's container filter (`Compat/ContainerFilter`) is the right place for it, but that file is
still being taught what a container *is* (wards, carts, display piles, backpacks). Capacity is the
next layer on top. Target: **0.5**, alongside wards and multiplayer hardening — not 0.1, whose only job
is proving the creature loop is fun.

**Cost:** Azumatt's build is closed source with no licence, so this is a reimplementation, not a port.
Scope is small — a config table of prefab → grid size applied at prefab registration — but it needs
its own compat pass for modded containers.

**If accepted:** Housecarl drops to 56 absorbed, Ring 4 to 17. The pack loses one server-synced plugin.

---

## Tier 2 — coordinate, do not move

### `j1gA-StationSpeed` 0.2.3 — machine and crop speed

Close, because Husnisse 0.4 owns machine supply and collection. But speed is a different knob, and
StationSpeed's real work is the part that keeps **players without the mod** seeing correct results
(smelters advance on the owning client, fermenters and plants store start times, every client computes
readiness). That logic is farming-and-stations, not storage.

Two mods touching smelters with different knobs is fine — supply vs rate. Keep StationSpeed in
Housecarl, and make sure Husnisse's machine module never claims a speed setting.

### `shudnal-MyLittleUI` 1.2.33 (pinned) — chest names and hover text

MLUI already owns chest naming and hover. Husnisse's ledger will want to show container roles. The
rule from the sidecar model applies: feed MLUI the role, don't build a second naming system.

Concretely: when the ledger assigns a chest a role (pantry, overflow, machine input), MLUI's hover can
display it — one line of data in, no competing UI. If the ledger instead invents its own labels, players
get two names for one chest.

### `Neobotics-HUDCompass` 1.2.1 (pinned) — deposit chest pins

Same pattern. The compass can show a nisse's bowl and its claimed chests through
`DynamicMapMarkers`, but Husnisse should not grow a second compass.

### `shudnal-ExtraSlots` 1.2.23 — inventory slots

Husnisse *consumes* slots as a source in the storage contract. It must not provide them. Moving slot
management into a storage mod would invert the dependency and make every consumer carry the slot API.

---

## Tier 3 — explicitly not

| Candidate | Why not |
|---|---|
| `MSchmoecker-DynamicStoragePiles`, `Muji-DynamicStorage*` | Display piles render a linked container's contents. Filing into the pile instead of the real container is exactly the misfile the compat filter exists to prevent. |
| `OdinPlus-OdinsFoodBarrels` | A content mod with its own containers. Husnisse should learn to file into them (below), not absorb them. |
| `Chazman-RunicDisplayStands` | Armour stands as equipment storage with loadouts — player equipment, not base routing. Also source-available, so no reuse. |
| `BootstrapBoys-ZenPlayer_Backpack_Bridge` | Needs ZenPlayer and `AdventureBackpacks.API`, which would break Husnisse's standalone posture (Jotunn + ConditionalConfigSync only). It also answers a pickup question, not a storage one. |
| `Azumatt-Recycle_N_Reclaim` | Reclaiming items into resources is a crafting-station feature. Husnisse feeds the station; it does not decide what the station makes. |
| `DropMoreLoot`, `cjayride-InstantMonsterLootDrop` | Loot generation. The nisse collects what drops; it does not decide what drops. |

---

## Merges to *create* inside Husnisse

Three features that absorb functions rather than mods — each removes a pack package without taking
someone else's code.

### 1. Container allowlist (0.2, with the ledger)

Today the compat filter skips anything it does not recognise as storage. That is safe but brittle: a
new storage mod is invisible until Husnisse ships an update.

Turn it around: the ledger lists every container in the claim with its classification, and the player
can promote an unknown one to storage, pantry or ignore. Modded barrels, Muji furniture, a friend's
weird prefab — all usable without a code change.

This is also what makes `OdinsFoodBarrels` and future storage content work without absorbing it. It
replaces a compatibility patch with a setting.

### 2. Cart loading (1.0, alongside ship unloading)

Carts are excluded from storage, deliberately. Ship unloading is already a 1.0 feature; carts are the
same problem at a smaller scale and the same solution: the nisse loads a cart from claim stock when
you park it in the claim.

It is the natural pair to ship unloading and it uses the same task — a `LoadVehicleTask` against
`Vagon` instead of `Ship`.

### 3. Capacity module (0.5, if Tier 1 is accepted)

Container grid size as a Husnisse module rather than a separate plugin, applied at prefab registration,
server-owned, with the same config shape the old mod had so a migration is a table, not a rebuild.

---

## Effect on Housecarl if Tier 1 is accepted

| | Now | After |
|---|---|---|
| Housecarl absorbed | 57 | **56** |
| Housecarl ring 4 | 18 | **17** |
| Pack packages | 76 | **76** — the package leaves either way, Husnisse just absorbs it instead |

The pack number does not improve by moving it; the *ownership* does. The count drops to 75 when
Husnisse retires QI at 0.3.

Nothing else moves. The rest of Housecarl's list is UI, movement, death, loot and netcode — none of it
touches a container.

## Sequencing

1. **Now** — no change to either plan. Husnisse 0.1 proves the creature loop; Housecarl 0.1 ships
   interface modules.
2. **Husnisse 0.2** — ledger + container allowlist. This is what makes modded containers work and
   removes the pressure to absorb content mods.
3. **Husnisse 0.5** — wards, multiplayer, capacity module. Housecarl drops `AzuContainerSizes` in the
   same release train, not before.
4. **Husnisse 1.0** — ship unloading and cart loading.

The rule that keeps this honest: **a mod only leaves Housecarl after its replacement has shipped and
been played with**, not when the design says it will.
