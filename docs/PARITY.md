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
claim a question. Consumption (`TryConsume`, craft-from-claim) is the nisse's work: **0.5 ships it as a
fetch before the pay.** A craft the pockets cannot cover sends the nisse to the claim's chests, it takes
the missing stacks while it stands at each one and puts them in the player's hands, and the game's own
craft then spends the pockets. `TryConsume` never returns `true` for an amount it has not debited: the
claim leg it cannot perform synchronously is started as that errand, and the answer is `false` until the
load is held. EpicLoot and PlanBuild take their costs through the same fetch rather than through a
provider that counts a chest it cannot take from.

## Quartermaster surfaces

| # | Feature | Husnisse | Status |
|---|---|---|---|
| 1 | Deposit chest auto-sort | 0.3 container network (toggle + preset/list) | **covered, different mechanic** — a Deposit chest is a designated source: the nisse carries its contents to the chests whose rules claim them (0.3c) |
| 2 | Chest remembers its item type when empty | 0.3 container rule lives in its ZDO | covered |
| 3 | Storage rules: preferred destination, overflow chest, base groups, copy settings | 0.3 container rules | **covered** — 0.3a shipped the container UI, the eight categories and the roles in the chest's ZDO; 0.3b added the preset table with priority, the saved list with its checkbox preview, the first-toggle suggestion and copy-rule; 0.3c added Overflow's "full category" case with a named reason and deposit-as-a-source. Base groups are the claim |
| 4 | Supply permissions per chest (crafting, production, fuel, feed) | 0.6 four-way permission | **covered** — every chest carries four supply purposes, all on by default; a craft, a processor, a fire and an animal each take only from chests that permit their purpose. Set from the container window's four toggles, `husnisse_rule supply`, or the rules file; a chest that may not supply a craft is not counted by the contract either |
| 5 | Deposit All · sort inventory · sort chest · combine compatible stacks | 0.1 orders; queue in 0.1.2; 0.3c deposit-all; merge in 0.3.1 | **covered** — deposit-all (network: the nisse files your pockets; off-network: dump into that chest, unsorted), Empty Pockets and Tidy are in; 0.3.1 merges partial pocket stacks before an Empty Pockets order queues | 
| 6 | Pickup controls, per item type | 0.3.1 type filter | **covered** — per claim, one toggle per preset in the ledger; the ground scan honours it, orders do not |
| 7 | Configurable stack sizes (no weight change) | 1.0 | scheduled |
| 8 | Craft from storage | 0.5 | **covered** — a recipe the pockets cannot pay for becomes a visible supply errand: the nisse takes the missing materials from the claim's chests, carries them to the bench and puts them in the player's hands, and the game's own craft spends them from the pockets. AAA batches fetch for the whole batch and the deferred craft keeps its size |
| 9 | Building coverage through the deposit chest's area | 1.0 comfort coverage | scheduled — coverage equals the claim radius |
| 10 | Supply processors, fermenters, cooking racks; collect finished goods | 0.6 machines | **covered** — a smelter, blast furnace, charcoal kiln, eitr refinery, windmill, fermenter, cooking station, oven, beehive and sap extractor are fed their own fuel and input through their own interact RPC, and emptied the same way. Output is a real drop the nisse carries to the chest whose rule claims it. Caps come from each prefab's maximum cut by `7. Machines` percentages |
| 11 | Machine caps, pause automation, protect valuable wood | 0.6 machines | **covered** — `Fuel Cap Percent` / `Input Cap Percent` are ceilings the nisse never fills past; the ledger's Pause machines switch (and `husnisse_machines pause`) stops automation per claim or per machine; `Protect Valuable Wood` refuses to burn fine wood, core wood, elder bark, ancient bark or Yggdrasil wood |
| 12 | Refuel fires and torches, feed animals and taming creatures | 0.6 home | **covered** — fire pits, hearths, bonfires and standing torches are kept at their fuel cap; a hungry tameable gets a portion of a food it eats, set down beside it and reserved from the claim's own pickup pass. The creature eating it is a client check (a spawned creature on a zone-less server does not walk its consume path) |
| 13 | Owl companion / base assistant | 0.1 nisse | replaced by the creature |
| 14 | Helmsman ship unloading | 1.0 | scheduled — any container on a ship inside the claim |
| 15 | Searchable chest settings, range previews, controller navigation | 0.2 range preview; 0.3.1 ledger | **partial** — the ledger lists every container and shows the ring; search and controller navigation are not built |
| 16 | Mailboxes and the postal system (`Quartermaster.outgoingMail`, `PostalDirectory`) | — | **moot (dropped 2026-11-13)** — the mailbox is a courier network, not a house spirit: it is out of scope and its postal keys are never read |
| 17 | Shared-chest handshake and ownership revision | 0.7 — `Storage/ChestHandover.cs`, `Net/ChestRpc.cs`, `Patches/StaleChest.cs` | **covered on the server, partial on the client** — the server asks the owning peer, never takes an open chest, verifies ownership before writing and re-reads the ZDO first; the client answers and hands the ZDO over in code, but that leg was not driven end to end on the client rig (`docs/evidence/0.7/RESULT.md`) |
| 18 | Ward ownership and access filtering | 0.2 — WardIsLove handover | **scheduled** — only for a bowl inside a ward |
| 19 | Claim / revision semantics for chest ZDOs | 0.7 — `Storage/ContainerWrite.cs` (`Acquire` → `Sync` → `Persist`), `Migration/*` | **covered** — a chest is read from its ZDO before every move and written back with a read-back; a claim is verified before use; the migration reads and writes the same ZDOs without Quartermaster, including chests whose piece is not loaded (`docs/evidence/0.7/RESULT.md`) |
| 20 | Forget / remembered / forgotten lists | 0.3.1 teaching | **covered, different mechanic** — demonstration replaces Forget: teaching a chest again rewrites its list, and the ledger's No-rule button forgets a container outright |

## QI surfaces

| # | Feature | Husnisse | Status |
|---|---|---|---|
| 1 | AAA Crafting: true max craft, batch cap, quantity field | 0.4 — claim counts in AAA's rows and batch ceiling | **covered for the numbers** — `GetItemsInCloseContainers` and `CalculateMaxCraftAmount` read the claim; the craft itself still spends pockets until 0.5 |
| 2 | MyLittleUI: requirement rows count storage | 0.4 readouts | **covered** — MyLittleUI's own pocket-only number is off for the session and Husnisse writes the row, so one number, not two |
| 3 | ExtraSlots: slot protection + slot as a source | 0.4 contract source 2 | **covered** — extra rows are source 2 and are partitioned out of the pocket count; protection is moot because the only thing that moves pocket items is an Empty Pockets order, which already skips the hotbar and equipped rows |
| 4 | AdventureBackpacks: equipped container as a source | 0.4 contract source 3 | **covered** — `ABAPI.TryGetEquippedBackpackInventory`, counted beside the pockets and never filed into |
| 5 | AAABuildMenu: pinned build-row counts | 0.4 | **covered** — `PinnedRecipeTracker.CountItem` postfix adds the claim to the player's own row |
| 6 | EpicLoot: registered inventory provider, and refusing to double-register | 0.5 | **covered, different mechanic** — no provider is registered, deliberately: EpicLoot pays through the same list it counts and ignores the remove return, so a provider counting the claim would make an enchant look affordable and leave the cost unpaid. Its affordability check (`LocalPlayerCanAffordCost`, which every cost path goes through) starts the fetch instead, and EpicLoot pays from the pockets once the load lands. The double-register refusal stays live for the day a provider could be honoured |
| 7 | PlanBuild: blueprint cost counts storage | 0.5 | **covered, different mechanic** — same reasoning: `GetInventories` feeds a colour and a `void` take, so no claim entry is added. `PlanPiece.GetInventories` is hooked to start the fetch for a plan's missing materials, and the plan's own clicks pay into it from the pockets |
| 8 | DropMoreLoot: defer stack sizes to it when installed | 0.5 | **covered by construction** — Husnisse never writes a stack size, so there is nothing to disable; the diagnostics report `stacks=dropmoreloot xN` when it owns them |
| 9 | WardIsLove: ward ownership and host range | 0.2 — handover | **scheduled** — only for a bowl inside a ward |
| 10 | Chest dock: gate "Chest Config", force-sort an unowned chest | 0.3.1 ledger | **covered** — gating is moot without QM's menu; the sort-unowned half is the ledger's **Tidy** row action |
| 11 | Forget override (delete from both lists) | — | **moot** |
| 12 | Mail watchdog: clear stale queued-mail links | — | **moot** — mailboxes are dropped |
| 13 | Pocket merge: merge stacks in the player inventory on sort | 0.3.1 | **covered** — `OrderService.MergePockets`, run by an Empty Pockets order before it queues |
| 14 | Craft repaint: repaint the recipe list after a craft | 0.5 | **covered** — `CraftRepaint` marks the list stale after `DoCrafting` and rebuilds it on the next idle frame; needed because a craft from the claim empties a chest and the pockets in one action |
| 15 | Stack rule: ignore crafter, ignored tags, EpicLoot components | 0.5 | **covered by construction** — stack identity is vanilla's (shared data, quality, world level), plus variant and durability in the mod's own pocket merge. The crafter and EpicLoot's material stamps are not part of it, so nothing has to be relaxed; quality, world level and durability are never ignored. There is no config because there is no stricter rule to relax |
| 16 | Ward host range / ownership modes | 0.2 — handover | **scheduled** |
| 17 | Routing and stack diagnostics | 0.1 `Diagnostics` | **partial** — needs the same tags |
| 18 | Refuse to run beside EpicLootContainerBridge | 0.4 | **covered** — logged as a refusal with the reason; there is no provider yet to double-register, and the guard is what keeps one from being added beside the bridge |

## The ten gaps

1. ~~AAABuildMenu pinned counts~~ — **closed in 0.4**
2. ~~EpicLoot inventory provider~~ — **closed in 0.5, as a fetch instead of a provider**: the cost
   question starts the nisse's errand and EpicLoot pays from the pockets. A provider would have
   advertised a claim it could not debit.
3. ~~PlanBuild blueprint cost~~ — **closed in 0.5, as a fetch instead of a list entry**: `GetInventories`
   starts the errand; the plan's clicks pay from the pockets.
4. ~~DropMoreLoot stack deferral~~ — **closed in 0.5 by construction**: no stack-size writes to defer.
5. ~~Pocket stack merge on sort~~ — **closed in 0.3.1**
6. ~~Craft-list repaint after craft~~ — **closed in 0.5**
7. ~~Stack rule relaxations (crafter, tags, EpicLoot components)~~ — **closed in 0.5**: stack identity is
   vanilla's relaxed rule already; the stricter one it relaxed never existed here.
8. ~~Pickup type filter~~ — **closed in 0.3.1**
9. ~~Range preview for the nisse's claim~~ — **closed in 0.2b** (placement ghost, bowl hover, and the ledger's Show range)
10. Mailbox parity — or an explicit decision to drop the postal system (0.6: **dropped**, 2026-11-13)

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
| 0.1.2 | four-slot food pantry bowl with drain rate · flow queue and rate presets · server orders · invulnerability · `husnisse_prefabs` |
| 0.1.3 | legs: walking, fade across thresholds, trip animation |
| 0.2 | provider seam · WardIsLove claim handover and last-known fallback · range preview · parity harness starts |
| 0.3 | container network core: toggle/settings, categories with priority, Overflow and Deposit, list preview, auto-suggest, copy rule, blacklist, deposit-all, LOD, fair queue, read-only badge |
| 0.3.1 | teaching · editable ledger · pickup type filter · pocket merge · YAML rules and presets |
| 0.4 | read-only contract: `Count`/`CanAfford`, sources, EpicLoot provider, AAABuildMenu, PlanBuild, MyLittleUI readouts, QI stand-down |
| 0.5 | `TryConsume`, craft-from-claim, delivery fiction, repaint, DropMoreLoot, stack rules |
| 0.6 | machines · four-way permissions · mailbox decision (dropped) |
| 0.7 | multiplayer hardening · migration route |
| 1.0 | stack sizes · ship unloading · comfort coverage · QM config import · parity sign-off |

## Open decisions

- **Mailboxes.** **Dropped, 2026-11-13.** QM ships a postal system; a courier network is a different
  mod, and owning `outgoingMail`/`PostalDirectory` semantics (and a watchdog for its stale links) buys
  nothing for a house spirit. Nothing reads or writes those keys, so a world that had mail keeps its
  data untouched and simply has no postal service.
- **Chest dock.** If Husnisse has no chest-config UI, the dock's gating half is moot; the
  "sort an unowned chest" half may still be wanted. Decide by 0.3.
- **Migration route.** One of the three above, chosen before 0.7.
- **Delivery fiction for consumption.** When a craft spends claim materials, what does the player see?
  The nisse carrying, a fade at the bench, or requisition into the craft. Decide in 0.5, before
  `TryConsume` ships.
- **QM's config surface.** 18,058 settings across the pack include QM's. A config import pass is the
  difference between "one launch" and "reconfigure everything by hand"; it is not in the ladder.
