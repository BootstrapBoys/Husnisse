#!/usr/bin/env bash
# Send one batch of probe commands to the client and print the probe's log lines.
#   usage: probe.sh "state" "where"        (each argument is one command line)
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"
OUT="${PROBE_TMP:-/home/hermes/.hermes/cache/scratch/husnisse-cmd.txt}"

: > "$OUT"
for line in "$@"; do printf '%s\n' "$line" >> "$OUT"; done

scp -q -i "$KEY" -o BatchMode=yes "$OUT" "$GUEST:C:/Users/Hermes/stage/cmd.txt"
bash "$REPO/tooling/client-rig/psrun.sh" "$HERE/send.ps1" >/dev/null
sleep 3
bash "$REPO/tooling/client-rig/psrun.sh" "$HERE/client-tail.ps1" 2>/dev/null |
    grep -a 'Gate Client Probe' | tail -40
