#!/usr/bin/env bash
# Wait for the shared Windows client to be free: valheim.exe gone, or idle long enough to say so.
#   usage: wait-client.sh <max-seconds>
# Prints one line per poll and exits 0 when the client looks free, 1 on timeout.
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
MAX="${1:-480}"
POLL="${2:-60}"

cat > /home/hermes/.hermes/cache/scratch/client-free.ps1 <<'PS'
$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if (-not $p) { Write-Output "client=free"; exit 0 }
$log = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
$mtime = if (Test-Path $log) { (Get-Item $log).LastWriteTime.ToString("HH:mm:ss") } else { "none" }
$size = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
Write-Output ("client=busy pid=" + ($p.Id -join ",") + " log=" + $mtime + "/" + $size)
PS

elapsed=0
while [ "$elapsed" -lt "$MAX" ]; do
  out=$(bash "$REPO/tooling/client-rig/psrun.sh" /home/hermes/.hermes/cache/scratch/client-free.ps1 2>&1 | tr -d '\000' | grep -a '^client=')
  echo "$(date +%H:%M:%S) $out"
  case "$out" in
    client=free) exit 0 ;;
  esac
  sleep "$POLL"
  elapsed=$((elapsed + POLL))
done
echo "timeout after ${MAX}s: client still busy"
exit 1
