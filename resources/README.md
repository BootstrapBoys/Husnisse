# Husnisse

A household spirit that sorts, stores and keeps a Valheim base, built around one idea: the nisse is
always the visible cause.

Place a **Porridge Bowl**, stock it with food, and a nisse settles beside it. It files what the base
owns into the chests that hold it, remembers where each item lives, and refuses to work when its bowl
is empty.

**This is 0.1.2, an early test build.** It is playable and server-authoritative, but movement, the
container network and the crafting integrations are still ahead. See *Planned features* below.

## Current features (0.1.2)

### The bowl is a pantry

- Interacting with the bowl opens **four food slots**, like a chest. Food only: weapons, wood, trophies
  and everything else are refused and stay in your inventory.
- The nisse eats **one item every `Bowl Drain Minutes`** (default 20, range 1–240) while satiety is
  below full. Food value raises satiety; an empty bowl lets satiety decay into hunger.
- Contents persist in the bowl's save data and survive a relog.
- `Alt` on the bowl toggles feeding for that bowl when the server allows it.

### The nisse

- Claims a **20 m radius** around its bowl. A second bowl inside a claim is refused, so no wasted
  materials and no overlapping nisses.
- **Invulnerable** and on a non-combat faction: enemies ignore it, and it cannot be killed.
- Mood and satiety decay with neglect. A sulking nisse complains and deliberately misfiles a capped
  number of stacks; feeding it restores it.
- Fades to its work target in this build. Walking arrives in 0.1.3.

### Storage work

- A **continuous queue** drains at 2, 5 or 10 items per second (default 5), refilled every five
  seconds, with a bounded catch-up after loading.
- Files **ground drops** inside the claim, and remembers which chest received which item so the same
  thing goes back to the same place next time.
- **Empty Pockets** — interact with the nisse to hand over what it can file. Hotbar and equipped items
  stay with you.
- **Tidy** — `Alt`-interact a chest inside a claim and the nisse queues its contents for the right
  chests.
- Orders are **server-validated**: the client asks, the server checks distance, feeding, ownership and
  the nisse, then queues the work.

### Around the edges

- **Localisation** through YAML under `BepInEx/config/Husnisse/localization/`. English ships embedded;
  add a file per language, with English as the fallback.
- **Diagnostics** in `BepInEx/BSBLogs/Husnisse.log`, rewritten each session, plus the
  `husnisse_status` console command.
- **WardIsLove** access checks are respected where it is installed. Full per-bowl ward claim handover
  arrives in 0.2.
- **One storage system per base**: remove any other storage automation before installing. Two systems
  managing the same chests lose items.

## Planned features

Not in this build. Order and detail are in the repository's `docs/ROADMAP.md`.

| Version | Planned |
|---|---|
| 0.1.3 | Legs: walking indoors and outdoors, fading across thresholds, visible trips to containers |
| 0.2 | Claim ownership: WardIsLove per-bowl handover with last-known fallback, claim range preview |
| 0.3 | Container network: opt-in toggle and settings on the container UI, item categories with priority, **Overflow** and **Deposit** chests, saved-content lists, per-container blacklist, deposit-all, animation LOD, fair queue |
| 0.3.1 | Teaching by demonstration, editable ledger, pickup filter, pocket stack merge, YAML rule export/import |
| 0.4 | Read-only storage contract: craft and build UIs count claim storage (AAA Crafting, AAABuildMenu, PlanBuild, MyLittleUI), EpicLoot provider, ExtraSlots and AdventureBackpacks as sources |
| 0.5 | Craft from claim: spending materials from storage with a visible delivery, recipe repaint, stack rules |
| 0.6 | Machines: processors, fermenters, refuelling, animal feeding, four-way supply permissions, mailbox decision |
| 0.7 | Multiplayer hardening and legacy storage migration |
| 1.0 | Full parity: configurable stack sizes, ship unloading, comfort coverage, sign-off against the parity matrix |

## Install

1. Install **BepInEx 5.4.x**, **Jotunn 2.30.2**, **ConditionalConfigSync 1.0.11** and
   **YamlDotNet 16.3.1** (`ValheimModding-YamlDotNet`).
2. Remove any other storage automation that manages the same chests before installing.
3. Extract the package at the Valheim root so
   `BepInEx/plugins/BootstrapBoys-Husnisse/Husnisse.dll` appears.
4. Start the game. The log should say `Husnisse 0.1.2 starting` and `content registered`.

Gale and other Thunderstore managers install the declared dependencies automatically.

## Config

| Section | Key | Default | Meaning |
|---|---|---|---|
| 1. General | Debug Logging | false | verbose flow, order and container lines |
| 1. General | Write Diagnostics File | true | write `BepInEx/BSBLogs/Husnisse.log` |
| 2. Nisse | Feeding Required | true | every bowl must be stocked; off makes feeding opt-in per bowl |
| 2. Nisse | Bowl Drain Minutes | 20 | minutes per eaten item |
| 2. Nisse | Claim Radius | 20 | metres claimed around a bowl |
| 2. Nisse | Allow Orders | true | enable Empty Pockets and Tidy |
| 3. Flow | Items Per Second | 5 | 2, 5 or 10 |
| 3. Flow | Reconcile Interval | 5 | seconds between queue refills |
| 3. Flow | Catch-up on Load | true | brief 2× drain after loading |

## Commands

- `husnisse_status` — claim state, satiety, mood, bowl contents, queue depth, rate, containers.
- `husnisse_prefabs` — writes every loaded container prefab to
  `BepInEx/BSBLogs/Husnisse-prefabs.txt`, useful for pack authors and blacklist planning.

## Known limitations

- Walking is not in this build; the nisse fades to its target.
- The container network UI (toggles, categories, deposit chests) is 0.3.
- Crafting UIs do not count claim storage yet; that is 0.4–0.5.
- Orders and the pantry UI have been headless-tested, but not yet with two clients in a live session.
- WardIsLove currently gates access only; it does not yet own the claim.

## Feedback

Bugs and suggestions: <https://github.com/BootstrapBoys/Husnisse/issues>. Please include `LogOutput.log` and
`BepInEx/BSBLogs/Husnisse.log`.

## Credits

BootstrapBoys. Original code and assets, built clean-room on Jotunn.
