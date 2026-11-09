# Suggested improvements — decisions pending

Each item is a proposal, not a commitment. Cost is against the release that would carry it.
Say the number to accept, reject, or modify.

| # | Improvement | Why it matters | Cost | Where | Recommendation |
|---|---|---|---|---|---|
| 1 | **Preferred + overflow per container** | A full or wrong chest currently blocks a category; overflow routing is QM's most-used storage rule | M | 0.3 | Add. Without it a single full chest stalls a category. |
| 2 | **Uncategorised fallback container** | Any item whose type is not covered by a preset or list needs a home, or it is silently never sorted | S | 0.3 | Add. Pick one container per claim as "everything else". |
| 3 | **Auto-suggest the preset on first toggle** | Opening a chest of iron and choosing "Metals" is obvious; inferring it from contents saves the step | S | 0.3 | Add. Pre-select the dominant category, still editable. |
| 4 | **Snapshot preview before saving a custom list** | "Save contents" is irreversible otherwise; a mis-click fixes the wrong rule permanently | S | 0.3 | Add. Show the captured list with checkboxes. |
| 5 | **Copy rule to nearby / all containers of this type** | 20 chests of the same rule is 20 menus | M | 0.3 | Add, behind a confirm. |
| 6 | **Recent moves + undo in the ledger** | Mis-sorting is the top complaint about storage mods; an undo is cheaper than a bug report | M | 0.3–0.4 | Add if 0.3 has room. |
| 7 | **Blacklist this mod** from a container | One bad mod container otherwise reappears every world load | M | 0.3 | Add, but see the caveat below. |
| 8 | **Pause this container** (temporary) distinct from blacklist (permanent) | Moving a base should not require deleting rules | S | 0.3 | Add. |
| 9 | **Animation LOD** — only animate the nisse within N metres; distant work stays in the background | Ten nisses animating across a big base is wasted frame time | S | 0.3 | Add. |
| 10 | **Fair queue across containers** | One large dump must not starve machines or another category | M | 0.3 | Add. Round-robin per task type. |
| 11 | **Read-only badge** for players without rule permission | Makes the permission model visible instead of mysterious | S | 0.3 | Add. |
| 12 | **YAML rule export/import** | Backup, share a base's scheme, move a profile between worlds; YamlDotNet already ships | M | 0.4 | Consider. Cheap because the parser is already there. |
| 13 | **YAML-editable presets** | The survival presets will not suit every pack; editing a file beats a code change | M | 0.4 | Consider with #12. |
| 14 | **Rate presets** (2 / 5 / 10) with a per-claim override | One global number is blunt for a big base and a small hut | S | 0.1.2 | Add the presets; per-claim later. |
| 15 | **"Deposit all" from the container UI** — pull the player's matching items into the network | Complements Empty Pockets and matches QM's tool | S | 0.3 | Consider. |

## Technical caveat on #7

Prefabs do not record which plugin registered them, so "blacklist this mod" cannot be fully automatic.
The workable approach: keep a `containers.yaml` with prefab-name prefixes, ship defaults for the known
mod containers, and when a player blacklists a mod from a container, derive the prefix from that
prefab's name (everything before the first `_` or digit) and add it. It is a heuristic; the UI must say
which prefabs it is blocking.

## Rejected for now

- Per-container throughput — one rate per claim is easier to reason about and to explain.
- A full chest settings panel — the two-button container UI is the point; anything bigger belongs in the
  ledger.
