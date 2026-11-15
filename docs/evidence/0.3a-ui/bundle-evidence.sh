#!/usr/bin/env bash
# Assemble the 0.3a 20-container UI sweep evidence into one text file: the per-chest table, the
# client's own container dumps before/after, the server's write log and the relog read-back.
#   usage: bundle-evidence.sh <positions> <logdir> <out.txt>
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POS="${1:?positions}"
LOG="${2:?log dir}"
OUT="${3:?out file}"
SWEEP="$(dirname "$POS")"

{
  echo "0.3a UI gate — 20-container sweep, one chest at a time through the client's own UI"
  echo "================================================================================"
  echo "build      Husnisse 0.3.0, client+server sha256 e92f919a… ; probe ad20ed16… (gate client)"
  echo "scene      fixture restored, then one build: bowl creator -202584743 at (-4,-4), 20 chests"
  echo "           5 x 4 grid x=-2..6, z=-2..4 step 2, y=34.17, claim r=20, harness hold on"
  echo "driver     sweep-driver.sh: stand <chest i> (asserts the container's own coordinates),"
  echo "           open <chest i>, click HusnisseNet + HusnisseRules + HusnisseCat<X> +"
  echo "           HusnisseRulesClose, read the ZDO back; 20 chests, 8 categories cycling"
  echo
  echo "per-chest verification (final container list by position)"
  echo "---------------------------------------------------------"
  bash "$HERE/verify-sweep.sh" "$POS" "$LOG"
  echo
  echo "the base before the sweep: harness build of the scene (claim + zero rules)"
  echo "-----------------------------------------------------------------------"
  cat "$SWEEP/scene-build.txt" 2>/dev/null || echo "(scene-build.txt missing)"
  echo
  echo "client's own container list AFTER the sweep (the acceptance line)"
  echo "----------------------------------------------------------------"
  grep -a '^gc containers total=' "$LOG/containers.txt" | tail -1
  echo
  echo "client's own container list AFTER a full client relog"
  echo "----------------------------------------------------"
  grep -a '^gc containers total=' "$SWEEP/containers-relog.txt" | tail -1
  echo
  echo "server writes (last 20 [Rules] rule written lines = this sweep, in order)"
  echo "----------------------------------------------------------------------"
  cat "$SWEEP/server-rules-sweep.txt"
  echo
  echo "UI clicks the client actually invoked (button.onClick, counted over the 20 chests)"
  echo "--------------------------------------------------------------------------------"
  for verb in HusnisseNet HusnisseRules HusnisseRulesClose; do
    printf '  click %-22s invoked: %s\n' "$verb" "$(grep -ah "click $verb invoked" "$LOG"/chest-*-click.txt | wc -l)"
  done
  printf '  click HusnisseCat<X>        invoked: %s\n' "$(grep -ahE 'click HusnisseCat[A-Za-z]+ invoked' "$LOG"/chest-*-click.txt | wc -l)"
  printf '  distinct categories clicked        : %s\n' "$(grep -ahoE 'click HusnisseCat[A-Za-z]+ invoked' "$LOG"/chest-*-click.txt | sort -u | wc -l)"
  echo
  echo "relog read-back (client quit, character saved, rejoined, one chest opened)"
  echo "------------------------------------------------------------------------"
  grep -a 'gc stand piece_chest_wood' "$SWEEP/relog-read.txt" 2>/dev/null | tail -1
  grep -a 'ui container=' "$SWEEP/relog-read.txt" 2>/dev/null | tail -1
  grep -a 'ui zdo' "$SWEEP/relog-read.txt" 2>/dev/null | tail -1
} > "$OUT"
echo "wrote $OUT ($(wc -l < "$OUT") lines)"
