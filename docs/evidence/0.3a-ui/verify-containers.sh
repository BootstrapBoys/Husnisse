#!/usr/bin/env bash
# Read the client's own container list and judge the base: one chest per expected position,
# how many are on the network, how many carry a rule, and duplicates if any.
#   usage: verify-containers.sh <positions.txt> <raw output file>
set -uo pipefail
POS="${1:?positions}"
RAW="${2:?raw containers output}"
expected=$(wc -l < "$POS")
grep -a '^gc container piece_chest_wood' "$RAW" \
  | sed 's/^gc container piece_chest_wood(Clone) at //; s/ net=/\tnet=/; s/ rule=/\trule=/' \
  | sort > "$RAW.chests"
echo "lines=$(wc -l < "$RAW.chests") expected=$expected"
echo "duplicate positions:"
cut -f1 "$RAW.chests" | sort | uniq -d | sed 's/^/  /'
grep -a '^gc containers total=' "$RAW" | tail -1
