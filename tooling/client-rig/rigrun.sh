#!/usr/bin/env bash
# Run PowerShell on the Valheim Windows host (WinGSM, 192.168.1.136) without quoting roulette.
# The host's default SSH shell is already PowerShell, but double quotes do not survive the hop,
# so encode the script as UTF-16LE and pass -EncodedCommand instead.
#   usage: rigrun.sh <script.ps1>      (or - for stdin)
set -euo pipefail

KEY="${RIG_KEY:-$HOME/.ssh/valheim_server_ed25519}"
HOST="${RIG_HOST:-WinGSM@192.168.1.136}"

src="${1:--}"
if [ "$src" = "-" ]; then
  script="$(cat)"
else
  script="$(cat "$src")"
fi

b64="$(printf '%s' "$script" | iconv -f UTF-8 -t UTF-16LE | base64 -w0)"
ssh -i "$KEY" -o BatchMode=yes -o ConnectTimeout=15 -o ServerAliveInterval=15 \
    "$HOST" "powershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand $b64"
