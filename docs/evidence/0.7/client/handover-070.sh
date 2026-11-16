#!/usr/bin/env bash
# The 0.7 handover path on a real client: the client owns the chest but has it closed, so the nisse
# asks for it and the client hands it over.
#
#   usage: handover-070.sh <baseX> <deckY> <baseZ> <playerId> <out.cmd>
set -euo pipefail

X="${1:?baseX}"
Y="${2:?deckY}"
Z="${3:?baseZ}"
PID="${4:?playerId}"
OUT="${5:?out cmd}"
PY="$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")"
CX="$((X+2))"

{
  echo "# 0.7 handover: the client owns the chest and has closed it; the nisse must ask, be handed the"
  echo "# ZDO, and file the stack exactly once."
  echo "cleanup"
  echo "hold on"
  for dx in -4 -2 0 2 4; do
    for dz in -4 -2 0 2 4; do echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"; done
  done
  echo "spawn husnisse_bowl $X $PY $Z $PID"
  echo "spawn piece_chest_wood $CX $PY $Z"
  echo "wait 6"
  echo "nostruct"
  echo "console husnisse_rule net piece_chest_wood on"
  echo "console husnisse_rule set piece_chest_wood cat:wood"
  echo "wait 2"
  echo "husnisse claims"
  echo "expect_claim owner=$PID"

  # window: the client opens the chest and closes it again (t+14 .. t+44)
  echo "wait 30"

  echo "zdopos piece_chest_wood $CX $Z"
  echo "expect_last inuse=0"
  echo "pocketorder Wood 6"
  echo "wait 20"
  echo "zdopos piece_chest_wood $CX $Z"
  echo "expect_last items=6"
  echo "expect_last serverOwns=True"
  echo "husnisse status"
  echo "cleanup"
} > "$OUT"

echo "wrote $OUT"
