# Roadmap — refined 2026-11-05

`PARITY.md` is the definition of done. This file is the order of work: what each release contains, what
proves it, and what it depends on. When they disagree, PARITY wins on scope and this file wins on
sequence.

## Rules that shape every release

- **The creature is the visible cause.** Nothing moves while the nisse is unfed, asleep or sulking; work
  drains from a queue at a configured rate and is seen as trips.
- **Native floor, one authority.** Every function works with no other mod; an owner mod replaces it for
  the bowls/containers where it applies, never alongside.
- **Standalone.** Jotunn and ConditionalConfigSync are hard; everything else is optional and absent-safe.
- **A release is done when its acceptance checks pass**, the changelog has an entry, and version numbers
  match in all four places. A surface closed without a test is not closed.

## Improvements folded in

From `docs/IMPROVEMENTS.md` (accepted 2026-11-05):

| # | Improvement | Release |
|---|---|---|
| 1 | Priority among containers of the same category | 0.3 |
| 2 | Overflow as a category (full categories **and** unmatched items) | 0.3 |
| 3 | Auto-suggest the dominant category on first toggle | 0.3 |
| 4 | Checkbox preview before saving a custom list | 0.3 |
| 5 | Copy a rule to nearby / same-type containers | 0.3 |
| 7 | `husnisse_prefabs` dump to `BSBLogs` | 0.1.2 |
| 9 | Animation LOD by distance | 0.3 |
| 10 | Fair round-robin queue | 0.3 |
| 11 | Read-only badge for players without permission | 0.3 |
| 12 | YAML rule export / import | 0.3.1 |
| 13 | YAML-editable presets | 0.3.1 |
| 14 | Rate presets 2 / 5 / 10 | 0.1.2 |
| 15 | Deposit chest as a category, plus deposit-all semantics | 0.3 |

Rejected: recent-moves undo (#6), pause (#8), per-mod blacklist (replaced by the prefab dump).

## Ladder

| Version | Name | Scope | Depends on |
|---|---|---|---|
| 0.1.1 | Playtest fix | shipped: station-free bowl, registration facts | — |
| 0.1.2 | Flow, pantry and safety | four-slot food bowl with drain rate; queue, rate, server orders, invulnerability, prefab dump | 0.1.1 |
| 0.1.3 | Client startup hotfix | localisation no longer aborts before Harmony/content registration | 0.1.2 |
| 0.1.4 | Legs | walking, fade across thresholds, trip animation | 0.1.3 |
| 0.2 | Claim ownership | provider seam, WardIsLove handover, range preview, parity harness | 0.1.4 |
| 0.3 | Network core | container UI, categories, roles, routing, deposit, LOD, fairness | 0.2 |
| 0.3.1 | Teaching and ledger | teaching, ledger, pickup filter, pocket merge, YAML rules | 0.3 |
| 0.4 | Read-only contract | Count/CanAfford, sources, EpicLoot/AAA/PlanBuild/MLUI readouts, QI stand-down | 0.3.1 |
| 0.5 | Craft from claim | `TryConsume`, the visible supply errand, craft repaint, DropMoreLoot deferral, stack rules | 0.4 |
| 0.6 | Machines and permissions | processors, refuel, feed, caps, four-way permissions, mailbox decision | 0.5 |
| 0.7 | Multiplayer and migration | shared chests, ownership revision, migration route | 0.6 |
| 1.0 | Parity | stack sizes, ship unloading, coverage, config import, sign-off | 0.7 |

## 0.1.2 — Flow, pantry and safety

**Scope**

- Bowl pantry: a real `Container`, four slots, food only, opened by ordinary interact. The nisse eats
  one item per `BowlDrainMinutes` (default 20, range 1–240) while satiety is below full.
- Hybrid flow: continuous drain at `ItemsPerSecond` (default 5; presets 2/5/10), `ReconcileInterval`
  (5 s), bounded 2× catch-up on world load.
- Move Empty Pockets and Tidy onto the same queue; no order moves items instantly any more.
- Server authority: order requests travel over `ZRoutedRpc`; the server validates and enqueues.
- Hotbar and equipped slots are excluded from Empty Pockets.
- Invulnerability: no damage, enemies ignore the nisse, no death state.
- `husnisse_prefabs`: dump every container prefab (name, width, height, piece name) to
  `BepInEx/BSBLogs/Husnisse-prefabs.txt`.
- Diagnostics: queue depth, items/s, bowl contents, items skipped and why.

**Done when**

- Food goes into any of the four slots; a non-food item is refused.
- One item leaves the bowl per configured interval; contents survive relog.
- A 200-item ground drop drains at the configured rate, visibly, with no instant jumps.
- An order enqueues instead of teleporting items, from a client against a dedicated server.
- Hitting the nisse does nothing; a raid ignores it.
- `husnisse_prefabs` produces a complete list on a dedicated server and on a client.

**Risks** — rate too slow for cleanup; queue starving idle nisse; catch-up spiking on load; container
interaction and food filtering living on different peers. All four are measurable in the diagnostics
this release adds.

## 0.1.4 — Legs

**Scope**

- Pathfinding movement: room-scale indoors, outdoors, trip to the target container.
- Fade across thresholds: a 2 m doorway is not navmesh-passable (measured), so the nisse fades through.
- Trip animation: walk, transfer, return or fade; no per-frame teleporting.
- Failure policy: one retry, then fade and report "could not reach".

**Done when**

- A 20 m indoor round trip completes within two attempts and is watchable.
- Doorways produce a fade, not a stuck nisse.
- Upper floors work if a path exists; if not, the fallback is a visible fade, never a silent no-op.

**Risks** — 32 m tiles and 10 s rebuild make the first path fail; the retry policy must hide that.

**Status (2026-11-14)** — implemented. Headless proof on the rig: a 20 m walkway round trip at 1.94 m/s
with no fades, a cold tile carried by the one retry, and a closed room producing `leg blocked` → fade →
delivery. The doorway measurement was refined: a free-standing 2 m opening is walkable on this build,
while the earlier enclosed 3×3 room stopped at the threshold — shape decides, so walking and fading are
both correct outcomes and the fade is the fallback. **The client visual pass is still open**: the
client VM was offline when the build was ready.

## 0.2 — Claim ownership

**Scope**

- `IClaimAuthority` seam with native and WardIsLove providers.
- Per-bowl resolution: outside a ward native; inside a ward the ward's range/owner/permits; one bowl per
  ward; overlapping wards resolve to the placer's ward, else the smallest.
- Live switch when a ward arrives or leaves; snapshot native radius/owner for fallback.
- Range preview at placement and in the ledger.
- Diagnostics: `claim_source`, ward id, effective radius.
- Parity harness begins: native claim, ward claim, ward removed, second bowl in a ward, overlap.

**Status** — 0.2a (provider seam, per-bowl resolution, snapshot, live switch) and 0.2b (range preview
at placement and in the hover/ledger seam, `husnisse_preview`, effective-centre diagnostics, the five
parity scenarios) implemented 2026-11-15. **The five scenarios pass headlessly and with a real client
connected, on the same final build** (`Husnisse.dll` sha256 `a790429e…`); evidence and screenshots are in
`docs/evidence/0.2b/`. The ledger panel is 0.3.1, so 0.2b ships the preview calculation and ring it will
call (`ClaimPreview.Evaluate` + `ClaimRing.ShowFor`) and exposes them today through the placement ghost,
the bowl hover and `husnisse_preview`.

**Done when** — the five harness scenarios pass headlessly and in a client session. **Met 2026-11-15**:
native 19, ward 21, ward removed 14, second bowl 11, overlap 31 assertions; the client's own claim state
matched every transition (client-side `c02 claim` log).

**Risks** — WardIsLove's API differs by version; the reflection seam must fail closed (native claim),
never half-bound.

## 0.3 — Network core

**Scope**

- Container UI: toggle and settings button top-right; hidden outside a claim or when blacklisted.
- Rules: item categories (survival presets expanding to types) with priority; custom list from a
  checkbox preview; **Overflow** (one/many; full categories and unmatched items); **Deposit** (one/many;
  dump everything in, the nisse sorts out).
- Auto-suggest the dominant category on first toggle; copy a rule to nearby/same-type containers.
- Per-container blacklist; shipped defaults for carts, ships, display piles.
- Deposit-all: network container hands items to the nisse; off-network container dumps every pocket item
  in, unsorted.
- Animation LOD by distance; fair round-robin queue; read-only badge for non-permitted players.
- Server authority: rules in container ZDO, written by RPC, client no-ops headless.

**Done when** — a 20-container base can be configured entirely through the UI; every routing case
(category, priority, overflow, deposit, unmatched) lands in the intended chest; the 200-container
benchmark is recorded; a blacklisted container never shows a button.

**Progress** — 0.3a landed the container UI and rule storage (buttons, settings panel, `husnisse_rule`
console path, ZDO encoding). 0.3b landed the preset table, priority, the saved list, the first-toggle
suggestion, copy-rule and the rule-aware router, with a headless gate on every routing case
(`docs/evidence/0.3b/`). 0.3c added the full-category case (a full chest does not claim a stack, the
next chest of the category is tried before Overflow, and the reason names the rules that were full),
Deposit chests as a **source** the nisse drains, and deposit-all on and off the network
(`docs/evidence/0.3c/`). 0.3d finished the release: the blacklist's two layers behind one button
predicate (class defaults follow `4. Storage / Skip Carts and Ships` and `Skip Display Piles`, the
per-container flag is `husnisse_rule blacklist`, and `netall` joins a whole prefab), the queue's
round-robin lanes with a trip filling its batch from the lane it serves, per-peer animation LOD
(`2. Nisse / Animation LOD Distance`), and the 200-container benchmark (`husnisse_bench`, recorded in
`docs/evidence/0.3d/`). 0.3 is code-complete and unreleased. The panel's controls, the deposit-all
button and the LOD's visual behaviour are built and gated headlessly but were not exercised on a
client.

**Risks** — vanilla container UI patching is the most compatibility-sensitive work in the project.
MyLittleUI and other UI mods must be tested; the buttons must survive a controller and a narrow screen.

## 0.3.1 — Teaching and ledger

**Scope**

- Teaching by demonstration writes the same custom list the UI writes.
- Ledger: per-claim rules, unknown containers to classify, pickup filter per item type, blacklist
  management, read-only view for permitted players.
- Pocket stack merge on sort.
- YAML export/import of rules and editable presets (all-or-nothing import; container mapping screen).

**Progress** — implemented 2026-11-15. Teaching is the deposit itself: Alt-interact the nisse (it
watches for two minutes, `[Teach]` diagnostics), then move a sample into a chest; the move is observed
where the game performs it (`Inventory.MoveItemToThis`, both overloads = drag and shift-click), and the
chest's rule becomes a custom list with that item, joined to the network in one server write
(`RuleOp.SetRuleAndNetwork`). A move made while the nisse is not watching changes nothing. The ledger
(`Husnisse / Ledger Key`, default **H**, inside the inventory) lists every container with its rule,
priority, network flag and blacklist, marks the unclassified ones, and offers classify / rule / priority
/ network / blacklist / Tidy / pickup-filter / range — every button one call into `LedgerActions`, the
same layer the `husnisse_ledger` console command uses. The pickup filter is per claim, stored in the
bowl's ZDO and applied by the ground scan only. Empty Pockets merges partial pocket stacks first. The
rules file (`husnisse_rules export|plan|map|apply|preset`) round-trips every rule, refuses the whole
import when one entry does not fit, and publishes editable presets as a global key so routing and
suggestions agree. Evidence: `docs/evidence/0.3.1/`.

**Done when** — teach three item types and they route; ledger edits match the UI; a YAML export imports
into a copied world with every rule intact or nothing applied. **Met 2026-11-15** headlessly on the
spare tree: `husnisse-031-teach` (three taught types route by name, a control chest left unwatched keeps
no rule, 19 assertions), `husnisse-031-ledger` (classify/rule/priority/net/blacklist via the ledger
layer, pickup filter live both ways, pocket merge, 25 assertions), `husnisse-031-rules` (preset narrowed
and restored, export → mutate → apply restores every rule, one destroyed chest refuses the whole import,
26 assertions). The **UI/ledger client proof** (the gate this card names) is recorded on a real client
the same day (`Stranger`, build `31ddbd35…`, `docs/evidence/0.3.1/RESULT.md`): Alt plus a sample taught
two chests (`[Teach] learned … rule=custom:1`) while an unwatched move wrote nothing, the ledger listed
the claim (`4 container(s) · 1 unclassified`), its `Metals` click showed up as `Rules: Metals` in the
chest's own window, its Net click put the chest on the network, its Blacklist click removed the chest's
strip, and its pickup click wrote the claim filter — and the server's `husnisse status` reads back every
one of those writes.

**Risks** — two editing paths (teaching and ledger) writing the same state must share one writer, or
they will drift. They do: teaching sends through `RuleRpc`, the ledger through `LedgerActions` →
`RuleRpc`, and both land on `ContainerNetwork.WriteRule`.

## 0.4 — Read-only contract

**Scope**

- `Count`, `CanAfford`, `IsReady`, `ApiVersion`, `ScopeDescription`; source order pockets → ExtraSlots →
  AdventureBackpacks → claim.
- EpicLoot provider registration, refusing to double-register beside EpicLootContainerBridge.
- AAABuildMenu build-row counts; PlanBuild blueprint costs; MyLittleUI requirement readouts.
- QI stand-down: when QI is loaded, no provider registration; diagnostics `provider=qi|native`.
- All client patches no-op headless.

**Done when** — craft and build UIs show claim totals with QI absent, QI present (stand-down), and
EpicLoot present; no double counting; no server-side UI work.

**Risks** — each UI mod's binding is a separate API surface; five have never been exercised outside the
pack.

**Status (2026-11-15)** — implemented on branch `husnisse/0.4-readonly`, build, tag and client gate done;
unreleased (internal only).

- `Contract/StorageContract` answers `ApiVersion 1`, `Provider`, `IsReady`, `ScopeDescription`, `Count` and
  `CanAfford` over the contract's source order — pockets → ExtraSlots rows → equipped AdventureBackpacks
  pack → the containers of the claim the player is standing in — and `husnisse_contract [item]` plus
  `husnisse_status` print the same lines a consumer gets. `TryConsume` answers `false` and says why once:
  this release reads, it does not spend.
- The claim leg is the nisse's own question (`ContainerIndex.InClaim` → `ContainerFilter.CanUse`, the
  blacklist on top), so the ward ground rule, the cart/ship/display-pile exclusions and the backpack rule
  apply to a craft row exactly as they apply to a trip. A claim the player is not permitted on contributes
  nothing; no claim contributes nothing.
- Readouts: the vanilla requirement row (`10 (42 [12+30])`, split by default, off with
  `5. Contract / Requirement Readouts`), AAA Crafting's container counts and batch ceiling,
  AAABuildMenu's pinned rows. MyLittleUI's own pocket-only number is switched off for the session so the row
  has one writer.
- `provider=qi|native` in the diagnostics; with Quartermaster_Integrations loaded Husnisse registers no
  consumer integration at all.
- **Two integrations are deliberately deferred to 0.5**, and both are the same shape: EpicLoot's inventory
  provider and PlanBuild's blueprint-cost adapter are lists that are *both read and spent*. EpicLoot takes
  the player's pockets first and only then asks the provider, so a count-only provider loses the pocket half
  of a cost it cannot finish; PlanBuild's take is `void`, so a count-only entry places blueprints for free.
  Both are detected and explained in the log today, and they land with `TryConsume` and its server round
  trip. This is a scope correction to this release, not a silent omission.
- Proof: `testrig/scenarios/husnisse-04-contract.cmd` (PASS, 15 assertions, dedicated server) and a real
  client gate with the consumer pack installed — `docs/evidence/0.4/RESULT.md`. The row number the client
  read back is `5 (55 [10+45])`, the count is `55 = 10 carried + 45 claim`, walking 30 m out of the claim
  drops it to `10`, and a client relog keeps both. Contract test 3 (a ward-locked container) is inherited
  from the 0.2b claim proof rather than re-run with a live ward; that is the one open corner.

## 0.5 — Craft from claim

**Scope**

- `TryConsume`, all-or-nothing, reentrancy-safe.
- Delivery fiction for consumed materials (decide: carry, fade at the bench, or requisition).
- Craft-list repaint after a craft; DropMoreLoot stack-size deferral; stack rule relaxations (crafter,
  ignored tags, EpicLoot components; never quality/level/durability).
- **Carried over from 0.4**: EpicLoot's inventory provider and PlanBuild's blueprint-cost adapter. Both are
  seams that are read *and* spent, so they cannot exist before `TryConsume` (see the 0.4 status); with it,
  each is a data-source swap from Quartermaster_Integrations' version, which is our code.

**Done when** — a craft spends pockets first, then claim storage, with a visible delivery and no
duplication; the recipe list is correct immediately after the craft.

**Risks** — consumption is the first place the law and the contract can contradict each other. The
delivery fiction must be decided before code, not during.

**Status (2026-11-15)** — implemented, unreleased (internal only). **The decision that shaped it:** the
delivery fiction is a **fetch before the pay**, not a fade, a requisition, or a provider that counts stock
it cannot take.

- The claim leg is a *transfer*, performed by the nisse: a craft the pockets cannot pay for sends the
  nisse to the chests the plan named, it takes the planned stacks while it stands at each one, walks a
  visible load to the bench and puts it in the player's hands, and the game's own craft then spends the
  pockets. Nothing leaves a chest at request time and the craft cannot run before the materials are held,
  which is what "no silent consumption" means in practice.
- `TryConsume` is honest about the split: it debits pockets → ExtraSlots rows → equipped pack, and when
  an amount would need the claim it asks the nisse for the shortfall and answers `false` with nothing
  mutated. A claim chest's contents belong to its ZDO owner, so a synchronous client call cannot debit
  one; returning `true` on a promise is how a craft gets paid for by a deletion. `CanConsume` is true and
  `can_consume=True` appears in the contract report from this build.
- Reentrancy: `TryConsume` answers `false` while a supply errand for that player is in flight, and the
  claim runs one errand at a time (a second request is `Busy`). The deposit queue yields to an errand
  the same way it yields to a trip.
- **EpicLoot and PlanBuild are not registered as count-plus-pay providers**, and that is the correction
  this release makes to their 0.4 deferral: both APIs pay through the same list they count and neither
  checks the remainder (EpicLoot's pay path ignores the return; PlanBuild's take returns `void`), so a
  provider counting the claim would make the UI say affordable and leave the cost unpaid — a free
  enchant or a free blueprint. Both instead hook the *affordability* question that precedes every one of
  their costs, and start the same fetch; the mod's own pay path then takes the materials from the
  pockets. Same patch targets, same data source, different order of events.
- DropMoreLoot: Husnisse never writes a stack size, so there is nothing to disable — the diagnostics say
  whether DropMoreLoot owns them (`stacks=dropmoreloot xN`). The merge rule is vanilla's plus nothing:
  crafter and EpicLoot's material stamps are not part of stack identity, while quality, world level,
  variant and durability are.
- Proof: `testrig/scenarios/husnisse-05-supply.cmd` — **PASS, 20 assertions** on a dedicated server: a
  request larger than the claim holds is refused whole with the chest untouched, the chest still reads
  20 wood in the same tick the accepted request is made, a second request is `Busy`, the nisse's pickup
  is logged at the chest, the load lands in the player's pockets (`pockets stacks=1 items=10`), the
  chest's packed ZDO drops 20 → 10, the errand ends idle, and `husnisse contract Wood` reads 20 with
  `can_consume=True`. The client gate (visible bundle, the craft itself, the repaint, AAA's batch and the
  two consumers' fetch-on-afford) is `docs/evidence/0.5`.

## 0.6 — Machines and permissions

**Scope** — processors, fermenters, cooking racks; collect finished goods; caps, pause, protect valuable
wood; refuel fires and torches; feed animals and taming creatures; four-way supply permissions; mailbox
decision recorded (keep or drop).

**Done when** — a smelter is fed and emptied unattended; caps hold; permissions are enforced per chest.

**Status — implemented 2026-11-15, headless-proven; client visual pass open.** The mailbox is **dropped**
(decision 2026-11-13; the docket entry is closed rather than deferred). Gates:
`testrig/scenarios/husnisse-06-machines.cmd` (21 assertions), `husnisse-06-permissions.cmd` (25),
`husnisse-06-home.cmd` (17); evidence in `docs/evidence/0.6/RESULT.md`. A smelter is fed its fuel and its
ore, held at both caps, made to work under a roof and emptied into the chest whose rule claims the bars;
`none`/`production`/`fuel`/`-crafting` are enforced per chest on the same setup; a fire is refuelled and
starved by its chest's fuel permission; a hungry boar gets a portion and gets nothing when the chest may
not feed. Two things the rig cannot prove and does not claim: a creature eating the portion (a spawned
creature on a zone-less server does not walk its consume path) and the client UI (four supply toggles, the
ledger's Pause machines switch).

## 0.7 — Multiplayer and migration

**Scope** — shared-chest handshake and ownership revision; two-client authority tests; the chosen
migration route proven on a copied world (QM present read-only, or a one-shot converter).

**Done when** — two clients cannot duplicate or lose an item across a chest handoff; migration preserves
contents on a copied world.

**Implemented 2026-11-15** — `Storage/ChestHandover.cs`, `Net/ChestRpc.cs`, `Storage/ContainerWrite.cs`
(acquire → re-read the ZDO → move → write back and read back), `Patches/StaleChest.cs`,
`Migration/*`, `Commands/MigrateCommand.cs`; evidence in `docs/evidence/0.7/RESULT.md`.

- The server asks the chest's owning peer for the ZDO instead of claiming it. A chest a player has
  open is never taken: the errand goes back on the queue, and the routing layer does not even choose
  an in-use chest as a destination. A claim is only used when the recorded owner is not a connected
  player, and it is verified before anything moves.
- Every server-side move re-reads the packed inventory out of the ZDO first. A deposit into a chest
  whose ZDO moved under the server merges with it instead of overwriting it (proven: 5 wood written
  straight to the ZDO + 8 stone deposited = 13 items), and a move that cannot be persisted is refused
  rather than performed.
- The client closes the container window when it loses ownership and clears the stuck `m_inUse` flag,
  so a stale grid can neither be written back nor survive a reopen.
- Migration reads Quartermaster's per-container JSON record out of the ZDO map (no Quartermaster
  installed, no key names needed) and translates role, supply permissions, priority and remembered
  items. On a copy of the live world: 154 records, 154 applied, **contents byte-identical** before and
  after, idempotent on a second run, unchanged across a save and reboot. 39 records belong to prefabs
  the world no longer has (Quartermaster's own chests) and are reported with their stack counts; 92
  item prefabs were untranslated because the migration tree did not have the pack installed.

**Still open on this card** — the client half of the handshake (ask → save → release → ack) was not
driven end to end: the client run proved only that a chest a player has open is never written. A
two-client test is not possible on the current rig (one client target). Both are named in
`docs/evidence/0.7/RESULT.md`.

## 1.0 — Parity

Stack sizes, ship unloading, comfort coverage, QM config import, and a surface-by-surface sign-off
against `PARITY.md` on a copied world.

## Cross-cutting

- **Parity harness** starts at 0.2 and gains a scenario per surface closed; it is the only proof that a
  gap is closed.
- **Benchmarks**: 200 containers at 0.3; 10 nisses at 0.7; queue catch-up at 0.1.2.
- **Every release**: changelog entry, four version fields, updated PARITY statuses, updated this file if
  scope moved.
- **Open decisions**: mailbox (0.6), migration route (0.7), QM config import (1.0). The delivery
  fiction is settled: the nisse fetches the shortfall and hands it over before the craft spends the
  pockets.
