# Changelog

## 0.5.0

Internal only — craft from the claim.

- **The nisse brings what the base owes a craft.** A recipe the pockets cannot pay for becomes a supply errand: the nisse walks to the chests holding the missing materials, takes the planned stacks while it stands at each one, carries a visible load back to the bench and puts it in your hands. The craft then runs exactly as vanilla does — it spends your pockets, which by then hold the materials — so the pockets-first order the storage contract fixes is kept and nothing is ever taken out of a chest on a promise. `5. Contract / Craft from Claim` turns the whole path off.
- **The load is visible on every peer.** The nisse carries two or three of the actual materials, built on each machine from the item prefabs (mesh only, no second networked object), shown when it lifts the load at the chest and cleared when it hands it over.
- **All-or-nothing, and never a promise.** A request for more than the claim holds is refused whole and no chest is touched; a second request while the nisse is carrying is refused as busy. `TryConsume` debits pockets, ExtraSlots rows and the equipped backpack, and when an amount would need the claim it asks the nisse for the shortfall and answers `false` with nothing mutated — the caller retries once the load is in its hands.
- **AAA Crafting's batch fetches for the whole batch**, and the deferred craft runs with the batch's size. The recipe list repaints after a craft, so the rows agree with the pockets the craft just emptied.
- **EpicLoot costs** ask the nisse before they are paid instead of registering a provider that cannot take from a chest: the affordability check every enchanting, augmenting, rune and upgrade cost goes through starts a fetch, and EpicLoot pays from the pockets once the load lands. No count is advertised that the mod cannot pay, and no enchant is free.
- **PlanBuild blueprint costs** do the same: a plan whose materials the pockets lack asks the nisse for the shortfall and pays from the pockets one unit at a time. No inventory entry is added to PlanBuild's list, because its take returns nothing and an entry that could not remove would place the blueprint for free.
- **Stack sizes defer to DropMoreLoot by not competing.** Husnisse never writes a stack size, and the diagnostics say whether DropMoreLoot owns them. Bench-crafted and looted stacks merge (the crafter is not part of the identity), EpicLoot's material stamps do not split a stack, and quality, world level, variant and durability still keep stacks apart.
- The bowl's pantry is not a supply chest: it is the nisse's own food. Every other container in the claim is a source.

## 0.4.0

Internal only — the read-only storage contract.

- **The contract answers.** `Count` and `CanAfford` over the sources the storage contract fixes — pockets → ExtraSlots rows → the equipped AdventureBackpacks pack → the containers of the claim you are standing in — with `ApiVersion`, `IsReady` and `ScopeDescription` beside them. `husnisse_contract [item]` prints the answer and where each part came from; `husnisse_status` prints the same lines. A player outside every claim counts what they carry; a claim a player is not permitted on contributes nothing.
- **The crafting and build rows count the base.** A requirement reads `10 (42 [12+30])` — how many are needed, then how many you can reach, then the split between carried and stored. `5. Contract / Split Requirement Counts` turns the split off; `5. Contract / Requirement Readouts` turns the whole readout off and leaves every crafting UI vanilla.
- **AAA Crafting** counts the claim in its requirement rows and its batch ceiling; **AAABuildMenu**'s pinned rows count it too. **MyLittleUI**'s own pocket-only number is switched off for the session, so a row has one count instead of two — the same arrangement AAA already uses for MyLittleUI's multicraft setting.
- **One provider, never two.** If `Quartermaster_Integrations` is loaded, Husnisse registers none of it and reports `provider=qi`, so a chest is never counted twice while the old packages are being removed.
- **This build reads; it does not spend.** `TryConsume` answers `false` and says so once in the log: a spend needs a visible delivery, and that is 0.5. EpicLoot's inventory provider and PlanBuild's blueprint cost are therefore deferred to 0.5 as well — both pay through the same list they count, so a read-only entry would either take the pocket half of a cost it cannot finish (EpicLoot) or place blueprints for free (PlanBuild). Both are detected and explained in the log instead of silently missing.
- Readouts are client-only and no-op headless: a dedicated server has no player to scope, and `husnisse_contract` answers `ready=False` there rather than inventing a number.

## 0.3.1

Internal only — teaching and the ledger.

- **Teaching by demonstration.** Alt-interact the nisse and it leans in to watch; put a sample into the chest where that kind of thing belongs and the chest learns it — its rule becomes a list holding that item and the chest joins the network if it was not on it. The nisse watches for two minutes, so three chests are three samples and one gesture. It only learns while it is watching: a stack put into a chest at any other time changes no rule.
- **The ledger.** `1. General / Ledger Key` (default **H**) opens it from the inventory: every container in the claim with its rule, priority, network flag and blacklist, the containers nobody has classified yet, and the claim's pickup filter. From one screen you can classify an unknown chest, change a rule or its priority, join or leave the network, blacklist a container, **Tidy** a chest you do not own, set the pickup filter and draw the claim ring. A player the claim does not permit sees the same screen read-only.
- **Pickup filter, per claim and per item type.** Untick a category in the ledger and the nisse stops collecting that kind of thing off the floor of its claim; a bowl nobody configured still collects everything. The filter is about pickup only — what a player hands over or empties out of a chest always moves.
- **Pocket stacks merge on sort.** An Empty Pockets order merges identical partial stacks in your pockets before it queues anything, so the nisse carries one full stack instead of three halves. Stacks merge only when the game would stack them (same item, level, quality, variant and durability); the hotbar is left alone.
- **Rules as a file.** `husnisse_rules export` writes every container rule in the world to `BepInEx/config/Husnisse/rules.yaml`; `plan` resolves it against the live world and prints the mapping; `map` re-points an entry the file cannot place; `apply` imports it. The import is all-or-nothing: one entry that does not fit and nothing is written. A copied world with the same base imports with every rule intact.
- **Editable presets.** The rules file carries the named half of the eight categories, so an admin can say what "Metals" means for their world; `husnisse_rules preset <category> <names|builtin>` edits one live. The table is world state — the host publishes it and every client applies it — so a suggestion and a routed stack cannot disagree.
- Seeds, crops, flour and spices are classified as **Food** now; they were in the table but the type rule never consulted it, so they routed nowhere.
- **A filed stack now survives the trip.** The nisse writes into a chest through the chest's own save, which only its owner may perform: on a dedicated server that is the player who last opened it — exactly how a chest gets configured. The nisse takes the chest first and verifies the packed contents reached it, for a trip's destination and its source (an unsaved removal would duplicate a stack), for **Deposit all**, and for a sulk's tidy-up. A deposit that did not reach the chest is logged instead of being reported as filed.
- A refused order now says why in the diagnostics file (`order EmptyPockets -> TooFar`).

## 0.3.0

Internal only — the container network's controls, its rule storage and its router. This build proves a
base can be configured and that items file themselves by the rules written there.

- Every container in a claim now has three buttons in the top-right of its window: a network toggle, a rule button and **Deposit all**. A container outside a claim, a blacklisted one (carts, ships and display piles are blacklisted by default) and a dedicated server show none.
- **A container can be blacklisted one at a time** — `husnisse_rule blacklist <prefab> <on|off> [index]` — and a blacklisted container never shows a button and is never part of a network. Carts, ships and display piles are blacklisted **by class**: `4. Storage / Skip Carts and Ships` and `Skip Display Piles` decide that, so turning a class back on really does make it eligible. `husnisse_rule netall <prefab> <on|off>` joins or leaves a whole prefab in one command.
- **The queue is fair.** Work is queued per task type — ground drops, deposit chests, player orders — and the nisse serves them in turn, so a floor full of loot can no longer starve a deposit chest or an order waiting behind it. One dump is one trip, not a queue of them.
- **Animation level of detail.** A nisse farther than `2. Nisse / Animation LOD Distance` (default 30 m) from every player stops animating; its work carries on. Each peer decides for itself: a dedicated server animates nothing while nobody is connected, a client animates the nisses near it.
- **A two-hundred-chest base is measured, not assumed.** `husnisse_bench` times the pieces of one reconcile pass against the first claim (container scan, network filter, one routing answer per item, the whole refill) and prints them in milliseconds, so a slow base has a number instead of a feeling. The refill now scans the claim's containers once and skips the world's drops entirely when the claim has no usable container.
- The rule panel offers the eight survival categories, the two roles — Overflow and Deposit — "No rule", a **priority** for the chest, a **saved list** captured from its contents with a checkbox preview, and a **copy** of the rule to the nearby chests of the same kind (behind a confirm).
- Categories are a real table, read off the game's own item list rather than typed from memory: ore, scrap, bars and nails; timber and bark; cut stone and tar; treasure; food, crops, seeds and spices; mead bases; and the tools Valheim files as weapons. `husnisse_categories check` reports what the presets do not cover in the running build, and `husnisse_categories dump` writes every item with its type and satiety.
- **Routing obeys the rules.** A stack goes to the chest whose category or saved list claims it, highest priority first; `Overflow` takes what no rule claims; a chest with no rule is the general-purpose one. A `Deposit` chest is never a destination, and the nisse's memory now only breaks a tie — a written rule outranks a habit. A stack nothing accepts is left where it is and named in the diagnostics instead of being dropped into the nearest chest.
- **A full chest does not claim a stack.** Room is decided per stack, so a chest with no free slot and no stack of that item drops out of the running: a lower-priority chest of the same category is tried before `Overflow`, and `Overflow` is used only when nothing that claims the stack can hold it. The reason names the rules that were full — `why=overflow (full: cat:metals)` — and a Tidy that can move nothing says the same thing instead of `unmatched=1` alone.
- **A `Deposit` chest is a source.** The nisse carries what a player throws into it to the chests whose rules claim it, one visible trip at a time, and it must be on the network to be drained.
- **Deposit all** in the container window: at a networked chest the nisse takes the pockets and files them by their rules; at an off-network chest every pocket stack goes into that chest, unsorted and at once, with no fed nisse required. What does not fit stays in the pockets and the message says how many.
- The first toggle of a chest with no rule suggests the category most of its contents belong to and writes it; the player can change it immediately.
- Every landing is written to `BepInEx/BSBLogs/Husnisse.log` with its reason — `landed moved=1 target=piece_chest_wood(Clone) at=(4,3) why=cat:metals p=1` — so a misfiled stack can be explained after the fact.
- `husnisse_rule` grew `prio <prefab> <n> [index]`, `suggest <prefab> [index]` and `copy <prefab> <index> [radius]`, and rule shorthands take a priority suffix (`cat:metals@2`).
- The claim owner and the players a ward permits change rules; anyone else sees the buttons disabled and is told why. A client writes nothing: the request goes to the server, which validates the claim, the sender's identity and the container before writing.
- `husnisse_rule list | net <prefab> <on|off> [index] | set <prefab> <rule> [index]` reads and writes the same state from the server console. `husnisse_status` gains one line per network container and a `[rules]` count.
- A rule encoding this build does not understand reads as "no rule" instead of breaking the chest.
- **Orders from a client now reach the server.** The order channel dropped every request on a dedicated server: it named the caller by `ZNetPeer.m_playerID`, which this game build never fills (it registers `RPC_PlayerID` and nothing invokes it), and it expected a `Player` object that a dedicated server does not simulate. The caller is now named by the character ZDO the connection is driving — the same `playerID` the client computes locally — and a request naming a different player is refused rather than obeyed.
- Alt on the bowl, Alt on a chest and interacting with the nisse now answer the player only after the server has accepted the order: "The nisse takes 6 stack(s)" / "The nisse files 3 stack(s)" instead of a promise made before anything happened.
- An order given on the host or in single-player no longer dies with `Unable to read beyond the end of the stream`: the server rewinds the package it built before reading it.
- Empty Pockets works from a client on a dedicated server. The player's inventory only exists on the player's own machine, so the order is now two steps: the server accepts it and asks for the stacks, the client hands them over, and the server puts them on the ground by its bowl as real drops and lets the ordinary trip file them — from that moment the server owns them, and a dropped handover is visible rather than lost in a list.

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
