#!/usr/bin/env bash
# Run PowerShell on the Windows client guest without quoting roulette.
# SSH lands in cmd.exe, so inner double quotes are stripped. Encode the script
# as UTF-16LE and pass it as -EncodedCommand instead.
#   usage: psrun.sh <script.ps1> [--raw]
#   echo 'Get-Date' | psrun.sh -        # read from stdin
set -euo pipefail

KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"

src="${1:--}"
if [ "$src" = "-" ]; then
  script="$(cat)"
else
  script="$(cat "$src")"
fi

b64="$(printf '%s' "$script" | iconv -f UTF-8 -t UTF-16LE | base64 -w0)"
ssh -i "$KEY" -o BatchMode=yes -o ConnectTimeout=15 -o ServerAliveInterval=15 \
    "$GUEST" "powershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand $b64"
