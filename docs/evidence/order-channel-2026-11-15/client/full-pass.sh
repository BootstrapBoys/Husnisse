#!/usr/bin/env bash
# One unattended attempt at the two client checks: take the client, install this card's build
# and probe, join 2856, build the scene at whatever position the client reports, then run the
# Tidy + Empty Pockets batches. Writes everything to /tmp/full-pass.log.
#
#   ./full-pass.sh            (only when the client is free: it quits any running instance)
#
# The order matters: install refuses while valheim.exe is running (the DLL is mapped).
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
NISSE="$(cd "$HERE/../../../.." && pwd)"
KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"
KEY_HOST="$HOME/.ssh/valheim_server_ed25519"
CLOG='C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/LogOutput.log'
WANT_BUILD=165f728beb32aefe39e07f1825c11728a3ca44c56201476f0e4d1c9197a8925d
LOG=/tmp/full-pass.log

say() { echo "[$(date +%H:%M:%S)] $*" | tee -a "$LOG"; }

client_ps() {
  timeout 40 bash "$NISSE/tooling/client-rig/psrun.sh" - 2>/dev/null
}

last_ready() {
  scp -q -i "$KEY" -o BatchMode=yes "$GUEST:$CLOG" "$HERE/logs/client-bepinex-orders.log" 2>/dev/null || return 0
  grep -a "gc ready" "$HERE/logs/client-bepinex-orders.log" | tail -1
}

: > /tmp/full-pass.log
say "== pass start =="

# 1. what is running?
info=$(client_ps <<'EOF'
$p = @(Get-Process valheim -ErrorAction SilentlyContinue)
"count=" + $p.Count
($p | ForEach-Object { "pid=" + $_.Id }) -join ' '
(Get-CimInstance Win32_Process -Filter "Name='valheim.exe'").CommandLine
EOF
)
say "client: $(echo "$info" | tr -d '\r' | tr '\n' ' ')"

if echo "$info" | grep -q "count=0"; then
  say "client idle"
else
  if echo "$info" | grep -q "joinserverwithcharacter 192.168.1.136:2856"; then
    say "already joined to 2856 - restarting it anyway so the installed build is ours"
  fi
  printf 'quit\n# full-pass %s\n' "$(date +%s)" > /tmp/quit2.cmd
  bash "$HERE/probe.sh" -f /tmp/quit2.cmd >/dev/null 2>&1 || say "quit send failed"
  for i in $(seq 1 20); do
    sleep 3
    n=$(client_ps <<'EOF'
"count=" + (@(Get-Process valheim -ErrorAction SilentlyContinue).Count)
EOF
)
    case "$n" in *count=0*) say "client closed after ${i}x3 s"; break;; esac
    [ "$i" = 20 ] && { say "FATAL: client would not close"; exit 1; }
  done
fi

# 2. install this card's build + probe
say "== install =="
timeout 150 bash "$NISSE/tooling/client-rig/psrun.sh" "$HERE/install-orders-165f728b.ps1" 2>&1 | tr -d '\r' | grep -E "Husnisse.dll|HusnisseGateClientProbe.dll|Error|running" | tee -a /tmp/full-pass.log
installed=$(client_ps <<'EOF'
(Get-FileHash -Algorithm SHA256 -LiteralPath "C:\Program Files (x86)\Steam\steamapps\common\Valheim\BepInEx\plugins\BootstrapBoys-Husnisse\Husnisse.dll").Hash
EOF
)
installed=$(printf '%s' "$installed" | grep -oiE '[0-9a-f]{64}' | head -1)
say "installed Husnisse.dll = $installed"
if [ "$(printf '%s' "$installed" | tr 'A-F' 'a-f')" != "$WANT_BUILD" ]; then say "FATAL: wrong build installed"; exit 1; fi

# 3. launch, join 2856
before="$(last_ready)"
say "ready line before launch: ${before:-<none>}"
say "== launch =="
client_ps <<'EOF' >/dev/null 2>&1
Set-Content -Path (Join-Path $env:USERPROFILE 'husnisse-probe.cmd') -Value "# full-pass start" -Encoding ASCII
Start-ScheduledTask -TaskName "HusnisseOrdersLaunch"
EOF

ready=""
for i in $(seq 1 45); do
  sleep 5
  now="$(last_ready)"
  if [ -n "$now" ] && [ "$now" != "$before" ]; then ready="$now"; break; fi
  [ $((i % 6)) = 0 ] && say "waiting for gc ready (${i}x5 s)"
done
if [ -z "$ready" ]; then say "FATAL: client never reported gc ready"; exit 1; fi
say "READY: $(printf '%s' "$ready" | tr -d '\r')"

id=$(printf '%s' "$ready" | sed -E 's/.* id=(-?[0-9]+).*/\1/')
pos=$(printf '%s' "$ready" | sed -E 's/.* pos=([-0-9.,]+) .*/\1/')
px=${pos%%,*}; rest=${pos#*,}; py=${rest%%,*}; pz=${rest#*,}
say "player id=$id pos=($px, $py, $pz)"

# 4. the scene + the batches
say "== gate =="
bash "$HERE/run-tidy-gate.sh" "$px" "$py" "$pz" "$id" 2>&1 | tr -d '\r' | tee -a "$LOG"

say "== pass done =="
