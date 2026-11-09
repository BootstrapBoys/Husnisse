# Code review and refactor plan

**Reviewed:** 0.1.1, 33 files, 2,654 lines. **Against:** the 2026-11-05 scope and roadmap.

Verdict: 0.1 is a coherent prototype and roughly two-thirds of it survives, but three systems are the
wrong shape for the new design (scheduler, router, claims), one layer is missing entirely (server RPC),
and several behaviours now contradict the visible-cause law. This is a refactor of foundations, not a
polish pass.

## Findings

### High

| # | Finding | Evidence | Consequence
|---|---|---|---|
| H1 | **No server-authority layer.** Orders, feeding and the Tidy patch execute wherever the interaction runs. `OrderService` moves items with `Inventory.AddItem/RemoveItem` inline; `BowlPiece.Interact` writes the ZDO directly; `ContainerTidy` runs as a client-side prefix. | `OrderService.cs:38-54`, `BowlPiece.cs:143-170`, `ContainerTidy.cs:16-29` | In multiplayer, a client can execute moves the server does not arbitrate: desync, ghost items, or a rejected write. 0.1 was only ever tested headless. Needs an RPC layer before any client ships. |
| H2 | **Scheduler is interval + budgets**, not a queue at a rate. | `RoundScheduler.cs:63-70`, `RoundContext.cs` | The new model (5 items/s, 5 s reconcile, bounded catch-up) cannot be expressed by scaling budgets. Replace both files. |
| H3 | **Router has no rules.** It picks remembered chest → chest already holding the item → any chest with room. | `StorageRouter.cs:13-42` | No categories, priority, Overflow, Deposit, unmatched handling, network membership or blacklist. Needs a new rule model and resolver. |
| H4 | **Claims are native-only.** `Claim` holds radius/owner; `WardIsLoveCompat` only gates access; suppression is computed once in `Awake` and never re-evaluated. | `Claim.cs:11-18`, `BowlPiece.cs:104-127` | No per-bowl ward handover, no snapshot, no live switch, no one-bowl-per-ward, no overlap resolution. The 0.2 foundation is absent. |
| H5 | **The nisse is killable.** `Character` has no invulnerability flag; the clone keeps health 100 and `Faction.PlayerSpawned`. | `NissePrefab.cs:51-55`; `Character.Damage(HitData)` exists, no `m_invulnerable` | A raid can kill it, contradicting the settled rule. Needs a `Character.Damage` prefix plus a faction test (`TrainingDummy` is the candidate). |
| H6 | **Container discovery is a hard whitelist**, the opposite of the new opt-in network. | `ContainerFilter.cs:109-148`, `ContainerIndex.cs:16-27` | New rule is: every container inside a claim is eligible, off by default, opt-in via ZDO, blacklist hides it. The filter becomes a blacklist policy, not a gate. |

### Medium

| # | Finding | Evidence | Consequence |
|---|---|---|---|
| M1 | **Every lookup rescans all containers.** `InClaim`, `ById`, diagnostics, sulk and storage scope each call `FindObjectsByType<Container>`. | `ContainerIndex.cs:16-39` | 200-container base is O(n) per call and O(n²) for routed moves. Needs a dirty registry with a per-claim cache. |
| M2 | **Orders move items instantly and ignore the queue.** | `OrderService.cs:38-54` | Contradicts "orders hand off to the nisse"; must enqueue move tickets. |
| M3 | **Sulking moves instantly, bypasses the queue, and hardcodes English.** The literal line is not in `Loc`. | `Sulking.cs:52-56` | Must be a queued, visible misfile and a localised line. |
| M4 | **Memory writes one ZDO key per item name** and never validates that the remembered container still exists in the network. | `ItemMemory.cs:26-30`, `StorageRouter.cs:17-23` | Key bloat in large bases; stale ZDOIDs silently ignored. Needs validation and a compact store (one JSON/CSV key) when item counts grow. |
| M5 | **Diagnostics predate the new model.** No queue depth, rate, provider, claim source or network counts; prints `interval` and budgets. | `Diagnostics.cs:19-34` | Cannot diagnose the flow or authority model. |
| M6 | **Schema has no refusal gate.** `Read()` reports a version, but nothing stops work on an unknown newer schema. | `SaveSchema.cs:21-30` | The settled rule is refuse, log, stop the queue, leave data untouched. |
| M7 | **Config keys are the old model**: `Round Interval`, four budgets, `Skip Carts/Ships`, `Skip Display Piles`. | `HusnisseConfig.cs:20-32,61-92` | Sections and keys need rewriting to Flow/Containers; blacklist defaults replace skip flags. |
| M8 | **New interaction surfaces have no strings.** No keys for network toggle, settings, categories, overflow, deposit, deposit-all, rate. | `Loc.cs` | Every new UI string needs a key and English default. |
| M9 | **Bowl init uses a literal key** (`"husnisse_bowl_init"`) and owner checks assume the placer is the only actor. | `BowlPiece.cs:90-98,269-275` | Move to `ZdoKeys`; owner semantics must follow claim authority (ward owner + permits). |
| M10 | **`WarpTo` sets the transform directly** for every collected item. | `NisseActor.cs:29-33`, `CollectDropsTask.cs:41` | Teleport per item is both the old law and unverified for client replication; replace with trip batches. |
| M11 | **No `husnisse_prefabs` command.** | `HusnissePlugin.cs:65-78` | Accepted improvement #7 is absent. |
| M12 | **`ContainerTidy` Alt-interact path will conflict with the new container UI** if it stays client-local. | `ContainerTidy.cs` | Keep the gesture, but route it through the same RPC as the UI button. |

### Low

- Redundant `using Husnisse.Core;` inside `namespace Husnisse.Core` (`Diagnostics.cs:3`).
- `HusnissePlugin.Running` is never set false on quit.
- `NisseFactory` resolves `ZNetScene.CreateObject` by reflection on every spawn instead of caching.
- `Loc.BowlHoverNoFeed` and `Loc.BowlContent` are defined but unused.
- `SaveSchema.Publish` is called from `WorldPolicy` (works, but the coupling is surprising).

## Target structure

```
src/
  Flow/            FlowQueue, FlowScheduler, MoveTicket, FlowContext, TripRunner
  Containers/      NetworkRegistry, ContainerRule, RuleCatalog, RuleResolver,
                   Blacklist, ContainerScanner
  Claims/          Authority/IClaimAuthority, NativeAuthority, WardAuthority,
                   ClaimResolver, ClaimSnapshot
  Net/             Rpc (Jotunn CustomRPC), requests and server handlers
  UI/              ContainerPanel (0.3), Ledger (0.3.1), RangePreview (0.2)
  Bowl/ Creature/ Config/ Core/ Memory/ Localization/ Util/ Patches/   (adapted)
```

## File disposition

| File | Verdict | Action |
|---|---|---|
| `HusnissePlugin.cs` | extend | RPC registration, prefab command, schema gate, soft-dependency attributes |
| `Config/HusnisseConfig.cs` | refactor | sections `3. Flow`, `4. Containers`; `ItemsPerSecond`, `ReconcileInterval`, rate preset; drop budgets/round interval |
| `Config/WorldPolicy.cs` | keep | — |
| `Config/SaveSchema.cs` | extend | `IsCompatible`, version 2 when authority lands |
| `Core/Log.cs`, `Core/DiagnosticsFile.cs` | keep | — |
| `Core/Diagnostics.cs` | extend | queue depth, rate, provider, claim source, network/blacklist counts |
| `Claim/Claim.cs` | extend | authority id, effective range/owner, snapshot fields |
| `Claim/ClaimRegistry.cs` | extend | resolver results, overlap across sources, recompute on ward events |
| `Compat/WardIsLoveCompat.cs` | extend | enumerate all containing wards with radius/owner; `CheckAccess` for permits |
| `Compat/ContainerFilter.cs` | replace | blacklist policy + backpack-proxy exclusion; no prefab whitelist |
| `Compat/PluginGuards.cs` | extend | QI presence check for provider stand-down (0.4) |
| `Bowl/BowlPiece.cs` | refactor | authority resolution, RPC feeding, owner rules, snapshot |
| `Bowl/BowlPrefab.cs` | keep | — |
| `Creature/NisseActor.cs` | extend | damage immunity, trip movement, idle LOD |
| `Creature/NissePrefab.cs` | extend | invulnerability/faction, LOD registration |
| `Creature/NisseFactory.cs` | adapt | cache reflection, bind to authority |
| `Creature/NisseHands.cs` | refactor | RPC order request |
| `Creature/NisseVoice.cs` | keep | add keys only |
| `Orders/OrderService.cs` | replace | `QueueRequest` + server handler |
| `Patches/PlacementGuard.cs` | extend | effective claim radius, ward claims, one bowl per ward |
| `Patches/ContainerTidy.cs` | adapt | RPC path; keep gesture |
| `Rounds/RoundScheduler.cs` | replace | `FlowScheduler` |
| `Rounds/RoundContext.cs` | replace | `FlowContext` |
| `Rounds/Sulking.cs` | adapt | queued misfile, localised line, permission check |
| `Storage/ContainerIndex.cs` | rewrite | `NetworkRegistry` + `ContainerScanner` with cache |
| `Storage/StorageRouter.cs` | rewrite | `RuleResolver` |
| `Storage/StorageScope.cs` | extend | network membership; `TryConsume` in 0.5 |
| `Tasks/IRoundTask.cs` | replace | `IFlowProducer` |
| `Tasks/CollectDropsTask.cs` | rewrite | producer only; enqueues tickets |
| `Memory/ItemMemory.cs` | adapt | validate ZDOID; compact store later |
| `Util/ZdoKeys.cs` | extend | network, rule, authority, snapshot |
| `Localization/Loc.cs` | extend | new UI and message keys |

## Data model (new ZDO keys)

Bowl/claim:
- `husnisse_claim_authority` — `native` or `ward`
- `husnisse_claim_ward` — ward ZDOID (0 when native)
- `husnisse_claim_snapshot_radius`, `_owner` — last native/ward state for fallback

Container:
- `husnisse_net` — bool, opted in
- `husnisse_rule_kind` — `none`, `category`, `custom`, `overflow`, `deposit`
- `husnisse_rule_categories` — compact list of item-type ids (or category ids)
- `husnisse_rule_items` — compact list of shared names for custom lists (cap plus overflow)
- `husnisse_rule_priority` — int, lower fills first
- `husnisse_blocked` — bool, per-container blacklist

Global:
- `husnisse_schema_2` when authority lands.

Rule encoding must be measured against ZDO string limits before implementation; if lists grow, move to one
JSON blob (Newtonsoft is already in the game folder).

## Phases

### Phase 0 — spikes (before 0.1.2, one day each)

1. **Authority context.** Determine where `Container.Interact`, `BowlPiece.Interact`, `NisseHands.Interact`
   and `Player.TryPlacePiece` run (client, server, both) and stand up one Jotunn `CustomRPC` round trip.
   This decides the whole Net layer.
2. **Invulnerability.** `Character.Damage` prefix for `NisseActor` + faction candidate (`TrainingDummy`)
   under a wolf/raid. Confirm enemies ignore it.
3. **Ward enumeration.** List every ward containing a point with radius, owner, and `CheckAccess` for a
   given player; confirm a live switch when a ward is placed or removed (poll or event).
4. **Container UI anchor.** Confirm a top-right button can be added to `InventoryGui` without breaking
   MyLittleUI or controller navigation.
5. **Rule encoding.** Round-trip a 40-item custom list through ZDO and measure the key budget.

### Phase 1 — 0.1.2 flow and safety

- Replace `RoundScheduler`/`RoundContext` with `FlowQueue` + `FlowScheduler` (drain at rate, 5 s
  reconcile, bounded catch-up).
- `OrderService` becomes `QueueRequest`: Empty Pockets and Tidy enqueue tickets; server executes.
- Invulnerability via Damage prefix; faction set; no death state.
- `husnisse_prefabs` command.
- Config: `3. Flow` with `ItemsPerSecond` and rate preset; remove old budgets.
- Diagnostics: queue depth, rate, skipped reasons.
- Fix sulk localization and route sulk through the queue.
- Schema gate: unknown newer schema stops the queue.

**Done when** a 200-item drop drains at the configured rate, orders visibly enqueue, and hitting the
nisse does nothing.

### Phase 2 — 0.1.3 legs

- `TripRunner`: request path, walk, transfer, fade across thresholds; one trip per batch.
- Replace `WarpTo` per item; keep a fade teleport as the failure path.
- Retry policy: one retry, then fade and report.
- Basic distance culling for trips (full LOD in 0.3).

### Phase 3 — 0.2 claim ownership

- `IClaimAuthority` + `NativeAuthority` + `WardAuthority`; `ClaimResolver` implements the per-bowl rules
  (outside ward native, inside ward ward; placer-owned ward wins; else smallest; one bowl per ward).
- Snapshot on handover; live recompute on ward place/remove and bowl move.
- Placement guard uses effective radius and checks both sources.
- Range preview.
- Harness scenarios listed in ROADMAP.

### Phase 4 — 0.3 network core

- `NetworkRegistry` + `ContainerScanner` cache; opt-in ZDO flag; blacklist.
- `ContainerRule` + `RuleCatalog` (survival presets + item types) + `RuleResolver` with priority,
  Overflow, Deposit, unmatched.
- Container UI patch: toggle + settings, category picker, saved-contents preview, auto-suggest, copy rule.
- Deposit-all and deposit chest semantics.
- Fair queue, animation LOD, read-only badge.
- 200-container benchmark.

### Phase 5 — 0.3.1 teaching and ledger

- Teaching writes the same rule as the UI.
- Ledger edits; pickup filter; pocket merge; YAML export/import and editable presets.

### Phase 6 — contract (0.4 / 0.5)

- `StorageScope` becomes network-aware and implements `Count`/`CanAfford`; provider registration with
  QI stand-down; then `TryConsume` and the delivery fiction.

## Tests to add with the refactor

- Harness: queue drains N items at rate; order enqueues and completes; no instant moves.
- Harness: nisse takes zero damage from `HitData`; raid ignores it.
- Harness: cart/ship/display pile blacklisted by default; toggled container participates.
- Harness: category → priority → overflow → deposit → unmatched routing table.
- Client probe: order RPC round trip; container toggle round trip; trip visible to a client.
- Benchmark: 200 containers, 10 nisses.

## Risks

- **Phase 0 can invalidate assumptions.** If container interactions are already server-authoritative,
  part of the Net layer is unnecessary; if not, it is larger than estimated.
- **WardIsLove has no public permit list.** Displaying "who is permitted" may require `CheckAccess` per
  known player, not a list read; the UI must not promise more than that.
- **ZDO string limits** for custom lists are unmeasured; the JSON fallback is a design change, not a
  tweak.
- **UI patching** is the most compatibility-sensitive work in the project; MyLittleUI and controller
  navigation must be tested in 0.3, not after.
