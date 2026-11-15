#!/usr/bin/env bash
# One poll: is the shared client free, and what is the order-channel card doing?
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
cat > /home/hermes/.hermes/cache/scratch/client-free.ps1 <<'PS'
$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if (-not $p) { Write-Output "client=free"; exit 0 }
$log = "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log"
$mtime = if (Test-Path $log) { (Get-Item $log).LastWriteTime.ToString("HH:mm:ss") } else { "none" }
$size = if (Test-Path $log) { (Get-Item $log).Length } else { 0 }
Write-Output ("client=busy pid=" + ($p.Id -join ",") + " started=" + (Get-CimInstance Win32_Process -Filter "Name='valheim.exe'").CreationDate.ToString("HH:mm:ss") + " log=" + $mtime + "/" + $size)
PS
echo "$(date +%H:%M:%S) $(bash "$REPO/tooling/client-rig/psrun.sh" /home/hermes/.hermes/cache/scratch/client-free.ps1 2>&1 | tr -d '\000' | grep -a '^client=')"
python3 - "$@" <<'PY'
import sqlite3, time, sys
db = sqlite3.connect('/home/hermes/.hermes/kanban/boards/husnisse/kanban.db')
now = time.time()
for tid in ('t_9dc34cce',):
    status = db.execute('select status from tasks where id=?', (tid,)).fetchone()
    hb = db.execute("select max(created_at) from task_events where task_id=? and kind='heartbeat'", (tid,)).fetchone()
    print(f"{tid} status={status[0] if status else '?'} last-heartbeat={int(now-(hb[0] or 0))}s ago")
PY
