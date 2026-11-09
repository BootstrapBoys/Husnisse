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
| 0.1.2 | Flow and safety | queue, rate, orders on the queue, invulnerability, prefab dump | 0.1.1 |
| 0.1.3 | Legs | walking, fade across thresholds, trip animation | 0.1.2 |
| 0.2 | Claim ownership | provider seam, WardIsLove handover, range preview, parity harness | 0.1.3 |
| 0.3 | Network core | container UI, categories, roles, routing, deposit, LOD, fairness | 0.2 |
| 0.3.1 | Teaching and ledger | teaching, ledger, pickup filter, pocket merge, YAML rules | 0.3 |
| 0.4 | Read-only contract | Count/CanAfford, sources, EpicLoot/AAA/PlanBuild/MLUI readouts, QI stand-down | 0.3.1 |
| 0.5 | Craft from claim | TryConsume, delivery fiction, repaint, DropMoreLoot, stack rules | 0.4 |
| 0.6 | Machines and permissions | processors, refuel, feed, caps, four-way permissions, mailbox decision | 0.5 |
| 0.7 | Multiplayer and migration | shared chests, ownership revision, migration route | 0.6 |
| 1.0 | Parity | stack sizes, ship unloading, coverage, config import, sign-off | 0.7 |

## 0.1.2 — Flow and safety

**Scope**

- Hybrid flow: continuous drain at `ItemsPerSecond` (default 5; presets 2/5/10), `ReconcileInterval`
  (5 s), bounded 2× catch-up on world load.
- Move Empty Pockets and Tidy onto the same queue; no order moves items instantly any more.
- Invulnerability: no damage, enemies ignore the nisse, no death state.
- `husnisse_prefabs`: dump every container prefab to `BepInEx/BSBLogs/Husnisse-prefabs.txt` (name,
  components, source folder where known).
- Diagnostics: queue depth, items/s, trips attempted/succeeded/faded, items skipped and why.

**Done when**

- A 200-item ground drop drains at the configured rate, visibly, with no instant jumps.
- An order enqueues instead of teleporting items.
- Hitting the nisse does nothing; a raid ignores it.
- `husnisse_prefabs` produces a complete list on a dedicated server and on a client.

**Risks** — rate too slow for cleanup; queue starving idle nisse; catch-up spiking on load. All three
are measurable in the diagnostics this release adds.

## 0.1.3 — Legs

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

## 0.2 — Claim ownership

**Scope**

- `IClaimAuthority` seam with native and WardIsLove providers.
- Per-bowl resolution: outside a ward native; inside a ward the ward's range/owner/permits; one bowl per
  ward; overlapping wards resolve to the placer's ward, else the smallest.
- Live switch when a ward arrives or leaves; snapshot native radius/owner for fallback.
- Range preview at placement and in the ledger.
- Diagnostics: `claim_source`, ward id, effective radius.
- Parity harness begins: native claim, ward claim, ward removed, second bowl in a ward, overlap.

**Done when** — the five harness scenarios pass headlessly and in a client session.

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

**Risks** — vanilla container UI patching is the most compatibility-sensitive work in the project.
MyLittleUI and other UI mods must be tested; the buttons must survive a controller and a narrow screen.

## 0.3.1 — Teaching and ledger

**Scope**

- Teaching by demonstration writes the same custom list the UI writes.
- Ledger: per-claim rules, unknown containers to classify, pickup filter per item type, blacklist
  management, read-only view for permitted players.
- Pocket stack merge on sort.
- YAML export/import of rules and editable presets (all-or-nothing import; container mapping screen).

**Done when** — teach three item types and they route; ledger edits match the UI; a YAML export imports
into a copied world with every rule intact or nothing applied.

**Risks** — two editing paths (teaching and ledger) writing the same state must share one writer, or
they will drift.

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

## 0.5 — Craft from claim

**Scope**

- `TryConsume`, all-or-nothing, reentrancy-safe.
- Delivery fiction for consumed materials (decide: carry, fade at the bench, or requisition).
- Craft-list repaint after a craft; DropMoreLoot stack-size deferral; stack rule relaxations (crafter,
  ignored tags, EpicLoot components; never quality/level/durability).

**Done when** — a craft spends pockets first, then claim storage, with a visible delivery and no
duplication; the recipe list is correct immediately after the craft.

**Risks** — consumption is the first place the law and the contract can contradict each other. The
delivery fiction must be decided before code, not during.

## 0.6 — Machines and permissions

**Scope** — processors, fermenters, cooking racks; collect finished goods; caps, pause, protect valuable
wood; refuel fires and torches; feed animals and taming creatures; four-way supply permissions; mailbox
decision recorded (keep or drop).

**Done when** — a smelter is fed and emptied unattended; caps hold; permissions are enforced per chest.

## 0.7 — Multiplayer and migration

**Scope** — shared-chest handshake and ownership revision; two-client authority tests; the chosen
migration route proven on a copied world (QM present read-only, or a one-shot converter).

**Done when** — two clients cannot duplicate or lose an item across a chest handoff; migration preserves
contents on a copied world.

## 1.0 — Parity

Stack sizes, ship unloading, comfort coverage, QM config import, and a surface-by-surface sign-off
against `PARITY.md` on a copied world.

## Cross-cutting

- **Parity harness** starts at 0.2 and gains a scenario per surface closed; it is the only proof that a
  gap is closed.
- **Benchmarks**: 200 containers at 0.3; 10 nisses at 0.7; queue catch-up at 0.1.2.
- **Every release**: changelog entry, four version fields, updated PARITY statuses, updated this file if
  scope moved.
- **Open decisions**: delivery fiction (0.5), mailbox (0.6), migration route (0.7), QM config import
  (1.0).
