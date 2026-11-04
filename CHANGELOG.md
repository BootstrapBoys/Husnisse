# Changelog

## 0.1.0

- Porridge bowl: a placeable home piece that holds satiety, mood and the claim radius. The clone strips CookingStation, CraftingStation and Container, so the bowl is only ever a bowl.
- A bowl's nisse leaves with it: destroying the bowl despawns the creature it settled.
- Feeding is the server owner's choice (`2. Nisse / Feeding Required`), written into the world as a global key on load. Forced: every nisse must be fed. Not forced: feeding is opt-in — new bowls ask for nothing, and Alt-interact at a bowl turns the care loop on, stored in its ZDO.
- Empty Pockets: interact with the nisse and it files what it can carry from your inventory, leaving equipped items alone.
- Tidy: Alt-interact a chest inside a claim and the nisse files its contents. Both orders respect the sort budget and refuse when the nisse is absent, unfed, or out of claim.
- Sulking: neglect drops mood, and below the threshold the nisse misfiles one stack at a time (capped), complains to nearby players, and stops as soon as it is fed.
- Bowls refuse to be placed on ground another claim covers, or inside a ward the placer cannot use.
- Diagnostics: `BepInEx/BSBLogs/Husnisse.log` plus the `husnisse_status` console command.
- Save schema marker (`husnisse_schema_1`) so later versions can tell what wrote a world.
- Read-only claim storage facade (`Count`, `CanAfford`) in the shape the storage contract expects.
- Nisse: a Greyling-derived creature that settles at a bowl and does not fight or wander.
- Claims: one non-overlapping radius per bowl, rebuilt from the world on load.
- Rounds: one bounded pass per interval, with collect and sort budgets.
- Tidying: the nisse files ground items dropped inside its claim into nearby chests and remembers where each kind of item went.
