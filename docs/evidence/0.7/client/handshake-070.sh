#!/usr/bin/env bash
# Generate the 0.7 handshake scenario: fixture, then the two client windows, with the server-side
# readings at each phase. The client is driven from the shell against the fixed timeline this file
# produces.
#
#   usage: handshake-070.sh <baseX> <deckY> <baseZ> <playerId> <out.cmd>
set -euo pipefail

X="${1:?baseX}"
Y="${2:?deckY}"
Z="${3:?baseZ}"
PID="${4:?playerId}"
OUT="${5:?out cmd}"
PY="$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")"
CX="$((X+2))"

{
  echo "# 0.7 shared-chest handshake: one bowl the joining player owns, one chest two metres away."
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
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=$PID"

  # ---- window 1: the client opens the chest here (t+16 .. t+39).
  echo "zdopos piece_chest_wood $CX $Z"
  echo "wait 25"

  # ---- the client owns it now; the nisse must ask and be refused.
  echo "zdopos piece_chest_wood $CX $Z"
  echo "expect_last inuse=1"
  echo "pocketorder Wood 6"
  echo "wait 14"
  echo "zdopos piece_chest_wood $CX $Z"
  echo "expect_last items=0"
  echo "husnisse status"
  echo "expect [chest]"

  # ---- window 2: the client closes the chest here (t+60 .. t+92).
  echo "wait 35"
  echo "wait 15"

  # ---- the handover happened: the same stack, once, in a chest the server owns.
  echo "zdopos piece_chest_wood $CX $Z"
  echo "expect_last items=6"
  echo "expect_last serverOwns=True"
  echo "husnisse status"

  echo "cleanup"
} > "$OUT"

echo "wrote $OUT"
