# Changelog

## 0.2.2

Internal only — the nisse's warning line now reaches a player's HUD.

- "The way is shut. The nisse steps through." now reaches the player who can hear the nisse. The line was addressed to `Player.GetAllPlayers()`, and a dedicated server simulates no player characters of its own, so that list is always empty there: the line was built, throttled and then handed to nobody. The audience is the connected peers now, each addressed through the game's own HUD entry point.
- A line about something that just happened — a fade, a misfiled stack, a bowl refilled — keeps its own once-a-minute slot per claim. An idle remark about an empty bowl can no longer take that slot and leave the event silent.
- Earshot is measured across the ground, so a player on another floor of the same house still hears the nisse.

## 0.2.1

Internal only — the threshold fade is now played on peers.

- A leg the nisse cannot walk now fades on **every** peer, not only where it was ordered. The fade RPC used the owner-addressed call, which on a dedicated server addresses the server itself: the body dissolved where nobody could see it and a connected client saw the nisse jump across the threshold instead of fading. Clients are addressed explicitly now.

## 0.2.0

Internal only — claim ownership and its range preview; 0.2 is not released.

- A bowl standing inside a WardIsLove ward takes that ward's range, owner and permitted players as its claim. Outside every ward the bowl keeps its own radius and its placer, exactly as before.
- A second bowl placed inside the same ward refuses to settle, and a nisse never uses a container standing inside a ward it does not own.
- Removing the ward, or moving the bowl out of it, returns the bowl to the radius and owner it had before, without a restart.
- **Range preview**: while a bowl is held, a ring on the ground shows the claim it would take — the ward's bubble inside a ward, the configured radius outside one. It is green where the bowl can be placed and red where it is refused; the refusal comes from the same calculation the guard uses, so the ring and the message always agree.
- Hovering a placed bowl states its claim (`Claim 25 m (ward)`), or why the claim is refused (`Claim refused — another bowl is already here`).
- `husnisse_preview [x z] [radius] [player]` prints the claim a bowl would get at a point and draws the same ring for eight seconds. This is the data seam the 0.3.1 ledger will read.
- `husnisse_status` claim lines now carry the effective centre and a machine-readable refusal code, and the placement refusal is logged with the verdict that produced it.
- Parity harness: five claim scenarios (native, ward, ward removed, second bowl in a ward, overlap) plus `husnisse preview` / `expect_preview`. All pass headlessly and with a real client connected on the same build; the client's own claim state matches every transition.

## 0.1.4

- The nisse walks. Work is a visible trip now: it collects a batch, walks to the chest that owns those items, stands at it while the transfer happens, and walks back to its bowl. It no longer appears next to the chest.
- A leg that cannot be walked — a wall, a closed room, a floor with no route — is retried once and then faded across, and the nisse says the way is shut. A silent no-op is never the fallback.
- The bowl's spirit returns home after every trip, and walks back to its bowl on load.
- A free-standing 2 m doorway is walkable on this game build (measured); a closed room is not and fades. Movement is still room-scale and outdoor-first.
- `3. Flow / Items Per Second` now bounds how much the nisse loads per trip; walking sets how often trips happen. The nisse may carry up to ten stacks.
- Diagnostics: `husnisse_status` reports the active trip and its batch, and `[Creature]` debug lines cover every leg, retry, fade and stuck body.
- Internal: the Greyling controller has no `spawn` trigger and its `idle` parameter is an int, so the settlement pose uses the idle variation instead of a trigger that never fired.

## 0.1.3

- Fixed the client failing to start: `Loc.T` read `Localization.instance` during plugin `Awake`, before Steamworks was initialized, throwing `InvalidOperationException` and aborting the plugin before localisation, Harmony patches and content registration. The lookup now falls back to English until the game is ready.
- Verified on the client rig: the plugin reaches `localisation loaded`, `core ready`, `content registered`; the food guard refuses coal and accepts food in the four-slot pantry.

## 0.1.2

- The bowl is now a pantry: interacting opens four slots that accept food only. The nisse eats one item every `2. Nisse / Bowl Drain Minutes` (default 20) while satiety is below full; an empty bowl decays into hunger as before.
- Work is continuous instead of round-based. The queue drains at `3. Flow / Items Per Second` (2, 5 or 10), reconciles every five seconds and gives one bounded 2× catch-up after load.
- Empty Pockets and Tidy are server requests now. The client asks; the server validates distance, feeding and the nisse, then queues the work.
- Empty Pockets leaves the hotbar and equipped items alone.
- The nisse is invulnerable and uses a non-combat faction; enemies no longer target it.
- Alt-interact on a chest now logs why Tidy did or did not run, and the bowl's Alt toggle still controls per-bowl feeding.
- `husnisse_prefabs` writes every loaded container prefab to `BepInEx/BSBLogs/Husnisse-prefabs.txt`.
- `husnisse_status` reports queue depth, flow rate, bowl contents and drain interval.
- Packaging: YamlDotNet is declared as a dependency (`ValheimModding-YamlDotNet`) and is no longer bundled in the zip.

## 0.1.1

- The bowl no longer demands a forge. It inherited the cauldron's crafting-station requirement, so its recipe was invisible unless you stood at a forge; it is now a station-free piece you can build anywhere.
- The registration log states the bowl's category, crafting station, requirement count and piece table; `husnisse_status` reports whether the piece exists in the object database.

## 0.1.0

- Porridge bowl: a placeable home piece that holds satiety, mood and the claim radius. The clone strips CookingStation, CraftingStation and Container, so the bowl is only ever a bowl.
- A bowl's nisse leaves with it: destroying the bowl despawns the creature it settled.
- Feeding is the server owner's choice (`2. Nisse / Feeding Required`), written into the world as a global key on load. Forced: every nisse must be fed. Not forced: feeding is opt-in — new bowls ask for nothing, and Alt-interact at a bowl turns the care loop on, stored in its ZDO.
- Empty Pockets: interact with the nisse and it files what it can carry from your inventory, leaving equipped items alone.
- Tidy: Alt-interact a chest inside a claim and the nisse files its contents. Both orders respect the sort budget and refuse when the nisse is absent, unfed, or out of claim.
- Localisation: all player-facing strings live in YAML files under BepInEx/config/Husnisse/localization. English ships embedded and is written on first run; translators add a file per language and the game's language setting selects it, with English as the fallback.
- Sulking: neglect drops mood, and below the threshold the nisse misfiles one stack at a time (capped), complains to nearby players, and stops as soon as it is fed.
- Bowls refuse to be placed on ground another claim covers, or inside a ward the placer cannot use.
- Diagnostics: `BepInEx/BSBLogs/Husnisse.log` plus the `husnisse_status` console command.
- Save schema marker (`husnisse_schema_1`) so later versions can tell what wrote a world.
- Read-only claim storage facade (`Count`, `CanAfford`) in the shape the storage contract expects.
- Nisse: a Greyling-derived creature that settles at a bowl and does not fight or wander.
- Claims: one non-overlapping radius per bowl, rebuilt from the world on load.
- Rounds: one bounded pass per interval, with collect and sort budgets.
- Tidying: the nisse files ground items dropped inside its claim into nearby chests and remembers where each kind of item went.
