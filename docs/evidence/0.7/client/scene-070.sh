#!/usr/bin/env bash
# 0.7 client gate scene: one bowl the joining player created, one chest two metres away.
#
# Compact on purpose: this rig's ZNetScene destroys harness pieces outside the client's culling
# radius (5-12 m observed), so everything sits inside 5 m of the bowl. The bowl carries the joining
# player's id as creator, so the claim belongs to the client and the chest is a chest a player can
# open — which is the whole point of the handshake test.
#
#   usage: scene-070.sh <baseX> <deckY> <baseZ> <playerId> <out.cmd>
set -euo pipefail

X="${1:?baseX}"
Y="${2:?deckY}"
Z="${3:?baseZ}"
PID="${4:?playerId}"
OUT="${5:?out cmd}"
PY="$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")"

{
  echo "# 0.7 shared-chest gate scene, generated at ($X, $Y, $Z); bowl creator $PID."
  echo "cleanup"
  echo "hold on"
  for dx in -4 -2 0 2 4; do
    for dz in -4 -2 0 2 4; do echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"; done
  done
  echo "spawn husnisse_bowl $X $PY $Z $PID"
  echo "spawn piece_chest_wood $((X+2)) $PY $Z"
  echo "wait 6"
  echo "nostruct"
  echo "console husnisse_rule net piece_chest_wood on"
  echo "console husnisse_rule set piece_chest_wood cat:wood"
  echo "wait 2"
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=$PID"
  echo "zdo piece_chest_wood husnisse_rule"
  echo "husnisse status"
  echo "expect_ge containers=(\\d+) 1"
} > "$OUT"

echo "wrote $OUT"
