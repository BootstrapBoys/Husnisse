#!/usr/bin/env bash
# Send one probe batch to the client and print this batch's own log lines.
# Adds a unique comment line so the probe never skips an identical batch
# (RunCommandFile ignores text equal to the last batch it ran).
#   usage: pc.sh "state" "where"       (each argument is one command line)
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"
TAG="gate-$(date +%H%M%S)-$$"
OUT="${PROBE_TMP:-/home/hermes/.hermes/cache/scratch/husnisse-cmd.txt}"

: > "$OUT"
printf '# %s\n' "$TAG" >> "$OUT"
for line in "$@"; do printf '%s\n' "$line" >> "$OUT"; done

scp -q -i "$KEY" -o BatchMode=yes "$OUT" "$GUEST:C:/Users/Hermes/stage/cmd.txt" || { echo "scp failed"; exit 1; }
bash "$REPO/tooling/client-rig/psrun.sh" "$HERE/send.ps1" >/dev/null 2>&1
sleep "${PROBE_WAIT:-4}"
bash "$REPO/tooling/client-rig/psrun.sh" "$HERE/client-tail.ps1" 2>/dev/null \
  | grep -a 'Gate Client Probe' | sed 's/^.*Gate Client Probe\] //' | tail -"${PROBE_LINES:-60}"
