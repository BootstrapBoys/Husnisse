#!/usr/bin/env bash
# The 0.3b client-gate scene: one claim owned by the client's player, ten chests with the roles the
# gate needs, and a cart.
#
# The chests sit in a compact block around the client's own position, because this rig's ZNetScene
# destroyed harness pieces that ended up far from the player when the client was disconnected (the
# first attempt lost the three chests at x+13..x+30). Every chest is within ~20 m of the base.
#
# The harness addresses containers by position (x, then z), so each role's index depends on the
# layout; this script computes that order and writes both the spawn script and a map the driver
# sources, instead of hard-coding indices that could silently point at the wrong chest.
#
#   usage: scene.sh <baseX> <deckY> <baseZ> <out.cmd> <out.env>
set -euo pipefail

X="${1:?baseX}"
Y="${2:?deckY}"
Z="${3:?baseZ}"
OUT="${4:?out cmd}"
ENV_OUT="${5:?out env}"
PY="$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")"

# role dx dz  — bowl at (-2,-2) relative to the base, native claim radius 20 m.
#   panel    the chest the panel acts on          copyA/B  its two same-prefab neighbours (2 m)
#   metals   cat:metals     wood  cat:wood        overflow / deposit  the two roles
#   plain    on network, no rule (deposit-all target)   dump  off network (deposit-all target)
#   outside  beyond the 20 m claim (no buttons at all)
CHESTS=(
  "panel 2 2"
  "copyA 4 2"
  "copyB 2 4"
  "metals 2 -9"
  "wood 12 -3"
  "overflow 11 -8"
  "deposit 4 -10"
  "dump -8 6"
  "plain -8 -2"
  "outside 19 2"
)

# Index the roles the way the harness sorts: by x, then z.
TMP="$(mktemp)"
for entry in "${CHESTS[@]}"; do
  set -- $entry
  printf '%d %d %s\n' "$((X+$2))" "$((Z+$3))" "$1" >> "$TMP"
done
SORTED=$(sort -n -k1,1 -k2,2 "$TMP")
rm -f "$TMP"

: > "$ENV_OUT"
index=0
while read -r cz cx role; do
  printf 'IDX_%s=%d\n' "$role" "$index" >> "$ENV_OUT"
  index=$((index + 1))
done <<< "$SORTED"

idx() { sed -n "s/^IDX_$1=//p" "$ENV_OUT"; }

{
  echo "# 0.3b client gate scene, generated at the client's own position ($X, $Y, $Z)."
  echo "# bowl creator -202584743 (the joining player); role -> index map in gate-03b-indices.env."
  echo "cleanup"
  echo "hold on"

  # Deck: one rectangle under the whole block (x -9..19, z -11..5, 2 m floors).
  for dx in -9 -7 -5 -3 -1 1 3 5 7 9 11 13 15 17 19; do
    for dz in -11 -9 -7 -5 -3 -1 1 3 5; do echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"; done
  done

  echo "spawn husnisse_bowl $((X-2)) $PY $((Z-2)) -202584743"
  for entry in "${CHESTS[@]}"; do
    set -- $entry
    echo "spawn piece_chest_wood $((X+$2)) $PY $((Z+$3))"
  done
  echo "spawn Cart $((X-6)) $PY $((Z+2))"

  echo "wait 6"
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=-202584743"
  echo "husnisse status"
  echo 'expect_ge containers=(\d+) 9'

  # Network and rules for every chest the UI is NOT about to configure: panel and dump stay off,
  # outside stays off (it is outside the claim anyway).
  for role in copyA copyB metals wood overflow deposit plain; do
    echo "console husnisse_rule net piece_chest_wood on $(idx "$role")"
  done
  echo "console husnisse_rule set piece_chest_wood cat:metals $(idx metals)"
  echo "console husnisse_rule set piece_chest_wood cat:wood $(idx wood)"
  echo "console husnisse_rule set piece_chest_wood overflow $(idx overflow)"
  echo "console husnisse_rule set piece_chest_wood deposit $(idx deposit)"
  echo "wait 2"
  echo "husnisse status"
} > "$OUT"

echo "wrote $OUT ($(grep -c '^spawn wood_floor' "$OUT") floor pieces, $(grep -c '^spawn piece_chest_wood' "$OUT") chests)"
cat "$ENV_OUT"
