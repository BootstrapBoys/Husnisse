#!/usr/bin/env bash
# The two-claim scene for the last two checks:
#   * a chest inside a bowl placed by ANOTHER player (creator 424242) — the strip must appear
#     with its controls read-only;
#   * a chest with NO claim at all (no bowl within 20 m) — the strip must not appear.
#
# Which one sits next to the player is decided by the rig, not by taste: ZNetScene destroys harness
# pieces that drift out of the client's radius (a chest 20 m away went within half a minute), so the
# chest whose reading the gate cannot get any other way — the read-only one — is the one the player
# is standing at. The no-claim chest sits 28 m out and is read second; the no-claim case is also
# proven independently in the p13b run (`client/logs/p13-open-noclaim.txt`).
#
#   usage: readonly-scene.sh <out.cmd> "<baseX> <deckY> <baseZ>"
#   indices: 0 = the 424242 bowl's chest at +(4,2), 1 = the no-claim chest at +(30,0)
set -euo pipefail
OUT="${1:?out cmd}"
BASE="${2:?base x y z}"
# shellcheck disable=SC2086
set -- $BASE
X="$1"; Y="$2"; Z="$3"
PY="$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")"

{
  echo "# 0.3b client gate: another player's claim (index 0) + no-claim chest (index 1)."
  echo "cleanup"
  echo "hold on"
  for dx in 0 2 4 6; do
    for dz in 0 2 4; do echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"; done
  done
  for dx in 28 30 32; do
    for dz in -2 0 2; do echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"; done
  done
  # index 0 (x+4): inside the claim of a bowl this player did not place
  echo "spawn husnisse_bowl $((X+2)) $PY $((Z+2)) 424242"
  echo "spawn piece_chest_wood $((X+4)) $PY $((Z+2))"
  # index 1 (x+30): no bowl anywhere near it -> no claim
  echo "spawn piece_chest_wood $((X+30)) $PY $((Z+0))"
  echo "wait 6"
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=424242"
  echo "husnisse status"
  # The mod's container count only covers containers inside a claim, and the no-claim chest is
  # deliberately in none: one claimed chest is the correct count for this scene.
  echo 'expect_ge containers=(\d+) 1'
} > "$OUT"
echo "wrote $OUT (424242 bowl at $((X+2)),$PY,$((Z+2)) + chest at $((X+4)),$PY,$((Z+2)); no-claim chest at $((X+30)),$PY,$((Z+0)))"
