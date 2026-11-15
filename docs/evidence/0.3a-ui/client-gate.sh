#!/usr/bin/env bash
# 0.3a client UI gate: the container buttons, the rule panel, and 20 containers through the UI.
# Runs the probe batches in order and prints the probe's observations after each.
#   usage: client-gate.sh [stage]      stages: basics | twenty | relog | all
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
PROBE="$HERE/probe.sh"
STAGE="${1:-all}"

shot() { bash "$PROBE" "shot $1" >/dev/null; }

basics() {
  echo "== 1. stand at chest 0, open it, look at the buttons =="
  bash "$PROBE" "heal" "god on" "stand piece_chest_wood 0" "open piece_chest_wood 0"
  bash "$PROBE" "ui"
  bash "$PROBE" "shot gate-03a-chest-window" >/dev/null

  echo "== 2. toggle the network from the button =="
  bash "$PROBE" "click HusnisseNet" "ui"

  echo "== 3. open the rule panel and pick Metals =="
  bash "$PROBE" "click HusnisseRules" "ui"
  bash "$PROBE" "shot gate-03a-rule-panel" >/dev/null
  bash "$PROBE" "click HusnisseCatMetals" "ui"
  bash "$PROBE" "click HusnisseRulesClose" "ui"

  echo "== 4. a second container gets Overflow, to prove the panel is per container =="
  bash "$PROBE" "close" "stand piece_chest_wood 1" "open piece_chest_wood 1"
  bash "$PROBE" "click HusnisseNet" "click HusnisseRules" "click HusnisseOverflow" "ui"
  bash "$PROBE" "click HusnisseRulesClose" "close"
}

twenty() {
  echo "== 5. every container through the UI: toggle on, pick a category =="
  local cats=(Metals Wood Food Trophies Valuables Building Consumables Tools)
  local n=0
  for i in $(seq 2 19); do
    local cat="${cats[$((i % 8))]}"
    bash "$PROBE" "stand piece_chest_wood $i" "open piece_chest_wood $i" >/dev/null
    bash "$PROBE" "click HusnisseNet" "click HusnisseRules" "click HusnisseCat$cat" "click HusnisseRulesClose" "close" |
      grep -a -E "click HusnisseNet invoked|after HusnisseCat$cat ui|click HusnisseCat$cat invoked" | tail -3
    n=$((n + 1))
    echo "  container $i -> $cat (${n}/18)"
  done
  bash "$PROBE" "stand piece_chest_wood 0" "open piece_chest_wood 0"
  bash "$PROBE" "ui"
}

relog() {
  echo "== 6. quit and rejoin: the rules are the world's, not the client's =="
  bash "$PROBE" "close" "quit"
  sleep 15
  cd "$REPO"
  bash tooling/client-rig/psrun.sh "$HERE/reset-cmd.ps1" >/dev/null
  bash tooling/client-rig/psrun.sh "$HERE/../client-gate-2026-11-15/launch-client.ps1" >/dev/null
  for i in $(seq 1 10); do
    local ready
    ready=$(bash tooling/client-rig/psrun.sh "$HERE/client-log-grep.ps1" 2>/dev/null | grep -a 'gc ready' | tail -1)
    [ -n "$ready" ] && { echo "$ready"; break; }
    sleep 20
  done
  bash "$PROBE" "heal" "god on" "stand piece_chest_wood 0" "open piece_chest_wood 0"
  bash "$PROBE" "ui"
  bash "$PROBE" "shot gate-03a-after-relog" >/dev/null
  bash "$PROBE" "close"
}

case "$STAGE" in
  basics) basics ;;
  twenty) twenty ;;
  relog) relog ;;
  all) basics; twenty; relog ;;
  *) echo "usage: client-gate.sh [basics|twenty|relog|all]"; exit 2 ;;
esac
