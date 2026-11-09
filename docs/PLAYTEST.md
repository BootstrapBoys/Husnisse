# Playtest checklist — 0.1.2

Everything below is unverified on a client. Run in this order: each block assumes the previous one passed.
Tick a box only when the expected result happened; anything else is a finding, including "it works but
feels wrong".

## Setup

- [ ] `BepInEx/plugins/BootstrapBoys-Husnisse/` contains **Husnisse.dll and YamlDotNet.dll**. Without the
      second the plugin will not load.
- [ ] `LogOutput.log` shows `Husnisse 0.1.2 starting`, `localisation loaded: English (check: Porridge Bowl)`,
      `bowl registered`, `creature registered`, `content registered`, `order RPC registered` — and no red
      lines from us.
- [ ] `BepInEx/config/Husnisse/localization/en.yaml` exists after the first run.
- [ ] `BepInEx/config/com.bootstrapboys.husnisse.cfg` has `3. Flow / Items Per Second` (2, 5 or 10) and
      `2. Nisse / Bowl Drain Minutes` (default 20).

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
