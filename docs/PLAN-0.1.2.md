# 0.1.2 — implementation plan

**Goal:** replace direct bowl feeding with a real 4-slot food container, move work from rounds to a
continuous queue, and close the confirmed playtest defects.

Supersedes the 0.1.2 entry in `docs/ROADMAP.md` where they differ; `PARITY.md` still wins on finished
scope.

## Features

### Bowl as a food container

- The bowl carries a vanilla `Container` configured 4×1, so interacting opens the normal container UI.
- Only food items may enter. `Inventory.CanAddItem`/`AddItem` are guarded for the bowl's inventory;
  non-food is refused, not silently dropped.
- Contents persist in the container's ZDO `items` key.
- Feeding is no longer a direct inventory transfer. The player stocks the bowl; the nisse eats from it.
- `BowlPiece` keeps satiety/mood and the feeding policy, but no longer implements `Interactable`; the
  container owns interaction. Hover and the Alt feeding toggle are Harmony patches on `Container`.
- Drain: on the server, one food item is consumed every `Bowl Drain Minutes` (default 20, range 1–240)
  while satiety is below 100. The item's `m_food` raises satiety, mood rises slightly, and the stack is
  reduced by one. An empty bowl stops contributing; satiety decays and the existing hunger/sulk
  behaviour follows.
- Feeding policy is unchanged: server `Feeding Required`, or per-bowl opt-in.

### Flow queue

- Replace `RoundScheduler`/`RoundContext` with a per-claim queue:
  - `ReconcileInterval` (5 s) refills the queue from ground drops inside the claim.
  - A token bucket drains at `ItemsPerSecond` (default 5; presets 2/5/10), one item per ticket.
  - Catch-up: double rate for the first 60 s after load, bounded.
- Orders (Empty Pockets, Tidy) enqueue tickets instead of moving items inline.
- Nothing moves unless the claim is fed, unsuppressed and has a nisse.
- Movement in this release still warps to the target per ticket; walking is 0.1.3.

### Server authority for orders

- `ZRoutedRpc` channel `Husnisse_Order` carrying a `ZPackage`:
  - kind (Empty Pockets / Tidy)
  - claimant player id
  - bowl ZDOID
  - source container ZDOID (Tidy; `ZDOID.None` otherwise)
- Client sends the request; server validates player, claim, distance, feeding and permission, then
  enqueues. Host/single-player calls the same server method directly.
- This closes the client-side order execution demonstrated in the 2026-11-09 playtest.

### Playtest fixes

- Hotbar and equipped slots are never taken by Empty Pockets or Tidy. Hotbar is inventory row 0.
- Remove the "bowl is full enough" message path; the bowl no longer receives hand-fed items. The empty
  bowl hover and hunger lines remain.
- Invulnerability: Harmony prefix on `Character.Damage`, ignored for `NisseActor`; faction set so enemies
  do not target it. No death state.
- Tidy: instrument the `Container.Interact` prefix (hit, alt, claim, source) and fix the Alt path; the
  gesture stays Alt-interact on a chest.
- `husnisse_prefabs` command writes container prefabs to `BepInEx/BSBLogs/Husnisse-prefabs.txt`.
- `bowl_piece` diagnostics uses the correct ObjectDB lookup.

## Config changes

| Section | Key | Default | Notes |
|---|---|---|---|
| 2. Nisse | Bowl Drain Minutes | 20 | One item per interval |
| 3. Flow | Items Per Second | 5 | Presets 2/5/10 |
| 3. Flow | Reconcile Interval | 5 | Seconds |
| 3. Flow | Catch-up on Load | true | 2× rate for 60 s |

Removed: `Round Interval`, `Collect Budget`, `Sort Budget`, `Machine Budget`, `Refuel Budget`.

## Acceptance

- Placing food in the bowl works through the container UI; non-food is refused.
- One item disappears every configured interval; satiety rises; an empty bowl leads to hunger.
- Bowl contents survive relog.
- A ground drop is filed through the queue at the configured rate, not in a 30 s burst.
- Empty Pockets and Tidy enqueue and complete server-side from a client.
- Hotbar items stay in the hotbar.
- The nisse takes no damage and is ignored by enemies.
- `husnisse_status` shows queue depth, rate, bowl contents and claim source.

## Implementation status (2026-11-09)

Built as `0.1.2`; the dev DLL is `artifacts/debug/Husnisse.dll` (final build SHA-256
`e3ad7b69…`). Everything in this plan is implemented. Runtime proof is still outstanding: the rig was
stopped at the user's request, so the bowl UI, food refusal, drain interval, order RPC and
invulnerability have only been compile-checked.

Deliberate implementation notes:

- Satiety and mood are written to the ZDO in ten-second steps, not every frame, so the bowl does not
  generate a ZDO write per tick.
- Orders additionally require claim ownership (or an unowned claim), matching the native claim rule.
- The bowl container is private to its placer; ward permits arrive with the 0.2 handover.
- Sulk still moves its stack directly; routing it through the queue is left for 0.1.3.

## Out of scope

Walking (0.1.3), claim authority and WardIsLove handover (0.2), container network UI
(0.3), recognition of legacy direct-feeding state (none shipped).
