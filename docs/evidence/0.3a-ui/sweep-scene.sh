#!/usr/bin/env bash
# 0.3a UI sweep scene, generated at the client's ground position.
#   usage: sweep-scene.sh <baseX> <groundY> <baseZ> <out.cmd> <positions.txt>
# Writes the harness scenario and the chest coordinate list the driver walks (position-addressed,
# never index-addressed: the fixture world already holds containers and index order is not ours).
set -euo pipefail
X="${1:?baseX}"; Y="${2:?groundY}"; Z="${3:?baseZ}"
OUT="${4:?out cmd}"
POS="${5:?positions file}"

{
  echo "# 0.3a UI sweep scene: bowl (creator -202584743) + 20 chests, held against ZNetScene culling."
  echo "cleanup"
  echo "hold on"
  # Deck: 7 x 6 wood floors, 2 m spacing, top flush with the client's feet.
  for dx in -6 -4 -2 0 2 4 6; do
    for dz in -6 -4 -2 0 2 4; do
      echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"
    done
  done
  # Bowl at the deck's south-west corner, owned by the joining player: the claim follows the creator.
  echo "spawn husnisse_bowl $((X-4)) $(awk "BEGIN{printf \"%.2f\", $Y+0.10}") $((Z-4)) -202584743"
  # 20 chests, 5 x 4, 2 m spacing, well inside the 20 m native claim.
  : > "$POS"
  for dx in -2 0 2 4 6; do
    for dz in -2 0 2 4; do
      echo "spawn piece_chest_wood $((X+dx)) $(awk "BEGIN{printf \"%.2f\", $Y+0.10}") $((Z+dz))"
      printf '%s %s %s\n' "$((X+dx))" "$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")" "$((Z+dz))" >> "$POS"
    done
  done
  echo "wait 5"
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=-202584743"
  echo "husnisse status"
  # Single quotes: the harness wants a bare regex, and bash must not eat the backslash.
  echo 'expect_ge containers=(\d+) 20'
  echo 'expect_ge ruled=(\d+) 0'
} > "$OUT"
echo "wrote $OUT ($(grep -c '^spawn piece_chest_wood' "$OUT") chests) and $POS"
