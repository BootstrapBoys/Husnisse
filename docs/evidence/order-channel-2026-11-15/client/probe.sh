#!/usr/bin/env bash
# Send one command batch to the client probe and print what was sent.
#   probe.sh "state"              (single line)
#   probe.sh -f batches/b01.cmd   (file)
# The probe polls %USERPROFILE%\husnisse-probe.cmd every 0.5 s; identical text is skipped,
# so make every batch text different (a trailing comment line does it).
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../../.." && pwd)"
KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"

if [ "${1:-}" = "-f" ]; then
  src="$2"
else
  src="$(mktemp)"
  printf '%s\n' "$*" > "$src"
fi

scp -q -i "$KEY" -o BatchMode=yes "$src" "$GUEST:C:/Users/Hermes/stage/cmd.txt"
bash "$ROOT/tooling/client-rig/psrun.sh" "$HERE/send.ps1"
