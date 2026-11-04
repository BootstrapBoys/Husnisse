# Changelog

## 0.1.0

- Porridge bowl: a placeable home piece that holds satiety, mood and the claim radius.
- Feeding is the server owner's choice (`2. Nisse / Force Feeding`), written into the world as a global key on load. Forced: every nisse must be fed. Not forced: each player switches feeding on or off at their own bowl with Alt-interact, stored in the bowl's ZDO.
- Nisse: a Greyling-derived creature that settles at a bowl and does not fight or wander.
- Claims: one non-overlapping radius per bowl, rebuilt from the world on load.
- Rounds: one bounded pass per interval, with collect and sort budgets.
- Tidying: the nisse files ground items dropped inside its claim into nearby chests and remembers where each kind of item went.
