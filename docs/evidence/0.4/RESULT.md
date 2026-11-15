# 0.4 — the read-only storage contract, on a headless server and a real client

**Build:** Husnisse 0.4.0, `Husnisse.dll` sha256 `269c6f1509b6…` (branch `husnisse/0.4-readonly`, based on
`b7cf1f3`). Probe `HusnisseGateClientProbe.dll` sha256 `4fb5ba7b9ff2…`; harness `TestHarness.dll`
sha256 `b9ecd880f4ff…` (shared testrig + `nostruct`, `husnisse contract`).

**Where:** dedicated tree `V:\Gate031`, port 2956, world `MTTest` (fresh: the previous world is kept as
`MTTest.pre-04client-*`). Client `DESKTOP-PAOVLP7`, character `Stranger` `-202584743`, joined with the
consumer pack installed on both sides: AAA Crafting `AzuAntiArthriticCrafting.dll`, `AAABuildMenu.dll`,
`MyLittleUI.dll`, `ExtraSlots.dll`, `AdventureBackpacks.dll`, `EpicLoot.dll` (with
`ValheimModding-JsonDotNET`), `PlanBuild.dll`.

**Commands:** `testrig/scenarios/husnisse-04-contract.cmd` (headless, 15 assertions) and
`docs/evidence/0.4/client/gate.sh {prep,client,scene,p1,p2,p3,p4,collect}` (client).

## What the contract is expected to do, and what was read back

| # | Expectation (STORAGE-CONTRACT.md) | Read this | Verdict |
|---|---|---|---|
| 1 | `Count` = pockets + claim, and the number the crafting row shows is the same number | client, in the claim: `count $item_wood=55 carried=10 claim=45`; row builder: `5 <color=#ADE8E6>(55 [10+45])</color>` | **PASS** |
| 2 | A container outside the scope drops out of the count | client walks 30 m out: `scope no claim at -96.8,-6.9`, `count $item_wood=10 carried=10 claim=0`; back: `count $item_wood=55` | **PASS** |
| 3 | A container the claim may not use contributes nothing | inherited: the claim leg is `ContainerIndex.InClaim` → `ContainerFilter.CanUse`, whose ward-ground exclusion is proven in `docs/evidence/0.2b`; a blacklisted container is filtered in `ClaimSupplySource` (same predicate as the container window). Not re-exercised with a live ward in this gate | **partial — inherited, not re-run** |
| 4–6 | `TryConsume` all-or-nothing, order, reentrancy | not implemented: `can_consume=False`, `TryConsume` answers `false` and says why once | **expected** (0.5) |
| 7 | `Count` never throws, any string, any time, before the world | server, no player: `ready=False`, `count $item_iron=0`, `count not_a_real_item_zzz=0`; client, item absent from pockets and claim: `count $item_iron=0`; row for a material the claim does not hold stays vanilla (`row Iron 2` → `text='2'`) | **PASS** |

## The headless half

`husnisse-04-contract.cmd` — **PASS, 15 assertions, 0 failed**. On a dedicated server the contract answers
`api=1 provider=native can_consume=False ready=False`, `scope none (not ready: no local player)`, every
source `absent`, and `0` for a real item, an unknown one and a junk one — with a bowl, a claim and a chest
standing in the world it still refuses to invent a number. `husnisse_status` carries the same lines plus
`[contract] readouts state=native rows=pocket+claim aaacrafting=… aaabuildmenu=… mylittleui=… epicloot=…
planbuild=…`, so a support log says which surface bound and which stood down.

## The client half

| Phase | Read this | Verdict |
|---|---|---|
| scene | `husnisse claim … owner=-202584743 r=20 source=native containers=1`; chest filled from the client (`woodx20`) | PASS |
| p1 | `[contract] api=1 provider=native can_consume=False ready=True`; `scope claim 1:5424 at -125.8,-6.9 r=20 source=native owner=-202584743 containers=3 permitted=True`; `sources pockets=stacks=3 items=34 slots=stacks=0 items=0 backpack=absent claim=containers=3 stacks=3`; `count $item_wood=55 carried=10 claim=45` | PASS |
| p2 | `row Wood 5` → `5 <color=#ADE8E6>(55 [10+45])</color>`; `row Wood 40` → `40 <color=#ADE8E6>(55 [10+45])</color>`; `row Iron 10` → `10` (untouched) | PASS |
| p3 | out of the claim: `scope no claim`, `count $item_wood=10 carried=10 claim=0`; back in: `count $item_wood=55 carried=10 claim=45` | PASS |
| p4 | `integrations state=native rows=pocket+claim aaacrafting=bound aaabuildmenu=bound mylittleui=already-off epicloot=deferred-readonly planbuild=deferred-readonly` | PASS |
| relog | after quitting and relaunching the client the chest still holds `woodx20` and the count is unchanged — the client's own write persisted (`ContainerWrite` from card `t_ab336800`) | PASS |

`containers=3` on the final build: `cleanup` destroys the live objects but the earlier chests' **ZDOs
survive the world save**, so they reappear and are counted — correctly, they are still storage in the claim.
The numbers on this build (10 carried + 45 in three chests = 55) are therefore larger than the first pass's
(5 + 25 = 30); the arithmetic is what the gate asserts, not the totals. Start a gate from a renamed world if
a clean scene matters.

Screenshots: `client/captures/contract-world.png` (the gate world: nisse at the bowl, a chest behind it).
**The requirement row itself is not visible in a screenshot**: the build HUD hides its rows unless a piece is
selected, and the gate does not select one. The row evidence is the text read back from the live
`res_amount` `TMP_Text` immediately after `InventoryGui.SetupRequirement` ran — the same call the crafting
window makes — which is stronger than a screenshot for the *number*, weaker for "it is drawn". A visual pass
with the hammer out is on `docs/PLAYTEST.md`.

## What the readouts bound to, and what is deferred

- **AAA Crafting** — `GetItemsInCloseContainers` and `CalculateMaxCraftAmount` patched; `CfcLoaded` set so
  AAA's own display asks for container counts. Its rows and its batch ceiling now include the claim.
- **AAABuildMenu** — `PinnedRecipeTracker.CountItem` postfix; the pinned row's number includes the claim.
  The first build of this binding **failed to attach** (`Parameter "inventory" not found`): Harmony matches
  postfix parameters by name, and the game's parameter is `inv`. Caught by the server log, fixed, re-read.
- **MyLittleUI** — its own pocket-only available-amount is switched off for the session (`already-off` in
  this run, `suppressed` when it starts on), so the row has one number instead of two.
- **Quartermaster_Integrations** — if loaded, Husnisse registers none of the above and reports
  `provider=qi`. Not reachable in this gate: QI hard-depends on Quartermaster, and Husnisse refuses to
  start beside Quartermaster, so QI is never *loaded* while Husnisse runs. The practical migration case —
  QM removed, QI left in place — makes BepInEx skip QI for its missing dependency, so `PluginInfos` has no
  QI entry and Husnisse correctly answers `provider=native` instead of standing down into silence. The
  guard is forward-compatible, not dead: it fires the day a QI build loads without QM.
- **EpicLoot — deferred to 0.5, deliberately.** `EpicLoot.API.RegisterInventoryProvider` is one list for
  both the count and the pay: `InventoryManagement.RemoveItem` takes from the player's inventory first and
  only then asks the providers (`EpicLoot_UnityLib/InventoryManagement.cs`, `RemoveItem(string,int)`), so a
  provider that counts the claim but cannot take from it would make an enchant look affordable and then
  spend the pocket half of a cost it cannot finish. The provider's removal is also synchronous and
  client-side, while a claim chest's ZDO belongs to the server — the honest spend is `TryConsume` behind a
  server round trip, which is 0.5. The double-register refusal is live beside it.
- **PlanBuild — deferred to 0.5, for the same reason in a worse shape.** `PlanPiece.GetInventories` feeds
  both `HasAllResources` (the colour) and `AddAllMaterials` (the take), and the take is `void`: a read-only
  entry that counted the claim would place blueprints **for free**. Detected and logged instead.

## Client-gate traps met (add these to the skill)

- The probe's `give` writes into a fixed slot and silently answers `item=False` when it is occupied; only
  slot `(0,0)` accepted a fresh stack on this character, so give there or `take` something out first.
- A **harness `fill` does not reach the client**: it writes the server's in-memory `Inventory`, and
  `Container.Save` is owner-gated. Fill a chest from the client (`give` then `move`) or the claim leg reads
  zero. (Same root cause as card `t_ab336800`.)
- `Cleanup()` + a world save leaves the *ZDO* behind: after a restart the old chest came back next to the
  new one. Both are in the claim, which is why the gate's numbers are 5 + 20 rather than 20 — a stale
  chest counting is correct behaviour, but a rerun should start from a fresh world.
- `gate.sh` must not truncate `state.env` when it records the player position; losing `CLIENT_PID` makes the
  scene file stop at the bowl line under `set -u`, and the harness reports `PASS (0 assertions)`.
- A relaunched client's `LogOutput.log` still contains the *previous* session's `gc ready`; truncate it
  before starting the task, or the first "ready" you match is history.

## Rerun recipe

```bash
cd ~/projects/valheim/testrig
RIG_DST='V:\Gate031' RIG_PORT=2956 ./rig.sh down
# then, from the worktree:
cd docs/evidence/0.4/client
bash gate.sh prep            # fresh world, deploy build + harness, boot
bash gate.sh client          # launch the client into 2956, wait, record CLIENT_PID
bash gate.sh scene           # floor + bowl + chest at the client's position
#   give/move from the client so the chest's ZDO really holds the stock:
PROBE_WAIT=6 bash pc.sh "give Wood 25 0" "open piece_chest_wood" ; PROBE_WAIT=8 bash pc.sh "move piece_chest_wood Wood 20" "close"
bash gate.sh p1 && bash gate.sh p2 && bash gate.sh p3 && bash gate.sh p4
bash gate.sh collect
```
