# Husnisse — finished scope handoff

**For:** the Husnisse implementation chat.
**From:** the pack-consolidation thread, 2026-11-05.
**Boundary:** the user's decision that the finished Husnisse is a complete replacement for
Quartermaster **and** Quartermaster_Integrations, and that all other consolidation ideas are
scrapped.

> **Version note (2026-11-05, later the same day).** 0.1.1 was used for the playtest fix (the bowl
> no longer demands a forge); "legs" is now **0.1.3**, after a separate 0.1.2 flow release.
> WardIsLove claim handover moved from 0.5 to **0.2** because claims are foundational; the contract
> split into read-only 0.4 and consuming 0.5; machines and migration each moved back one release.
> The ladder in §4 is updated; `docs/PARITY.md` carries the current per-surface versions and
> `docs/ROADMAP.md` the per-release acceptance criteria.

You know Husnisse. What you do not have is this thread's outcome, which changes what "finished"
means.

---

## 1. What changed

| Idea | State after 2026-11-05 |
|---|---|
| Pairwise merge study (C1–C12) | scrapped |
| Housecarl umbrella absorbing 57 QoL mods | scrapped |
| Housecarl as a **QoL mod** | survives as a name only; separate project, not a dependency, not a neighbour |
| QI gaining a licence, facade, guard, or retirement release | not part of this plan — **QI keeps shipping independently** |
| Husnisse | **the only active project**; finished scope is now QM + QI replacement |

Two consequences of leaving QI alone that shape the whole plan:

- QI's 18 surfaces get **reimplemented inside Husnisse**, not bridged to. Reading and copying our
  own code is allowed; this plan does not edit the QI repo, its release train or its docs — it is
  still developed and used independently while Husnisse is built.
- The storage contract loses its first implementer. It stays binding as a description of
  semantics, but its "QI 2.8.0 is implementation #1" framing is void. Do not edit that file — it
  lives in QI's repo. If Husnisse needs the semantics locally, copy them into `nisse/docs/` and cite
  the original.

## 2. The finish line

> `BootstrapBoys-Husnisse` installed alone provides every behaviour of `Vassteel-Quartermaster`
> 0.1.66 and `BootstrapBoys-Quartermaster_Integrations` 2.7.6. Both are uninstalled. No shim, no
> pointing release, no optional companion package.

**Parity is pinned to that snapshot, not to whatever QI ships later.** QI keeps being developed and
used in parallel, so it will move. Each new QI feature after 2.7.6 is a scope decision — include it
or record it as a deliberate exclusion — not an automatic parity obligation that reopens 1.0.

The parity matrix is `docs/PARITY.md`: 20 Quartermaster surfaces + 18 QI surfaces, each marked
covered / partial / gap / moot / decision. That file is the definition of done. A feature that is
not on it is out of scope; a gap that closes without a test is not closed.

## 3. What 1.0 must contain

Beyond the existing ladder, ten gaps are now in scope:

| Gap | Behaviour it restores | Suggested version |
|---|---|---|
| AAABuildMenu pinned counts | build rows count claim storage | 0.4 |
| EpicLoot inventory provider | enchanting can spend from the claim; refuse to double-register beside EpicLootContainerBridge | 0.4 |
| PlanBuild blueprint cost | blueprint totals count claim storage | 0.4 |
| DropMoreLoot stack deferral | do not fight DropMoreLoot's stack sizes when installed | 0.5 |
| Pocket stack merge on sort | sorting the player inventory merges identical stacks | 0.3.1 |
| Craft-list repaint after craft | recipe list does not keep a stale unaffordable tint | 0.5 |
| Stack rule relaxations | ignore crafter, ignored tags and EpicLoot components when stacking, but never quality/level/durability | 0.5 |
| Pickup type filter | per-item-type auto-collect inside a claim | 0.3.1 |
| Claim range preview | show the radius before placing / on aim | 0.2 |
| Overflow chests and base groups | routing fallback and separate bases, not one flat claim | 0.3 |

Two QI features become **moot**, not work: the Forget override exists only because Quartermaster has
a chest-config UI, and the dock's gating half has nothing to gate. The dock's "sort a chest you do not
own" half survives: the ledger is editable, so it can carry that order. Teaching by demonstration
remains the default path; the ledger is the precise one.

## 4. Resizing the ladder

The ladder was written for a storage mod. It is now a storage **and integration** mod:

| Version | Was | Now also carries |
|---|---|---|
| 0.1 | bowl, claim, memory, sort rounds, orders | unchanged — still the go/no-go on the fiction |
| 0.1.1 | — | *shipped*: playtest fix, the bowl no longer demands a forge |
| 0.1.2 | legs | **flow and safety**: continuous queue at `ItemsPerSecond` (2/5/10), orders on the queue, invulnerability, `husnisse_prefabs` dump |
| 0.1.3 | — | **legs**: walking, fade across thresholds, trip animation |
| 0.2 | teaching, ledger, chest roles | **claim ownership: provider seam, WardIsLove handover, last-known fallback**, range preview; parity harness starts |
| 0.3 | pantries, craft-from-claim | **network core**: toggle/settings UI, categories with priority, Overflow and Deposit, list preview, auto-suggest, copy rule, blacklist, deposit-all, LOD, fair queue, read-only badge |
| 0.3.1 | — | **teaching and ledger**: teaching writes lists, editable ledger, pickup filter, pocket merge, YAML rules and presets |
| 0.4 | machines | **read-only contract**: `Count`/`CanAfford`, sources, EpicLoot/AAA/PlanBuild/MLUI readouts, QI stand-down |
| 0.5 | — | **craft from claim**: `TryConsume`, delivery fiction, repaint, DropMoreLoot, stack rules |
| 0.6 | — | machines, four-way permissions, mailbox decision |
| 0.7 | — | multiplayer hardening, migration route |
| 1.0 | stack sizes, ship unload, comfort coverage | QM config import; final parity sign-off against `PARITY.md` |

Sizing reality: QI is **5,834 lines** across 18 surfaces; Husnisse is **2,654** at 0.1.1. The port
roughly doubles the mod. Plan for it as a module, not as a handful of patches.

## 5. Non-negotiables

- **The law.** The creature is always the visible cause. Nothing moves while the nisse is unfed, asleep
  or sulking, and every move is seen — walking, transferring, fading across thresholds — but the work
  itself may be simulated in a queue. Read-only contract queries (`Count`, `CanAfford`) are exempt:
  they ask the claim a question and move nothing. Consumption (`TryConsume`) is not exempt and needs a
  delivery fiction before 0.5. The one documented exception is the pockets-first spend order.
- **Ownership hands over, it does not stack.** Every function has a native implementation; an
  installed mod that owns a function replaces it, and the two never run together. For claims this is
  decided per bowl: only a bowl standing inside a WardIsLove ward uses the ward as its claim; a bowl
  outside every ward keeps the native radius and owner. The native path resumes from the last
  recorded state when the owner disappears. Optional integrations are declared soft dependencies for
  load order. See `README.md`, "Ownership hands over, it does not stack".
- **QI coexistence stays guarded.** Even though testing runs without QI, when QI is loaded Husnisse
  does not register its contract provider, so two storage answers never coexist. Diagnostics print
  `provider=qi|native`.
- **Standalone posture.** Jotunn + ConditionalConfigSync + YamlDotNet (and Newtonsoft from the game
  folder). No dependency on Housecarl, QI, or the server pack. The mod must load into a vanilla+
  profile.
- **Clean-room.** No Quartermaster code, decompiled output, ZDO keys or config key names. Classes
  must not mirror its shape. `CLEANROOM.md` is binding.
- **The refusal guard stays.** Husnisse must not load beside Quartermaster. That is what stops two
  storage systems from eating the same chests.
- **Headless no-op.** Husnisse is server-required; every ported UI integration must do nothing when
  there is no local player. Nine client patches running on a dedicated server is the failure mode to
  design out.
- **Policy is world state.** `Feeding Required` stays a global key; per-bowl choice stays in the
  bowl's ZDO. A client-local policy value that disagrees with the simulation is a bug.
- **Diagnostics from day one.** Each module gets its debug tag and, if it holds state, a line in
  `Diagnostics.Lines()`. A playtest you cannot see is a vibe.
- **One concern per file.** The module list is a list of files, not one large plugin class.

## 6. Open decisions (settle before the version that needs them)

| Decision | Needed by | Options |
|---|---|---|
| **Mailboxes** | 0.6 | Keep QM's postal system (own `outgoingMail`/`PostalDirectory` semantics + watchdog), or drop it explicitly and record the regression |
| **Migration route** | 0.7 | (a) migration mode that permits Quartermaster present, read-only, rounds disabled; (b) separate one-shot tool run before removal; (c) accept the loss and document a fresh world |
| **Chest dock** | 0.3.1 | Drop with the chest UI, or keep the "sort an unowned chest" half |
| **QM config import** | 1.0 | Copy matching values from QM's `.cfg` on first run, or make users reconfigure by hand |

Migration is the sharp one. QM's deposit chests are its own prefabs; converting them needs those
prefabs to resolve, which needs QM loaded — which the guard forbids. Nothing in the current plan
proves the ZDOs survive the mod's removal. Do not promise a migration until one of the three routes
is chosen and tested on a copied world.

## 7. What not to do

- **Do not change QI for this plan.** QI stays a live, independently maintained package until
  Husnisse is finished — it is still developed, released and used meanwhile. No edits, licence pass,
  facade or deprecation notice are part of this work. Its behaviour is reimplemented here, and the
  package leaves *this pack* at parity.
- **Do not copy Quartermaster code, decompiled output, ZDO keys or config names.** Behaviour only.
- **Do not build a craft UI.** AAA Crafting owns it, MyLittleUI yields to it automatically. Husnisse
  asks the contract question; it does not draw the panel.
- **Do not absorb content mods.** Prefabs and art stay separate; removing them deletes built objects.
- **Do not add a third transpiler to `Player.AutoPickup`.** Hook ZenPlayer's `CanIntercept` when that
  integration lands.
- **Do not quote 57/58/64 absorbed mods.** The umbrella is scrapped; those numbers describe a plan
  that no longer exists. `quartermaster_integrations/docs/HOUSECARL-HANDOFF-2026-11-04.md` is a
  historical document from that plan — read it for QI's surface list, not for scope.
- **Do not treat `docs/COMPATIBILITY.md` as the scope.** It describes what a correct container is,
  not what must be replaced.

## 8. Order of work

1. **0.1 playtest.** Unchanged and still first — it is the go/no-go on the fiction, and nothing in
   this handoff makes sense if feeding a spirit to sort your base is not fun.
2. **0.1.2 flow and safety**: queue, rate presets, orders on the queue, invulnerability, prefab dump.
3. **0.1.3 legs**: walking, fade across thresholds, trip animation.
4. **0.2 claim ownership**: provider seam, WardIsLove full handover with last-known fallback, range
   preview. The parity harness starts here.
5. **0.3 network core**: container UI, categories with priority, Overflow and Deposit, previews, copy
   rule, blacklist, deposit-all, LOD, fair queue, badge.
6. **0.3.1 teaching and ledger**: teaching, ledger, pickup filter, pocket merge, YAML rules.
7. **0.4 read-only contract**: counts, sources, providers, readouts, QI stand-down.
8. **0.5 craft from claim**: `TryConsume`, delivery fiction, repaint, stack rules.
9. **0.6 machines, permissions and the mailbox decision.**
10. **0.7 multiplayer and the migration route.**
11. **1.0 parity sign-off against `docs/PARITY.md`, surface by surface, on a copied world.**

## 9. Still unverified

- All of 0.1 in play — the only release that exists, and the playtest is unrun.
- Doorways are not navmesh-passable (measured: `GetPath` stops 5.42 m short at a 2 m gap). 0.1
  teleports; 0.1.1 fades across thresholds.
- Whether QM's container ZDOs survive Quartermaster's removal — the block on any migration promise.
- QI's 18 surfaces have README/source-level analysis only; no runtime parity has been measured, and
  five of them (AAABuildMenu, EpicLoot, PlanBuild, DropMoreLoot, pocket merge) have never been
  exercised outside the pack.
- The 10 gaps are scoped, not estimated. Do not put a date on 1.0 without sizing 0.3 first.

---

**Read next:** `docs/PARITY.md` (definition of done), `docs/ROADMAP.md` (sequence and acceptance
criteria), `README.md` (design law and ladder), `CLEANROOM.md` (provenance), `docs/PLAYTEST.md` (0.1
checklist).
