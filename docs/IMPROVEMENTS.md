# Improvements — decisions

Decided 2026-11-05. Accepted items are folded into the ladder at the version shown.

## Accepted

| # | Improvement | Where | Note |
|---|---|---|---|
| 1 | Priority among containers accepting the same category | 0.3 | Preferred first; no per-container overflow flag, Overflow is a category |
| 2 | **Overflow as a container category**, one or many chests | 0.3 | Takes items whose category chests are full **and** items no rule matches |
| 3 | Auto-suggest the preset from contents on first toggle | 0.3 | Pre-selects the dominant category, still editable |
| 4 | Snapshot preview with checkboxes before saving a list | 0.3 | Stores item names, not stacks |
| 5 | Copy a rule to nearby / same-type containers | 0.3 | Behind a confirm |
| 7 | **`husnisse_prefabs` command** replaces mod blacklisting | 0.1.2 | Dumps every container prefab to `BepInEx/BSBLogs/Husnisse-prefabs.txt`; blacklisting stays per container |
| 9 | Animation LOD by distance | 0.3 | Only the nearby nisse animates; distant work stays in the queue |
| 10 | Fair queue across containers | 0.3 | Round-robin per task type; one dump cannot starve machines |
| 11 | Read-only badge for players without rule permission | 0.3 | Same permission model as the rules |
| 12 | YAML rule export / import | 0.3.1 | Uses YamlDotNet; import is all-or-nothing |
| 13 | YAML-editable presets | 0.3.1 | Same file as the rules |
| 14 | Rate presets 2 / 5 / 10 | 0.1.2 | Per-claim override later |
| 15 | **Deposit chest** as a container category | 0.3 | Dump everything into it, the nisse sorts from it |

## Deposit-all behaviour (from #15)

- On a **network** container: deposit-all hands the player's items to the nisse, which sorts them to their
  proper containers at the configured rate.
- On an **off-network** container: deposit-all dumps every pocket item into that container with no sorting.
  That is the manual "dump it here and deal with it later" action.
- A **Deposit chest** is a category, like Overflow: one or many, receives everything, and is a source the
  nisse drains into the proper containers.

## Rejected

| # | Improvement | Reason |
|---|---|---|
| 6 | Recent moves + undo | Not this project; the ledger stays informational |
| 8 | Pause container | The network toggle already removes it; a second off state is redundant |
| — | Per-mod blacklist | Prefabs carry no source plugin; replaced by the prefab dump command |

## Rejected earlier

- Per-container throughput — one rate per claim is easier to reason about.
- A full chest settings panel — the two-button container UI plus the ledger stays the boundary.
