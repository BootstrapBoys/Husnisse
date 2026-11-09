# Changelog

## 0.1.2

- The bowl is now a pantry: interacting opens four slots that accept food only. The nisse eats one item every `2. Nisse / Bowl Drain Minutes` (default 20) while satiety is below full; an empty bowl decays into hunger as before.
- Work is continuous instead of round-based. The queue drains at `3. Flow / Items Per Second` (2, 5 or 10), reconciles every five seconds and gives one bounded 2× catch-up after load.
- Empty Pockets and Tidy are server requests now. The client asks; the server validates distance, feeding and the nisse, then queues the work.
- Empty Pockets leaves the hotbar and equipped items alone.
- The nisse is invulnerable and uses a non-combat faction; enemies no longer target it.
- Alt-interact on a chest now logs why Tidy did or did not run, and the bowl's Alt toggle still controls per-bowl feeding.
- `husnisse_prefabs` writes every loaded container prefab to `BepInEx/BSBLogs/Husnisse-prefabs.txt`.
- `husnisse_status` reports queue depth, flow rate, bowl contents and drain interval.

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
