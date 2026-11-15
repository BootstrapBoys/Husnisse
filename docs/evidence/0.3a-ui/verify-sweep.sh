#!/usr/bin/env bash
# Judge a finished sweep: every expected chest position against the client's own final container
# list (position -> net/rule), plus the per-chest stand line that proves which object was touched.
#   usage: verify-sweep.sh <positions.txt> <logdir>
# The per-chest `zdo` line in the click log is a same-frame read and can precede the server ack;
# the containers dump taken after the sweep is the settled state, so it is the source of truth.
set -uo pipefail
POS="${1:?positions}"
LOG="${2:?log dir}"
CATS=(Metals Wood Food Trophies Valuables Building Consumables Tools)
FINAL="$LOG/containers.txt"
n=0; ok=0; bad=0
printf '%-3s %-20s %-12s %-26s %s\n' '#' 'chest at' 'wanted' 'final net/rule' 'stand line'
while read -r x y z; do
  [ -z "${x:-}" ] && continue
  n=$((n + 1))
  cat="${CATS[$(( (n - 1) % 8 ))]}"
  lower=$(echo "$cat" | tr 'A-Z' 'a-z')
  key=$(printf '%.2f,%s,%.2f' "$x" "$y" "$z")

  found=$(grep -a "^gc container piece_chest_wood(Clone) at $key " "$FINAL" | tail -1 \
          | sed "s/.*net=\([A-Za-z]*\) rule='\([^']*\)'.*/\1 \2/")
  stand=$(grep -a '^gc stand piece_chest_wood' "$LOG/chest-$n-move.txt" | tail -1 | sed 's/^gc stand piece_chest_wood(Clone)//; s/ -> player.*//')

  verdict=OK
  [ "$found" = "True v1;k=cat;c=$lower" ] || { verdict=FAIL; bad=$((bad + 1)); }
  [ "$verdict" = "OK" ] && ok=$((ok + 1))
  printf '%-3s %-20s %-12s %-26s %s  %s\n' "$n" "$key" "$cat" "$found" "$stand" "$verdict"
done < "$POS"
echo "verified=$ok failed=$bad of $n"
grep -a '^gc containers total=' "$FINAL" | tail -1
[ "$bad" -eq 0 ]
