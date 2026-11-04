# Husnisse — a storage and automation mod where a creature does the work

**Husnisse** — *hus* (house) + *nisse*, the Danish and Norwegian name for a house spirit.
The mod is named after the office; the creature is still a nisse. Package `BootstrapBoys-Husnisse`,
GUID `com.bootstrapboys.husnisse`. Alternates considered: Hustomte, Buvættir, Tonttu, Hearthkeeper
(taken on Thunderstore).

Clean-room replacement for Quartermaster. New project, nothing shared with Housecarl or QI.

Design record: [`CLEANROOM.md`](CLEANROOM.md) — where every requirement came from, and the rules that
keep this independent of Quartermaster's code.

## The one law

**Nothing happens unless the creature does it.**

There are no magic chests, no invisible item pipes, no automation that runs when nobody is home. If the
nisse is unfed, asleep, sulking or dead, the base stops sorting, stops feeding the smelter, stops
unloading the boat. Every automated action in the mod has an actor you can point at.

That single rule generates the whole design:

- **Performance is bounded by fiction.** The creature works in passes, not instantly, so a 200-chest
  base costs what a creature can carry, not what a loop can scan.
- **Balance is a relationship, not a config slider.** Automation is gated by care, not by a checkbox.
- **Failure is legible.** When something is not being sorted, you can see why: the bowl is empty, the
  nisse is in the wrong room, or it is hiding your boots because you forgot it.

### The law's boundary

The law governs **storage**, not hands. It says no item moves around the base unless the nisse moves
it — it does not claim the player cannot spend what they are already holding.

This matters at 0.3, when craft-from-claim arrives. The storage contract fixes the spend order as
pockets → extra slots → equipped backpack → scoped storage, so a craft debits the player's own
inventory before it touches a chest. That is not the nisse working: the nisse supplies only the leg
that comes out of its claim, and the player's pockets are spent by the player's own craft action.
Put plainly: **the nisse moves what is in the base; the player moves what is on the player.** Stating
the exception here keeps the law from being quietly broken the first time a craft succeeds.

## The creature

A **nisse** is a small, soot-coloured household spirit from Norse folklore: it does the chores at night
and expects to be fed, and it turns mischievous when it is not. That is a better automation metaphor
than any UI we could design, because it explains *why* work happens while you sleep.

| Trait | What it does |
|---|---|
| **Small and mundane** | Lives in the base, not on the battlefield. Not a combat pet. |
| **Works in passes** | A nightly round, or a round every N seconds while you are home. Visible if you watch. |
| **Eats** | A porridge bowl is its home. Empty bowl, no work. Food quality sets its speed and reach. |
| **Remembers** | Learns where things go, by being shown, not by being configured. |
| **Sulks** | Unfed for long enough and it hides items in the wrong chests until you apologise with food. |

It should be charming enough that players feed it for its own sake, and useful enough that they notice
when they forget.

## The signature mechanic: teaching by demonstration

Quartermaster configures chests with a menu: open a chest, assign allowed item types, set preferred
destination and overflow. It is precise and it is tedious.

Husnisse replaces the common case with a gesture:

1. Pick up an item.
2. Look at the nisse, press the interact key. It takes the item and asks where it goes.
3. Walk to a chest and interact. The nisse files the item there, and remembers *this kind of thing goes
   here*.

Three inputs, no menus, controller-friendly, and it is exactly the kind of thing a household spirit
would do. The menu still exists for bulk editing and for players who want precision — but the default
path is a demonstration.

The same gesture in reverse teaches supply: hand the nisse a crafting material while standing at a
workbench and it marks the chest it came from as a supply source.

## What it has to replace

Quartermaster's public feature list, and how each one becomes a creature action rather than a system.

| Quartermaster feature | Husnisse equivalent |
|---|---|
| Deposit Chests (auto-sort into matching storage) | The nisse's rounds: it carries the deposit pile into the chests it has learned |
| Remembered items (chests remember types when empty) | The nisse remembers; the chest does not need to |
| Storage rules (preferred, overflow, base groups, copy settings) | Rooms: a nisse claims a radius, multiple nisse = base groups. Preferred/overflow stay as settings |
| Supply permissions (which chests feed crafting/production) | Teaching a chest as a pantry; the nisse will fetch from pantries only |
| Inventory tools (deposit all, sort, combine stacks) | Orders given to the nisse: Tidy, Combine, Empty Pockets |
| Pickup controls (auto-collect certain types) | The nisse collects drops inside its claim while working |
| Stack sizes | Plain config, no creature angle — it is a number, not a behaviour |
| Craft from storage | The nisse answers the call: crafting draws from its claim because it is the one holding the network together |
| Building coverage (station range extension) | The hearth's warmth extends station coverage inside the claim |
| Production (supply processors, collect output) | The nightly round feeds and empties machines in the claim |
| Machine controls (caps, pause, protect valuable wood) | Standing orders per machine, set once, obeyed by the nisse |
| Refuelling and feeding (fires, torches, animals, taming) | Part of the round: fuel in, ash out, troughs filled |
| Owl companion | The creature itself — there is no separate mascot |
| Helmsman ship unloading | A nisse whose claim reaches water unloads a docked ship's cargo into its chests |
| Searchable menus, range previews, controller | The ledger panel: one screen listing what the nisse knows, with the same range preview |
| Config sections | `1. General`, `2. Nisse`, `3. Rounds`, `4. Storage`, `5. Production`, `6. Ward` |

Not everything should be a creature action. Numbers are numbers. The rule is that **no item moves unless
the nisse moves it** — settings that describe *where* and *what*, not *how*, stay as config.

## Core systems

### Claim

A nisse claims a radius around its bowl (the home piece). Claims do not overlap; a second nisse
refuses to settle inside an existing claim and wanders to the nearest free spot. That is how base
groups emerge without a "group" setting.

Only the player who placed the bowl can feed or teach that nisse; everyone else can read the ledger.
WardIsLove permissions filter every container from 0.1: a bowl inside a ward its owner cannot use
refuses to settle, and an optional stricter mode limits a nisse to wards its own player placed. The
rest of the ward integration lands at 0.5. The placer rule costs almost nothing and prevents the
obvious grief on a shared world from day one. The full pack analysis lives in
[`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md).

### Memory

Per nisse, persisted in its own ZDO:

- `item → chest` assignments, learned by demonstration or set in the ledger.
- `chest → role` (storage, pantry, overflow, machine input, machine output).
- `machine → standing order` (fill to N, empty always, leave X wood).

Memory is the nisse's, not the chest's. Move a chest and the nisse follows it because it tracks the
container object, not a coordinate.

### Rounds

A round is a bounded pass, not a loop:

```
every RoundInterval (default 30 s):
    for each claim, if the nisse is awake, fed and in range:
        collect drops           (budget 20 items)
        sort the deposit pile   (budget 40 items)
        service machines        (budget 10 actions)
        refuel fires and troughs(budget 10)
        report what it could not finish
```

Budgets are per round and configurable. The nisse never does more work than the budget, which is what
keeps a large base from spiking the server and what makes the fiction honest.

### Movement

The nisse walks, but not everywhere. Measured against the live game rather than assumed:

- **Built floors are navmesh.** `Pathfinding` bakes the `piece` layer, so a wood floor is walkable and
  a path can end on it.
- **A 2 m doorway is not.** On a 3×3 room with a 2 m gap between wall pieces, a path from the walkway
  into the room stops 5.42 m short — at the threshold — once the tile rebuild includes the walls. A
  0.4 m agent radius plus wall posts leaves no corridor the navmesh builder will bridge.
- **Tiles build lazily.** They are 32 m and rebuild at most once per 10 s; `GetPath` pokes a tile and
  then snaps immediately, so the first request in a fresh tile always fails.

So walking is **room-scale and outdoors**, and a threshold fades. That is not a compromise bolted on
at the end: a household spirit does not use doors, it steps through the wall. A round pokes the next
destination, the following round walks it, and after one failed retry the nisse fades and reports that
it could not reach — legible failure, same as everything else.

### The bowl

The home piece. It stores food, satiety and mood. Mood decays with time and work; food restores it.
Below a threshold the nisse stops, sits by the bowl, and the ledger says why. Prolonged neglect and it
starts a sulk: it hides one random stack per day in a wrong chest until it is fed again — never more
than three stacks at once, and mood only decays while a player is online, so a week away is not a
week of misfiling.

Sulking is deliberately recoverable and never destructive — nothing is deleted, only misfiled. It is a
consequence with a story, not a punishment.

Feeding is a policy, not a tax. The host's `2. Nisse / Feeding Mode` is written into the world as a
global key on load — **Required**, **Optional**, **Disabled** — so every peer reads the same value and
no client can disagree with what the server simulates. Under **Optional** each owner may switch feeding
off at their own bowl with Alt-interact; that choice lives in the bowl's ZDO. Both decisions are world
state, which is why the two halves can never drift apart.

### Server authority

All item movement is server-side, in loaded areas only. The same constraint Quartermaster documents,
and it has to be documented here too rather than papered over.

## Technical architecture

| Piece | Approach |
|---|---|
| **Creature** | A vanilla `Character` prefab cloned at runtime (Greyling is the cheapest humanoid) and re-tinted, wrapped in Jötunn's `CustomCreature` and registered through `CreatureManager`, with our own `BaseAI` subclass. No new art for v1 — this is the single biggest scope saving and it is legitimate: we are using Valheim's own assets at runtime, which is what the game gives every mod. |
| **Home piece** | A small build piece (bowl/hearth) registered as a Jötunn `CustomPiece`. Holds claim radius and bowl state. |
| **Claim registry** | Rebuilt on world load from ZDOs, then maintained incrementally on `ZDO.SetOwner` and container changes. No per-frame scans. |
| **Item movement** | `Inventory` APIs on the server. Every move checks ward access before touching a container. |
| **Save data** | nisse state in its own ZDO (`radius`, `mood`, `satiety`, `assignments`); chest roles in the chest's ZDO; machine orders in the machine's ZDO. Nothing in config. |
| **Config** | Numbered sections, host-owned for anything that changes world state, client-local for UI. Synced with ConditionalConfigSync, so each key can opt in or out of force-sync; Jötunn's own sync only covers `AdminOnly` entries. |
| **Compatibility guard** | If Quartermaster is installed, refuse to run and say so. Two storage systems on one chest is how items vanish. |

**Dependencies.** BepInEx + Harmony, Jötunn, and ConditionalConfigSync — nothing else. Jötunn is
required on the server and on every client; a missing or wrong version refuses to start with a
readable message rather than failing later. Husnisse is its own mod: it does not require the server pack,
and it does not require Quartermaster to be absent at install time.

## Relationship to Quartermaster_Integrations

QI exists because Quartermaster has no API. If Husnisse replaces Quartermaster, QI's reason to exist goes
with it — and that is the end state worth aiming at: the storage answer should live in the mod that owns
the storage.

**But not from the start**, for three reasons:

| Merge at 0.1 | Consequence |
|---|---|
| **Parity gap** | QI's job is making storage visible to craft UIs. Husnisse cannot do that until 0.3 (craft-from-claim). Folding QI in early leaves the pack with no working bridge for two releases. |
| **Deployment** | QI is client-only. Husnisse is server-required. A merged plugin is server + clients, and every client-only UI patch has to no-op when headless. |
| **Focus** | 0.1 exists to answer one question: is feeding a spirit to get your base sorted fun? Nine integrations and a public API are noise until that answer is yes. |

What makes the merge attractive *later* is that **QI is our code**. Its nine integrations are the one
thing in this whole project we may lift wholesale, because we own the copyright. At 0.3 the port is a
data-source swap: the patch targets (AAA, MyLittleUI, EpicLoot, PlanBuild, ExtraSlots, AdventureBackpacks)
all stay the same — only the query behind them changes from Quartermaster's storage to the nisse's claim.

### Sequence

| Version | Storage API | QI |
|---|---|---|
| 0.1 | none | unchanged, still bridging Quartermaster |
| 0.2 | read-only: `Count`, `CanAfford` within a claim, version handshake | unchanged |
| 0.3 | adds `TryConsume`; craft-from-claim works | integrations ported into Husnisse; QI ships a final release pointing here, then unlists |
| 1.0 | stable surface | retired to a shim or gone |

0.3 is cheaper than it looked. Container contents already replicate inside the container's ZDO, so
`Count` and `CanAfford` are a client-side scan of instantiated containers; only `TryConsume` needs
server-side work. No custom sync layer, no streaming design.

To make the port cheap, three things have to be true from 0.1: Husnisse's storage core sits behind the same
interface shape QI's integrations expect, the `StorageApi` semantics are written once and shared, and no
integration concern leaks into the creature code.

The shared semantics live in
[`../quartermaster_integrations/docs/STORAGE-CONTRACT.md`](../quartermaster_integrations/docs/STORAGE-CONTRACT.md).
QI is implementation #1; the nisse's claim is implementation #2. Where the two could disagree — source
order, spend order, ward filtering, all-or-nothing spending, reentrancy — that file decides.

## What this is not

- **Not a port.** See `CLEANROOM.md`. No Quartermaster code, no decompiled dumps in the tree, no
  line-by-line translation.
- **Not a drop-in migration at v1.** Quartermaster's deposit chests are its own prefabs; removing it
  removes them. A migration tool arrives at 0.5 and reads runtime ZDO data only, never its source.
- **Not a Housecarl module.** Different domain, different users, server-side. It is its own project and
  its own release.

## Versions

| Version | Scope | Why |
|---|---|---|
| **0.1** | Creature (a nisse), bowl, claim, memory, sort-only rounds. **No navigation** — it fades between bowl and chest | Proves the fiction is playable. If feeding a spirit to get items sorted is not fun, stop here. |
| **0.1.1** | "Legs": room-scale and outdoor walking, fade across thresholds | Only after 0.1 playtests well. Movement changes the feel without changing the loop. |
| **0.2** | Teaching by demonstration, ledger panel, chest roles | The signature mechanic; needs 0.1's memory to build on. |
| **0.3** | Pantries and craft-from-storage. Counts read straight from container ZDOs — no sync layer | First feature that touches another mod's flow; needs the supply rules settled. |
| **0.4** | Machines: fuel, feed, collect, caps | The nightly round earns its keep. |
| **0.5** | Ward integration, multiplayer hardening, Quartermaster ZDO migration | Server authority and permissions, before anyone runs this on a shared world. Migration reads runtime data only, which the clean-room rules already allow. |
| **1.0** | Stack sizes, ship unloading, comfort-based coverage | Parity, once the creature model is proven. |

## Risks

- **The fiction might be tiring.** A creature you must feed is friction. If 0.1 playtests as a chore,
  the answer is to make feeding interesting (meals with effects) or to let a fed nisse run for days —
  not to add an "auto" switch that deletes the concept.
- **Pathing is not free, and not universal.** Measured: 32 m navmesh tiles rebuild at most once per
  10 s, the first path request in a fresh tile always fails, and a 2 m doorway is not passable at all.
  The poke-ahead/retry/fade rule is the mitigation. The 200-container benchmark still has to happen
  before 0.4.
- **Dependency surface.** Jötunn on server and clients, plus ConditionalConfigSync. Both are common in
  packs, but a version mismatch is a support burden — which is why the startup guard exists.
- **Multiplayer.** Ownership, wards and desync are where storage mods die. 0.5 exists because of this.
- **Migration.** Moved to 0.5. Replacing Quartermaster on a live world is a data problem, not a code
  problem, and the only safe input is its runtime ZDO data — never its source.
- **Scope.** Parity is 15 features. The creature model makes each one *different*, not cheaper.
