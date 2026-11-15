#!/usr/bin/env bash
# Generate the 0.3a UI scene at the client's current ground position.
#   usage: make-scene.sh <baseX> <groundY> <baseZ>
set -euo pipefail
X="${1:--7}"; Y="${2:-34.07}"; Z="${3:--4}"
OUT="${4:-/home/hermes/projects/valheim/testrig/scenarios/husnisse-03a-ui-scene-generated.cmd}"

{
  echo "# 0.3a UI scene, generated for the client's ground position ($X, $Y, $Z)."
  echo "cleanup"
  echo "hold on"
  # 6 x 5 deck, 2 m spacing, top at the client's feet.
  for dx in -5 -3 -1 1 3 5; do
    for dz in -5 -3 -1 1 3; do
      echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"
    done
  done
  # bowl at the deck's corner, owned by the joining player.
  echo "spawn husnisse_bowl $((X-4)) $(awk "BEGIN{printf \"%.2f\", $Y+0.10}") $((Z-4)) -202584743"
  # 20 chests, 5 x 4, 2 m spacing.
  for dx in -3 -1 1 3 5; do
    for dz in -3 -1 1 3; do
      echo "spawn piece_chest_wood $((X+dx)) $(awk "BEGIN{printf \"%.2f\", $Y+0.10}") $((Z+dz))"
    done
  done
  # one chest outside the 20 m claim
  echo "spawn piece_chest_wood $((X+30)) $(awk "BEGIN{printf \"%.2f\", $Y+0.10}") $Z"
  echo "wait 5"
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=-202584743"
  echo "husnisse status"
  echo "expect_ge containers=(\\d+) 20"
} > "$OUT"
echo "wrote $OUT"
