#!/usr/bin/env bash
# A compact 0.3.1 fixture: one bowl and four chests on a small deck, built at the client's feet.
#
# Why compact: this rig's ZNetScene destroys harness pieces that fall outside the client's culling
# radius (5-12 m observed in the 0.3b gate), so every piece sits within ~5 m of the bowl. The
# bowl carries the joining player's id as creator, so the client owns the claim and the ledger's
# CanEdit path is the one a player gets.
#
# Roles (and the harness/probe index order: sorted by x, then z):
#   wood    -2,0   taught a sample (custom list)              index 0
#   spare    0,-2  unclassified, untouched                    index 1
#   control  0,2   the no-teach control / ledger net+blacklist index 2
#   copper   2,0   taught a sample, then reclassified to Metals  index 3
#
#   usage: scene-031.sh <baseX> <deckY> <baseZ> <playerId> <out.cmd> <out.env>
set -euo pipefail

X="${1:?baseX}"
Y="${2:?deckY}"
Z="${3:?baseZ}"
PID="${4:?playerId}"
OUT="${5:?out cmd}"
ENV_OUT="${6:?out env}"
PY="$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")"

CHESTS=(
  "copper 2 0"
  "wood -2 0"
  "control 0 2"
  "spare 0 -2"
)

# Index the roles the way the harness and the probe sort: by x, then z.
TMP="$(mktemp)"
for entry in "${CHESTS[@]}"; do
  set -- $entry
  printf '%d %d %s\n' "$((X+$2))" "$((Z+$3))" "$1" >> "$TMP"
done
SORTED=$(sort -n -k1,1 -k2,2 "$TMP")
rm -f "$TMP"

: > "$ENV_OUT"
index=0
while read -r cx cz role; do
  printf 'IDX_%s=%d CHEST_%s="%s %s"\n' "$role" "$index" "$role" "$cx" "$cz" >> "$ENV_OUT"
  index=$((index + 1))
done <<< "$SORTED"

idx() { sed -n "s/^IDX_$1=//p" "$ENV_OUT"; }

{
  echo "# 0.3.1 client gate scene, generated at ($X, $Y, $Z); bowl creator $PID."
  echo "# role -> index map in gate-031-indices.env."
  echo "cleanup"
  echo "hold on"

  for dx in -4 -2 0 2 4; do
    for dz in -4 -2 0 2 4; do echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"; done
  done

  echo "spawn husnisse_bowl $X $PY $Z $PID"
  for entry in "${CHESTS[@]}"; do
    set -- $entry
    echo "spawn piece_chest_wood $((X+$2)) $PY $((Z+$3))"
  done

  echo "wait 6"
  echo "nostruct"
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=$PID"
  echo "husnisse status"
  echo 'expect_ge containers=(\d+) 4'
} > "$OUT"

echo "wrote $OUT ($(grep -c '^spawn wood_floor' "$OUT") floor pieces, $(grep -c '^spawn piece_chest_wood' "$OUT") chests)"
cat "$ENV_OUT"
