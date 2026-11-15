# Playtest checklist — 0.1.4

Everything below is unverified on a client unless marked proven. Run in this order: each block assumes
the previous one passed. Tick a box only when the expected result happened; anything else is a finding,
including "it works but feels wrong".

## Setup

- [ ] `BepInEx/plugins/BootstrapBoys-Husnisse/` contains **Husnisse.dll only**, and
      `ValheimModding-YamlDotNet/` is a sibling folder (detector + `YamlDotNet.dll`). A copy of
      `YamlDotNet.dll` inside the Husnisse folder is a finding.
- [ ] `LogOutput.log` shows `Husnisse 0.1.4 starting`, `localisation loaded: English (check: Porridge Bowl)`,
      `bowl registered`, `creature registered`, `content registered`, `order RPC registered` — and no red
      lines from us.
- [ ] `BepInEx/config/Husnisse/localization/en.yaml` exists after the first run, with `version: 3`.
- [ ] `BepInEx/config/com.bootstrapboys.husnisse.cfg` has `3. Flow / Items Per Second` (2, 5 or 10) and
      `2. Nisse / Bowl Drain Minutes` (default 20).

## 0. Legs (new in 0.1.4)

Headless-proven on the rig; this block is the visual pass. **Client pass 2026-11-15** on 0.2.1
(`docs/evidence/0.1.4-client/RESULT.md`): the walk and the fade were watched and photographed
from a real client. Ticked boxes carry the evidence there; the two unticked ones were not
exercised.

- [x] Build a bowl and a chest about 15 m apart on a floor or flat ground. Put one food item in the bowl,
      then drop three items near the bowl. *(deck, bowl and chest built by the scene driver; the bowl
      starts at satiety 25, so it worked without a food item being placed)*
- [x] The nisse **walks** to each drop in turn, then to the chest, stands at it for about a second, and
      the items appear in the chest. Then it walks back to its bowl. *(chest filled while the doorway was
      closed: 3 → 5 and 2 → 4; the transfer pause is 1 s by code and was not timed separately)*
- [x] Watch the whole trip: the movement is continuous at walking pace (about 2 m/s). If it jumps more
      than a stride, that is a finding. *(1.9 m/s per-second positions, largest single-frame step
      0.13 m while walking)*
- [x] The walk animation plays while it moves — legs moving, not sliding. *(animator `forward_speed`
      1.77–1.92 moving, 0.00 idle; `walk-nisse-0.2.1.jpg`)*
- [ ] Log out and back in with the nisse away from its bowl: it walks back home. *(not exercised)*
- [x] Put the chest in a room or behind a wall it cannot walk to. Drop items: the nisse **fades** at the
      threshold (about half a second out, half a second in) and the message
      "The way is shut. The nisse steps through." appears. The items still reach the chest.
      *(fade and delivery proven, `fade-threshold-0.2.1.jpg`; **the HUD line was never observed** —
      see the open finding in RESULT.md)*
- [x] A doorway it *can* walk through produces a walk, not a fade — both outcomes are correct, and
      which one happens depends on the shape of the opening. *(proven on 0.2.0, doorway open, no fade)*
- [x] With Debug Logging on, the log shows `[debug][Creature] leg -> …`, `leg blocked` / `fade across` for
      the unreachable case, and no `leg waiting for navmesh` line repeats more than twice per leg.
      *(13 `leg ->`, 12 `leg blocked`, 12 `fade across`, 3 `leg waiting` in one session)*
- [ ] `husnisse_status` shows `trip=Pickup/3`, `trip=ToTarget/3`, `trip=Transfer/3`, `trip=Home/3` in
      order during a trip, and `trip=idle` between trips. *(not sampled mid-trip)*

## 0.2 Claim authority (client pass)

Headless-proven on the rig (`SPIKE-REPORT.md` §5); these are the client-only checks.

**0.2b client session (2026-11-15) — proven, not by hand:** the five parity scenarios ran with a real
client connected on the same build, and the client's own claim objects matched every transition
(`docs/evidence/0.2b/client-claims-final.txt`): native `r=20`; ward `r=25 owner=111`; ward removed back
to native `r=20` without a restart; second bowl `suppressed=True code=WardHeld`; overlap `owner=222
r=28` then `owner=111 r=12`. The range ring and the hover claim line were photographed on the client
(`range-preview-ring.jpg`, `hover-native-claim.jpg`). The boxes below are the manual pass that remains.

- [ ] Place a bowl outside every ward: `husnisse_status` reports `source=native`, `ward=0` and the
      configured radius.
- [ ] Place a WardIsLove ward over it: within a couple of seconds the claim reports `source=ward`, a
      ward id, and the ward's radius instead of the configured one.
- [ ] Hold the bowl before placing it: a **green** ring shows the ground the claim would take, and it
      draws the ward's bubble (not the configured radius) when the ghost stands inside a ward.
- [ ] Aim at a spot already claimed, or a ward you may not use: the ring turns **red** and the placement
      is refused with the matching message.
- [ ] Hover a placed bowl: the last line reads `Claim <N> m (native|ward)`; hover a suppressed bowl and
      it reads `Claim refused — …`.
- [ ] Feed and order from the bowl while it stands in your own ward: both work.
- [ ] Let a permitted tribe member place a bowl in your ward (`Require Ward Ownership` off): it settles,
      and they can feed and order it.
- [ ] Turn `Require Ward Ownership` on and repeat: the placement is refused with the other-player's-ward
      message.
- [ ] Try to place a second bowl anywhere in the same ward: refused before it is placed, no materials
      spent.
- [ ] Remove the ward: within a couple of seconds the claim returns to the native radius and owner and
      the nisse keeps working — no restart.
- [ ] Stand a bowl inside two overlapping wards, one of them yours: the claim takes **your** ward's
      radius.
- [ ] Put a chest inside someone else's ward that overlaps your native claim: the nisse never touches it.

## 1. The bowl is a pantry

- [ ] Hammer → Misc → **Porridge Bowl** (5 wood). It places like any piece.
- [ ] Hover shows the name, satiety, mood, `[E] open bowl` and the Alt hint.
- [ ] `E` opens a container UI with **four slots**, not the old offering hand-off.
- [ ] Put food in: it goes in and stays. Put a non-food item in: it is refused and remains in your
      inventory. Try a weapon, wood and a trophy.
- [ ] Put one food item in and set `Bowl Drain Minutes` to 1, then wait: the item disappears after about a
      minute, satiety rises and the hover updates.
- [ ] Empty the bowl: appetite lines appear and the hover says the bowl is empty.
- [ ] Log out and back in: the same food is still in the four slots.

## 2. The creature

- [ ] It does not attack you, follow you, or wander out of the base.
- [ ] Hover on the nisse shows "Nisse" and `[E] take my things`.
- [ ] **Damage:** hit it. It must take no damage and must not die.
- [ ] **Raids/enemies:** enemies ignore it; note anything that targets it.
- [ ] **Floors:** place a bowl on an upper floor. Does the nisse stand on the floor or fall through?

## 3. Feeding policy

- [ ] With the server on forced feeding, `Alt` on the bowl refuses and says the server requires it.
- [ ] With forced feeding off, `Alt` toggles "the nisse will expect offerings" / "will work without
      offerings"; the change survives a relog.
- [ ] Feeding is now stocking the bowl, not handing food over. There is no `[E] offer food` interaction.

## 4. Orders and the queue

- [ ] Fill your inventory with mixed junk, keeping a few stacks in the **hotbar**, then `E` on the nisse.
- [ ] It says "The nisse will see to it." and items begin moving into claim chests over the next few
      seconds, not all at once.
- [ ] Hotbar and equipped gear stay on you.
- [ ] With no chests in claim: the queue has nowhere to put things and your inventory is unchanged.
- [ ] With the bowl empty and feeding required: the order refuses and says so.
- [ ] `Alt`-interact a chest inside the claim: its contents file into other chests over time. The chest
      itself is not a destination for its own contents.
- [ ] `Alt`-interact a chest outside the claim: it opens normally, no nisse action.
- [ ] With Debug Logging on, the log shows `tidy hit alt=True ... claim=<id>` for a real Tidy, so a silent
      Alt can be told apart from a missed hook.

## 5. Flow rate and memory

- [ ] Set `Items Per Second` to 2, drop 20 loose items in the claim, and time the cleanup: roughly 10
      seconds plus the 5 s reconcile tick.
- [ ] Set it to 10 and repeat: noticeably faster, without a single instant jump of the whole pile.
- [ ] Drop the same item again later: it goes to the **same chest** as before.
- [ ] Items dropped outside the claim are ignored.
- [ ] A cart or ship inside the claim is not used as storage.

## 5b. The container network (0.3a + 0.3b)

The panel itself, on a client — the headless gate proves the routing, not the drawing.

- [ ] Open a chest inside the claim: two buttons sit in the top-right, `Off the network` and
      `Rules: No rule`. Open a chest outside the claim, or a cart/ship: neither appears.
- [ ] Click the network toggle: it reads `On the network`. Close and reopen the chest: still on.
- [ ] Open the rule panel: eight categories in two rows, `Overflow`, `Deposit`, `No rule`, a
      `Priority` row with `−`/`+`, `Save contents`, `Use suggestion` (only when the chest has no rule
      and something in it is a known category) and `Copy to nearby`.
- [ ] Click `Metals` while the chest holds coal: the button turns amber, the title bar reads
      `Rules: Metals`, and the chest's `husnisse_rule` is `v1;k=cat;c=metals`.
- [ ] Put a chest of ore next to it, set that one to `Metals`, then click `+` on the second until its
      priority reads `1`. Drop ore in the claim: it goes to the priority-1 chest while it has room.
- [ ] Fill a chest with mixed junk and toggle it on from the network button (not the panel): the rule
      it lands on is the category most of the contents belong to. Change it — the suggestion never
      comes back over your choice.
- [ ] Open `Save contents` on a chest holding a few kinds of item: one row per item with a count,
      all ticked. Untick one, `Apply`, and confirm the rule reads `Save contents (n-1)` and the
      unticked item no longer routes there.
- [ ] `Copy to nearby` on a chest with a rule and one or two same-type neighbours: the line reads
      "Copy this rule to N nearby container(s)?" with `Copy`/`Cancel`; cancel changes nothing, copy
      sets the neighbours' rules.
- [ ] With a `Deposit` chest in the base, drop an item no rule claims: it goes to `Overflow` (or a
      plain networked chest), never into the deposit chest.
- [ ] Put mixed items in the `Deposit` chest (a metal, timber, and something no rule claims): over a
      few trips the chest empties itself into the metals chest, the wood chest and `Overflow`.
- [ ] Fill a metals chest to the last slot, then drop a second metal: it goes to the next metals chest —
      at any priority — before `Overflow`. Fill that one too and drop a third: `Overflow` takes it and
      the log line reads `why=overflow (full: cat:metals)`.
- [ ] `Deposit all` on a **networked** chest with a mixed pocket: the nisse takes the stacks and they
      land in the chests whose rules claim them, not in the chest you pressed the button on.
- [ ] `Deposit all` on an **off-network** chest: every pocket stack lands in that chest at once,
      unsorted, and the message says `Deposited N stack(s)`. Nothing later moves them on.
- [ ] Fill that off-network chest to the last slot and press `Deposit all` again: the overflow stays in
      your pockets and the message says how many would not fit.
- [ ] A blacklisted container (a cart, a ship, a display pile) shows no buttons.
- [ ] `husnisse_rule blacklist piece_chest_wood on <n>` on an ordinary chest: its buttons disappear the
      next time the window is opened, and a stack dropped beside it is not filed into it.
      `... blacklist piece_chest_wood off <n>` brings the buttons back.
- [ ] **Animation LOD**: stand 40 m from the nisse (`2. Nisse / Animation LOD Distance` is 30) and watch
      it: the body stops animating while its trips keep running (the position still changes). Walk
      inside 30 m and the walk animation resumes. Set the distance to 5 m and back to confirm the knob.
- [ ] A non-owner opens the same chest: the buttons are there, greyed, and the title says `read only`.
- [ ] `husnisse_categories check` in the console lists what the presets do not cover; a gap item
      (a pelt, a bone, a boss drop) lands in Overflow rather than in a ruled chest.

## 5c. Teaching and the ledger (0.3.1)

- [ ] Alt on the nisse: it says it is watching and its hover says so; Alt again stops it.
- [ ] While it watches, put one stack into a chest that had no rule: the chest joins the network
      (`husnisse_rule list` shows `net=on rule=custom:1`), the nisse says it will keep that item here,
      and the item stays in the chest.
- [ ] Put a second kind of item into a second chest without pressing Alt again: it learns that too.
- [ ] Stop watching (Alt), then put a stack into a third chest: its rule stays `none` and it stays off
      the network.
- [ ] Throw three of the taught items on the floor inside the claim: each lands in the chest that was
      taught, not merely in a chest that accepts the category.
- [ ] Teach a chest that already has a category rule it does not accept: the nisse refuses in words and
      the rule is unchanged.
- [ ] Open the ledger (Tab, then H): the claim line, the rows and the unclassified count match
      `husnisse status`; the panel closes with the inventory.
- [ ] Select an unclassified chest, press a category: the row shows the rule, `husnisse_rule list`
      agrees, and opening that chest shows the same rule in its own panel (one writer, two surfaces).
- [ ] Change priority, net and blacklist from the ledger: each is reflected in the container window and
      in `husnisse_rule list`; Tidy on a chest you are not standing at still sends the nisse.
- [ ] Untick Metals in the pickup filter and drop copper and wood in the claim: only the wood is
      collected; tick Metals again and the copper is collected on the next pass.
- [ ] With two partial stacks of the same item in your pockets, Empty Pockets: the stacks merge first
      and the nisse carries one stack.
- [ ] On the host, `husnisse_rules export`, change two rules, `husnisse_rules apply`: both come back.
      Destroy a chest and `apply` again: it refuses and nothing changes.
- [ ] `husnisse_rules preset metals $item_coal` narrows Metals to coal (a copper drop stops being
      picked up); `husnisse_rules preset metals builtin` restores it.
- [ ] A client that is not the claim's owner sees the ledger read-only: buttons disabled, "read only" in
      the title, no message spam when clicked.

## 6. Persistence and diagnostics

- [ ] Log out and back in: claim, bowl owner, satiety, mood, bowl contents and memory survive.
- [ ] The nisse comes back to its bowl on load.
- [ ] `husnisse_status` lists the claim with `bowl_items`, `queued`, `rate`, `bowl_drain`, satiety, mood and
      nisse presence.
- [ ] `husnisse_prefabs` writes `BepInEx/BSBLogs/Husnisse-prefabs.txt` and it lists the pack's containers.

## 7. Teardown

- [ ] Destroy the bowl: the claim is removed, the nisse disappears, and nothing is left orphaned.
- [ ] After teardown, `husnisse_status` shows `claims=0 nisses=0`.

## 8. Localisation

- [ ] Copy `en.yaml` to `de.yaml`, change `language: German`, translate a few values, restart.
- [ ] Switch the game language to German: hover and messages change; untranslated keys stay English.
- [ ] Deliberately break the YAML (bad indent): the file is skipped with a log line, the game still runs.

## 9. Feel — the actual question

Not pass/fail. Write down:

- Does the nisse read as a spirit that lives here, or as a prop with a sorting script?
- Is the fade (walking arrives in 0.1.3) noticeable and does it matter?
- Is stocking the bowl a ritual or a chore? Would you keep doing it after a week?
- Did any order surprise you — wrong items taken, wrong chest, unclear message?

## What to send back

- `BepInEx/LogOutput.log`
- `BepInEx/BSBLogs/Husnisse.log`
- Screenshots of: the four-slot bowl, a refused non-food item, the bowl hover, an order message, a filed
  chest
- A short list of findings, each with what you expected and what happened
