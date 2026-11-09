# Parity — what "replaces Quartermaster and QI" means

**Status:** target definition, 2026-11-05. Not a promise about sequence; it is the finish line.
**Finish line:** `BootstrapBoys-Husnisse` installed alone provides every behaviour below.
`Vassteel-Quartermaster` 0.1.66 and `BootstrapBoys-Quartermaster_Integrations` 2.7.6 are
uninstalled. No shim, no pointing release, no optional companion.

Two packages are the reference, **at these versions**:
`Vassteel-Quartermaster` 0.1.66 (20 surfaces) and `Quartermaster_Integrations` 2.7.6 (18 surfaces,
5,834 lines of source). QI remains independently maintained and will gain features after 2.7.6; those
are scope decisions, not parity debt. Re-baseline this table deliberately if a later QI release adds
a surface worth replacing.

Status key: **covered** · **partial** · **gap** · **moot** (the mechanism it patched no longer
exists in Husnisse) · **decision** (needs a call, not code).

**Handover.** A surface marked *handover* means the other mod owns that function while present.
Husnisse's native implementation is the floor; it resumes from the last recorded state when the owner
disappears. UI-only surfaces are consumers, not handovers — see `README.md`, "Ownership hands over,
it does not stack".

**Settled 2026-11-05, later.** The law is now "the creature is always the visible cause": the queue may
run in the background, but the nisse is seen walking and transferring, and nothing moves while it is
unfed or sulking. Containers join a claim's network **opt-in** from a toggle in the container UI; every
container inside a claim is eligible (no whitelist), with a per-container blacklist hiding both buttons.
Rules are item categories with priority, a saved-contents list, or a special role — **Overflow** (one or
many; full categories and unmatched items) or **Deposit** (one or many; dump everything in). Teaching
writes the same list. Throughput is a hybrid — continuous drain at `ItemsPerSecond` (default 5, presets
2/5/10) with a 5 s reconciliation tick and a bounded load catch-up. Claim owner and ward-permitted
players change rules; anyone with container access reads them.

Ward handover is **per bowl**: a bowl inside a ward uses that ward's range, owner and permissions as the
claim; a bowl outside every ward keeps the native radius and owner. One bowl per ward — the second is
refused. Overlapping wards: the ward owned by the bowl's placer wins, otherwise the smallest containing
ward. A ward arriving over an existing native bowl records the native state, then takes over
immediately; a claim is refused wherever two claims would overlap, across sources. Where a ward overlaps
a native claim without containing its bowl, the ward's ground wins: the native nisse excludes containers
inside a ward it does not own. Ward handover gives the ward owner and permitted players
feed/teach/order rights.

The pickup filter is per claim, edited in the ledger. The nisse cannot be damaged and enemies ignore
it. Station coverage is the claim radius. Ship unloading covers any container on a ship inside the
claim. A save schema newer than the build understands stops rounds and leaves the data untouched.

**Coexistence with QI.** QI answers the same storage questions Husnisse does, so two providers would
double-count. When QI is loaded, Husnisse detects its GUID and does not register its contract provider;
the rest of the mod runs. Diagnostics print `provider=qi|native`. Testing can skip this path, but the
guard ships at 0.4: users will not remove QI cleanly on day one.

**The law at the contract boundary.** Read-only queries (`Count`, `CanAfford`) are exempt: they ask the
claim a question. Consumption (`TryConsume`, craft-from-claim) is the nisse's work and needs a delivery
fiction — decided in 0.4, before the first consuming surface lands.

## Quartermaster surfaces

| # | Feature | Husnisse | Status |
|---|---|---|---|
| 1 | Deposit chest auto-sort | 0.3 container network (toggle + preset/list) | **covered, different mechanic** |
| 2 | Chest remembers its item type when empty | 0.3 container rule lives in its ZDO | covered |
| 3 | Storage rules: preferred destination, overflow chest, base groups, copy settings | 0.3 container rules | **partial** — categories and saved lists land in 0.3; priority/overflow pending |
| 4 | Supply permissions per chest (crafting, production, fuel, feed) | 0.6 pantries | **partial** — one permission today, four in QM |
| 5 | Deposit All · sort inventory · sort chest · combine compatible stacks | 0.1 orders; queue in 0.1.2; merge in 0.3.1 | **partial** |
| 6 | Pickup controls, per item type | 0.3.1 type filter | **partial** — per claim, edited in the ledger |
| 7 | Configurable stack sizes (no weight change) | 1.0 | scheduled |
| 8 | Craft from storage | 0.5 | scheduled |
| 9 | Building coverage through the deposit chest's area | 1.0 comfort coverage | scheduled — coverage equals the claim radius |
| 10 | Supply processors, fermenters, cooking racks; collect finished goods | 0.6 | scheduled |
| 11 | Machine caps, pause automation, protect valuable wood | 0.6 | scheduled |
| 12 | Refuel fires and torches, feed animals and taming creatures | 0.6 | scheduled |
| 13 | Owl companion / base assistant | 0.1 nisse | replaced by the creature |
| 14 | Helmsman ship unloading | 1.0 | scheduled — any container on a ship inside the claim |
| 15 | Searchable chest settings, range previews, controller navigation | 0.2 range preview; 0.3.1 ledger | **partial** — preview lands in 0.2 |
| 16 | Mailboxes and the postal system (`Quartermaster.outgoingMail`, `PostalDirectory`) | — | **decision** — keep or drop, needed by 0.6 |
| 17 | Shared-chest handshake and ownership revision | 0.7 multiplayer | scheduled |
| 18 | Ward ownership and access filtering | 0.2 — WardIsLove handover | **scheduled** — only for a bowl inside a ward |
| 19 | Claim / revision semantics for chest ZDOs | 0.7 migration | scheduled |
| 20 | Forget / remembered / forgotten lists | 0.3.1 teaching | replaced by demonstration |

## QI surfaces

| # | Feature | Husnisse | Status |
|---|---|---|---|
| 1 | AAA Crafting: true max craft, batch cap, quantity field | 0.4 | scheduled |
| 2 | MyLittleUI: requirement rows count storage | 0.4 readouts | **partial** |
| 3 | ExtraSlots: slot protection + slot as a source | 0.4 contract source 2 | needs the 0.4 contract path |
| 4 | AdventureBackpacks: equipped container as a source | 0.4 contract source 3 | needs the 0.4 contract path |
| 5 | AAABuildMenu: pinned build-row counts | 0.4 | **gap** |
| 6 | EpicLoot: registered inventory provider, and refusing to double-register | 0.4 | **gap** |
| 7 | PlanBuild: blueprint cost counts storage | 0.4 | **gap** |
| 8 | DropMoreLoot: defer stack sizes to it when installed | 0.5 | **gap** |
| 9 | WardIsLove: ward ownership and host range | 0.2 — handover | **scheduled** — only for a bowl inside a ward |
| 10 | Chest dock: gate "Chest Config", force-sort an unowned chest | 0.3.1 ledger | **decision** — gating is moot without QM's menu; the sort-unowned half survives as a ledger order |
| 11 | Forget override (delete from both lists) | — | **moot** |
| 12 | Mail watchdog: clear stale queued-mail links | 0.6 | **gap** if mailboxes are kept |
| 13 | Pocket merge: merge stacks in the player inventory on sort | 0.3.1 | **gap** |
| 14 | Craft repaint: repaint the recipe list after a craft | 0.5 | **gap**, needed the moment 0.5 lands |
| 15 | Stack rule: ignore crafter, ignored tags, EpicLoot components | 0.5 | **gap** |
| 16 | Ward host range / ownership modes | 0.2 — handover | **scheduled** |
| 17 | Routing and stack diagnostics | 0.1 `Diagnostics` | **partial** — needs the same tags |
| 18 | Refuse to run beside EpicLootContainerBridge | 0.4 | **gap** |

## The ten gaps

1. AAABuildMenu pinned counts
2. EpicLoot inventory provider + double-register refusal
3. PlanBuild blueprint cost
4. DropMoreLoot stack deferral
5. Pocket stack merge on sort
6. Craft-list repaint after craft
7. Stack rule relaxations (crafter, tags, EpicLoot components)
8. Pickup type filter
9. Range preview for the nisse's claim
10. Mailbox parity — or an explicit decision to drop the postal system

## Three structural consequences

**1. The swap is atomic, and parity cannot be proven side by side.** Husnisse refuses to load while
`local.valheim.quartermaster` is present (`Compat/PluginGuards.cs`), because two storage systems on
the same chests lose items. That guard is right, but it means the usual "install beside, compare,
then remove" migration is unavailable. Parity has to be demonstrated on a **copied world**, twice:
once with QM+QI installed, once with Husnisse alone, against a written checklist.

**2. Migration contradicts the guard.** QM's deposit chests are its own prefabs. To convert them,
something must resolve those prefabs — which needs QM loaded. The current plan ("0.5, reads runtime
ZDO data only") works only if the ZDOs survive QM's removal, which is exactly the thing that needs
proving before anyone promises a migration. Three options, in order of preference:

- a **migration mode** on Husnisse that permits QM to be present, runs read-only, and refuses to
  start rounds until the conversion is finished;
- a **separate one-shot migration tool** that runs with QM installed, before it is removed;
- **accept the loss** and document it (fresh world, or manual rebuild of deposit chests).

**3. Sizing.** QI is 5,834 lines; Husnisse is 2,654 at 0.1.1. Porting QI's behaviour roughly
*doubles* the mod. That is the real cost of "a complete replacement for Quartermaster and QI", and
it belongs in the version ladder, not in a footnote. It also has to stay client-safe: every ported
UI integration must no-op when headless, because Husnisse is server-required and QI was not.

## Suggested placement of the gaps

| Version | Add to the ladder |
|---|---|
| 0.1.2 | flow queue and rate presets · orders on the queue · invulnerability · `husnisse_prefabs` |
| 0.1.3 | legs: walking, fade across thresholds, trip animation |
| 0.2 | provider seam · WardIsLove claim handover and last-known fallback · range preview · parity harness starts |
| 0.3 | container network core: toggle/settings, categories with priority, Overflow and Deposit, list preview, auto-suggest, copy rule, blacklist, deposit-all, LOD, fair queue, read-only badge |
| 0.3.1 | teaching · editable ledger · pickup type filter · pocket merge · YAML rules and presets |
| 0.4 | read-only contract: `Count`/`CanAfford`, sources, EpicLoot provider, AAABuildMenu, PlanBuild, MyLittleUI readouts, QI stand-down |
| 0.5 | `TryConsume`, craft-from-claim, delivery fiction, repaint, DropMoreLoot, stack rules |
| 0.6 | machines · four-way permissions · mailbox decision |
| 0.7 | multiplayer hardening · migration route |
| 1.0 | stack sizes · ship unloading · comfort coverage · QM config import · parity sign-off |

## Open decisions

- **Mailboxes.** QM ships a postal system. Dropping it silently is a feature regression; keeping it
  means owning `outgoingMail`/`PostalDirectory` semantics and a watchdog. Decide before 0.6.
- **Chest dock.** If Husnisse has no chest-config UI, the dock's gating half is moot; the
  "sort an unowned chest" half may still be wanted. Decide by 0.3.
- **Migration route.** One of the three above, chosen before 0.7.
- **Delivery fiction for consumption.** When a craft spends claim materials, what does the player see?
  The nisse carrying, a fade at the bench, or requisition into the craft. Decide in 0.5, before
  `TryConsume` ships.
- **QM's config surface.** 18,058 settings across the pack include QM's. A config import pass is the
  difference between "one launch" and "reconfigure everything by hand"; it is not in the ladder.
