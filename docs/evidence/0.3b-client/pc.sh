#!/usr/bin/env bash
# Send one probe batch to the client and print ONLY the lines that batch produced.
#
# The batch is a comment line (a unique tag, so the probe never skips an identical batch) followed
# by one probe command per line. tools.ps1 records the log length before the send and reads the
# delta afterwards, so a late line from an earlier batch cannot be mistaken for this one.
#
#   usage: pc.sh "ui" "panel"             (each argument is one command line)
#   env:   PROBE_WAIT=8 pc.sh "click HusnisseNet"
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"
TAG="gate-$(date +%H%M%S)-$$"
OUT="${PROBE_TMP:-$HOME/.hermes/cache/scratch/husnisse-cmd.txt}"
WAIT="${PROBE_WAIT:-5}"

: > "$OUT"
printf '# %s\n' "$TAG" >> "$OUT"
for line in "$@"; do printf '%s\n' "$line" >> "$OUT"; done

scp -q -i "$KEY" -o BatchMode=yes "$OUT" "$GUEST:C:/Users/Hermes/stage/cmd.txt" || { echo "pc.sh: scp failed"; exit 1; }
bash "$HERE/run.sh" batch -Wait "$WAIT"
