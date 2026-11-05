# Playtest checklist — 0.1

Everything below is unverified. Run in this order: each block assumes the previous one passed. Tick a box
only when the expected result happened; anything else is a finding, including "it works but feels wrong".

## Setup

- [ ] `BepInEx/plugins/BootstrapBoys-Husnisse/` contains **Husnisse.dll and YamlDotNet.dll**. Without the
      second the plugin will not load.
- [ ] `LogOutput.log` shows `Husnisse 0.1.1 starting`, `localisation loaded: English (check: Porridge Bowl)`,
      `bowl registered`, `creature registered`, `content registered` — and no red lines from us.
- [ ] `BepInEx/config/Husnisse/localization/en.yaml` exists after the first run.

## 1. The bowl

- [ ] Hammer → Misc → **Porridge Bowl** (5 wood). It places like any piece.
- [ ] Hover shows the name, satiety, mood and `[E] offer food`.
- [ ] A nisse appears at the bowl within a second or two.
- [ ] Placing a **second bowl inside the first claim (20 m)** is refused with "Another nisse already claims
      this ground." Place it further away and it is allowed.

## 2. The creature

- [ ] It does not attack you, follow you, or wander out of the base.
- [ ] Hover on the nisse shows "Nisse" and `[E] take my things`.
- [ ] **Damage:** hit it. It must not take damage (0.1.1 does not implement this yet — report what happens).
- [ ] **Raids/enemies:** does anything attack it or does it stay ignored? Note what happened.
- [ ] **Floors:** place a bowl on an upper floor. Does the nisse stand on the floor or fall through?

## 3. Feeding

- [ ] `E` on the bowl with food in hand consumes one item, raises satiety, and prints the offering line.
- [ ] With the bowl empty, appetite lines appear at intervals and the hover reads "The bowl is empty".
- [ ] `Alt` on the bowl toggles "the nisse will expect offerings" / "will work without offerings"
      (only when the server has feeding set to optional; a forced server refuses).
- [ ] A second player cannot fill your bowl (owner check).

## 4. Empty Pockets

- [ ] Fill your inventory with mixed junk, then `E` on the **nisse**.
- [ ] Items move into chests inside the claim; you get "The nisse takes N stack(s)."
- [ ] Equipped gear and food stay on you.
- [ ] With no chests in claim: "The nisse has nowhere to put that." and your inventory is unchanged.
- [ ] With the bowl empty and feeding required: it refuses and says so.

## 5. Tidy

- [ ] Place two or three chests inside the claim. Put a mixed pile in one.
- [ ] `Alt`-interact that chest → contents are filed into the other chests, "The nisse files N stack(s)."
- [ ] The chest you tidied is not used as a destination for its own contents.
- [ ] `Alt`-interact a chest **outside** the claim: it opens normally, no nisse action.

## 6. Rounds and memory

- [ ] Drop items on the ground inside the claim; within ~30 s they disappear into chests.
- [ ] Drop the same item again later: it goes to the **same chest** as before.
- [ ] Items dropped outside the claim are ignored.
- [ ] A cart or ship inside the claim is not used as storage.

## 7. Persistence

- [ ] Log out and back in: claim, bowl owner, satiety, mood and memory survive.
- [ ] The nisse comes back to its bowl on load.
- [ ] `husnisse_status` in the console lists the claim with bowl, owner, radius, satiety, mood, nisse.

## 8. Teardown

- [ ] Destroy the bowl: the claim is removed, the nisse disappears, and nothing is left orphaned.
- [ ] After teardown, `husnisse_status` shows `claims=0 nisses=0`.

## 9. Localisation

- [ ] Copy `en.yaml` to `de.yaml`, change `language: German`, translate a few values, restart.
- [ ] Switch the game language to German: hover and messages change; untranslated keys stay English.
- [ ] Deliberately break the YAML (bad indent): the file is skipped with a log line, the game still runs.

## 10. Feel — the actual question

Not pass/fail. Write down:

- Does the nisse read as a spirit that lives here, or as a prop with a sorting script?
- Is the teleport (no walking in 0.1) noticeable and does it matter?
- Is feeding a ritual or a chore? Would you keep doing it after a week?
- Did any order surprise you — wrong items taken, wrong chest, unclear message?

## What to send back

- `BepInEx/LogOutput.log`
- `BepInEx/BSBLogs/Husnisse.log`
- Screenshots of: the bowl hover, the nisse, an order message, a filed chest
- A short list of findings, each with what you expected and what happened
