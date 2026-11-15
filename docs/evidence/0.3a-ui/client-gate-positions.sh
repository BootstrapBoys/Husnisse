#!/usr/bin/env bash
# 0.3a UI gate, position-addressed: 20 chests at known coordinates, the same order the scene wrote.
#   usage: client-gate-positions.sh
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROBE="$HERE/probe.sh"

CATS=(Metals Wood Food Trophies Valuables Building Consumables Tools)
n=0
for x in 27 29 31 33 35; do
  for z in -7 -5 -3 -1; do
    cat="${CATS[$((n % 8))]}"
    bash "$PROBE" "close" "teleport $(awk "BEGIN{printf \"%.2f\", $x+1.2}") 32.6 $z" "open piece_chest_wood" >/dev/null
    bash "$PROBE" "click HusnisseNet" "click HusnisseRules" "click HusnisseCat$cat" "click HusnisseRulesClose" "ui" |
      grep -a -E "after HusnisseCat$cat zdo|after HusnisseCat$cat ui" | tail -2
    echo "  chest ($x,$z) -> $cat"
    n=$((n + 1))
  done
done
echo "configured $n chests"
