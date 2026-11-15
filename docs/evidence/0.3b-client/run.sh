#!/usr/bin/env bash
# Run one mode of tools.ps1 on the client guest.
#
# psrun.sh inlines a script over SSH and cannot pass arguments, so the script is copied to the
# guest's stage folder once per invocation and a tiny wrapper dot-sources it with the parameters.
#   usage: run.sh <mode> [extra powershell args]
#   e.g.   run.sh batch -Wait 5
#          run.sh tail -Lines 40
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"
WRAP="${PROBE_WRAP:-$HOME/.hermes/cache/scratch/client-run.ps1}"
CACHE="${TOOLS_CACHE:-$HOME/.hermes/cache/scratch/tools-cached.txt}"

MODE="${1:?usage: run.sh <mode> [args]}"
shift || true

# Push the helper only when its text changed, so a batch costs one SSH round trip in the common case.
SUM="$(sha256sum "$HERE/tools.ps1" | cut -d' ' -f1)"
if [ ! -f "$CACHE" ] || [ "$(cat "$CACHE")" != "$SUM" ]; then
  scp -q -i "$KEY" -o BatchMode=yes "$HERE/tools.ps1" "$GUEST:C:/Users/Hermes/stage/tools.ps1" || {
    echo "run.sh: could not push tools.ps1"; exit 1; }
  printf '%s' "$SUM" > "$CACHE"
fi

{
  printf '. "C:\\Users\\Hermes\\stage\\tools.ps1" -Mode %s' "$MODE"
  for arg in "$@"; do printf ' %s' "$arg"; done
  printf '\n'
} > "$WRAP"

bash "$REPO/tooling/client-rig/psrun.sh" "$WRAP" 2>&1 | tr -d '\000' | sed '/^#< CLIXML/d' | grep -v '^<Objs '
