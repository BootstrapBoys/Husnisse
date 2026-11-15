#!/usr/bin/env bash
# 0.3a UI gate — 20-container sweep, one chest at a time, identity by position.
#
# Reads the chest coordinates written by sweep-scene.sh and, for each chest:
#   [close + stand + teleport] -> [open that chest] -> [network toggle, rule panel, category,
#   close panel, read the ZDO back].
#
# Movement is `teleport` (Player.TeleportTo, which moves the rigidbody); `stand`'s bare
# transform write is reverted by the body and only used here as an identity probe — it prints the
# container's own coordinates, which are asserted against the scene's list before the chest is
# touched. The chest is then opened by index, because a 2 m grid makes "nearest" ambiguous.
# The final `containers` dump is the position-addressed end state.
#
#   usage: sweep-driver.sh <positions.txt> <logdir>
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POS="${1:?positions file}"
LOG="${2:?log dir}"
mkdir -p "$LOG"

CATS=(Metals Wood Food Trophies Valuables Building Consumables Tools)
mapfile -t CHESTS < "$POS"
total=${#CHESTS[@]}
n=0; ok=0; fail=0
: > "$LOG/sweep.txt"

for line in "${CHESTS[@]}"; do
  read -r x y z <<< "$line"
  [ -z "${x:-}" ] && continue
  cat="${CATS[$((n % 8))]}"
  lower=$(echo "$cat" | tr 'A-Z' 'a-z')
  i=$((n))
  n=$((n + 1))
  printf '=== chest %02d at %s,%s,%s (index %d) -> %s\n' "$n" "$x" "$y" "$z" "$i" "$cat" | tee -a "$LOG/sweep.txt"

  bash "$HERE/pc.sh" "close" "stand piece_chest_wood $i" < /dev/null > "$LOG/chest-$n-move.txt" 2>&1
  bash "$HERE/pc.sh" "open piece_chest_wood $i" < /dev/null > "$LOG/chest-$n-open.txt" 2>&1
  bash "$HERE/pc.sh" "click HusnisseNet" "click HusnisseRules" "click HusnisseCat$cat" \
                     "click HusnisseRulesClose" "ui" < /dev/null > "$LOG/chest-$n-click.txt" 2>&1

  standline=$(grep -a '^gc stand piece_chest_wood' "$LOG/chest-$n-move.txt" | tail -1)
  moveline=$(grep -a '^gc teleport ' "$LOG/chest-$n-move.txt" | tail -1)
  openline=$(grep -a '^gc open gui=' "$LOG/chest-$n-open.txt" | tail -1)
  zdoline=$(grep -a 'zdo net=' "$LOG/chest-$n-click.txt" | tail -1)
  uiline=$(grep -a 'ui container=' "$LOG/chest-$n-click.txt" | tail -1)
  { echo "  $standline"; echo "  $moveline"; echo "  $openline"; echo "  $zdoline"; echo "  $uiline"; } | tee -a "$LOG/sweep.txt"

  if echo "$standline" | grep -qF "at $x,$y,$z" &&
     echo "$openline"  | grep -q 'gui=piece_chest_wood' &&
     echo "$zdoline"   | grep -q "net=True rule='v1;k=cat;c=$lower'" &&
     echo "$uiline"    | grep -q "rules='Rules: $cat'"; then
    ok=$((ok + 1))
  else
    fail=$((fail + 1))
    echo "  !! chest $n ($x,$z) did not read back as net=True rule=cat:$lower" | tee -a "$LOG/sweep.txt"
  fi
done

# Close the last window, then let the client say what it sees across the whole base.
bash "$HERE/pc.sh" "close" < /dev/null > "$LOG/final-close.txt" 2>&1
bash "$HERE/pc.sh" "containers" < /dev/null > "$LOG/containers.txt" 2>&1
grep -a '^gc containers total=' "$LOG/containers.txt" | tail -1 | tee -a "$LOG/sweep.txt"

echo "sweep: $n/$total chests, $ok confirmed, $fail unconfirmed" | tee -a "$LOG/sweep.txt"
[ "$fail" -eq 0 ] && [ "$n" -eq "$total" ]
