# Changelog

## 0.1.0

- Porridge bowl: a placeable home piece that holds satiety, mood and the claim radius.
- Feeding is a server policy (`2. Nisse / Feeding Mode`): Required for every nisse, Optional so each player can switch it off at their own bowl with Alt-interact, or Disabled entirely. The per-bowl choice is stored in the bowl's ZDO.
- Nisse: a Greyling-derived creature that settles at a bowl and does not fight or wander.
- Claims: one non-overlapping radius per bowl, rebuilt from the world on load.
- Rounds: one bounded pass per interval, with collect and sort budgets.
- Tidying: the nisse files ground items dropped inside its claim into nearby chests and remembers where each kind of item went.
