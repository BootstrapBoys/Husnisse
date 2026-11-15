#!/usr/bin/env bash
# A compact scene for the remaining 0.3b phases (p8-p13).
#
# Why this exists: the wide layout in scene.sh spreads the chests over ~27 m, and this rig's
# ZNetScene destroys harness pieces that fall outside the client's culling radius — during the
# p8-p13 session a chest 11.9 m from the player and the cart were gone within a minute, which
# breaks both the harness's position-sorted indices and any phase that needs that chest. Every
# piece here sits within ~5 m of the base, so the whole block stays inside the radius wherever the
# player stands. The roles, the network/rule setup and the generated indices are the same as
# scene.sh; only the spacing is smaller and the cart/outside chest (needed by p1, already proven)
# are dropped from this fixture.
#
#   usage: scene-compact.sh <baseX> <deckY> <baseZ> <out.cmd> <out.env>
set -euo pipefail

X="${1:?baseX}"
Y="${2:?deckY}"
Z="${3:?baseZ}"
OUT="${4:?out cmd}"
ENV_OUT="${5:?out env}"
PY="$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")"

# role dx dz  — bowl at (0,0) relative to the base; native claim radius 20 m.
#   panel    the chest the panel acts on (starts with a rule, so p8 can clear it)
#   copyA/B  its two same-prefab neighbours, on network, no rule (p9 targets)
#   metals   cat:metals     wood  cat:wood        overflow / deposit  the two roles
#   plain    on network, no rule (deposit-all target)   dump  off network (deposit-all target)
CHESTS=(
  "panel 2 0"
  "copyA 4 0"
  "copyB 2 2"
  "metals -2 -14"
  "wood 2 -14"
  "overflow 0 -16"
  "deposit -2 -12"
  "plain 2 -12"
  "dump 0 -10"
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
  echo "# 0.3b client gate scene (compact, p8-p13), generated at ($X, $Y, $Z)."
  echo "# bowl creator -202584743 (the joining player); role -> index map in gate-03b-indices.env."
  echo "cleanup"
  echo "hold on"

  # Deck: one rectangle covering both clusters (x -5..5, z -19..5, 2 m floors).
  for dx in -5 -3 -1 1 3 5; do
    for dz in -19 -17 -15 -13 -11 -9 -7 -5 -3 -1 1 3 5; do echo "spawn wood_floor $((X+dx)) $Y $((Z+dz))"; done
  done

  echo "spawn husnisse_bowl $X $PY $Z -202584743"
  for entry in "${CHESTS[@]}"; do
    set -- $entry
    echo "spawn piece_chest_wood $((X+$2)) $PY $((Z+$3))"
  done

  echo "wait 6"
  echo "zdo husnisse_bowl husnisse_bowl_init"
  echo "husnisse claims"
  echo "expect_claim owner=-202584743"
  echo "husnisse status"
  echo 'expect_ge containers=(\d+) 8'

  # panel starts with a rule so p8 has one to clear; everything else as in scene.sh.
  for role in copyA copyB metals wood overflow deposit plain; do
    echo "console husnisse_rule net piece_chest_wood on $(idx "$role")"
  done
  echo "console husnisse_rule set piece_chest_wood cat:metals $(idx metals)"
  echo "console husnisse_rule set piece_chest_wood cat:wood $(idx wood)"
  echo "console husnisse_rule set piece_chest_wood overflow $(idx overflow)"
  echo "console husnisse_rule set piece_chest_wood deposit $(idx deposit)"
  echo "console husnisse_rule set piece_chest_wood cat:wood $(idx panel)"
  echo "wait 2"
  echo "husnisse status"
} > "$OUT"

echo "wrote $OUT ($(grep -c '^spawn wood_floor' "$OUT") floor pieces, $(grep -c '^spawn piece_chest_wood' "$OUT") chests)"
cat "$ENV_OUT"
