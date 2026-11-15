#!/usr/bin/env bash
# Queue one scenario on the gate tree and print the lines it produced.
#
# The host rig lock exists so two cards cannot drive the same server; this tree (V:\Gate031) is only
# ever written by this card, so a scenario here needs no lock — which is what let the 0.3.1 client
# gate proceed while another card held the shared 2556 tree.
#
#   usage: s031.sh <file.cmd>
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="/home/hermes/projects/valheim/.worktrees/04-readonly"
KEY="$HOME/.ssh/valheim_server_ed25519"
SRV="WinGSM@192.168.1.136"

[ -f "$1" ] || { echo "no such scenario: $1"; exit 2; }

hcount() { bash "$REPO/tooling/client-rig/rigrun.sh" "$HERE/hcount.ps1" 2>/dev/null | tr -d '\000\r' | grep -E '^[0-9]+$' | tail -1; }

before=$(hcount); before=${before:-0}
scp -q -i "$KEY" "$1" "$SRV:V:/Gate031/BepInEx/config/harness.cmd" || { echo "scp failed"; exit 1; }

for i in $(seq 1 100); do
  sleep 3
  now=$(hcount); now=${now:-0}
  if [ "$now" -gt "$before" ]; then
    bash "$REPO/tooling/client-rig/rigrun.sh" "$HERE/htail.ps1" 2>/dev/null | tr -d '\000\r' | grep -v '^#<' | tail -50
    exit 0
  fi
done
echo "timeout waiting for scenario done (count $before -> $(hcount))"
exit 1
