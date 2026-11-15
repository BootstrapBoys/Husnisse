#!/usr/bin/env bash
# 0.3.1 client gate — teaching and the ledger, on a real client.
#
# Shape: the dedicated gate tree (V:\Gate031, port 2956) runs the gate build; ONE harness scenario
# builds the fixture (every scenario load runs Cleanup() and wipes the previous scene, so nothing
# server-side runs after it); everything after is driven from the client process — the client's own
# Alt-interact, its own inventory moves, and clicks through the ledger's own Button.onClick — and
# every reading is taken from what the client sees. The server side of the story comes from the
# mod's diagnostics file (BSBLogs\Husnisse.log) copied off the host at the end.
#
#   usage: gate.sh <phase> [...]
#   phases: prep install launch scene teach ledger collect all
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="/home/hermes/projects/valheim/nisse"
BUILD="${GATE_BUILD:-/home/hermes/projects/valheim/nisse-031-gate}"
RIG="${RIG_DIR:-/home/hermes/projects/valheim/testrig}"
KEY="$HOME/.ssh/husnisse_client_ed25519"
GUEST="Hermes@192.168.1.172"
LOG="$HERE/logs"
STATE="$HERE/state.env"
mkdir -p "$LOG"

RIG_DST2='V:\Gate031'
RIG_PORT2=2956
GAME_PORT=2956

# shellcheck disable=SC1090
[ -f "$STATE" ] && . "$STATE"
[ -f "$HERE/gate-031-indices.env" ] && . "$HERE/gate-031-indices.env"

note() { printf '\n== %s\n' "$*" | tee -a "$LOG/driver.txt"; }
rig() { RIG_DST="$RIG_DST2" RIG_PORT=$RIG_PORT2 bash "$RIG/rig.sh" "$@" 2>&1; }
P() {
  local name="$1"; shift
  printf -- '--- %s\n' "$name" >> "$LOG/driver.txt"
  bash "$HERE/pc.sh" "$@" 2>&1 | tee "$LOG/$name.txt" | sed 's/^/   /'
}
S() {
  local label="$1" file="$2"
  printf -- '--- %s\n' "$label" >> "$LOG/driver.txt"
  bash "$HERE/s031.sh" "$file" 2>&1 | tee "$LOG/server-$label.txt" | sed 's/^/   /'
}
shot() { P "shot-$1" "shot $1"; }
psclient() { bash "$REPO/tooling/client-rig/psrun.sh" "$1"; }
psserver() { bash "$REPO/tooling/client-rig/rigrun.sh" "$1"; }

set_task_port() {
  local port="$1"
  cat > "$LOG/task-port.ps1" <<PS
\$t = Get-ScheduledTask -TaskName 'HusnisseLegsLaunch'
\$a = \$t.Actions[0]
\$a.Arguments = [regex]::Replace(\$a.Arguments, '192\.168\.1\.136:\d+', '192.168.1.136:$port')
Set-ScheduledTask -TaskName 'HusnisseLegsLaunch' -Action \$a | Out-Null
Write-Output ('task args: ' + (Get-ScheduledTask -TaskName 'HusnisseLegsLaunch').Actions[0].Arguments)
PS
  psclient "$LOG/task-port.ps1"
}

kill_client() {
  cat > "$LOG/kill.ps1" <<'PS'
$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) {
  $p | ForEach-Object { $_.CloseMainWindow() | Out-Null }
  Start-Sleep -Seconds 8
  $p = Get-Process -Name valheim -ErrorAction SilentlyContinue
  if ($p) { $p | Stop-Process -Force; Start-Sleep -Seconds 5; Write-Output 'force killed' }
  else { Write-Output 'closed' }
} else { Write-Output 'not running' }
PS
  psclient "$LOG/kill.ps1"
}

# --------------------------------------------------------------------------- phases

prep() {
  note "prep: stop the spare tree, side-line its world, boot the gate build"
  rig down | tail -5
  psserver "$HERE/fresh-world.ps1"
  rig up | tail -12 | tee "$LOG/server-up.txt"
  rig check | tail -30 | tee "$LOG/server-check.txt"
}

install() {
  note "install: stop the client, push the gate build and the probe, verify the hashes"
  kill_client
  ls -l "$BUILD/artifacts/debug/Husnisse.dll" "$BUILD/probe/gate-client/artifacts/HusnisseGateClientProbe.dll"
  scp -q -i "$KEY" "$BUILD/artifacts/debug/Husnisse.dll" "$GUEST:C:/Users/Hermes/stage/Husnisse.dll" || return 1
  scp -q -i "$KEY" "$BUILD/probe/gate-client/artifacts/HusnisseGateClientProbe.dll" \
      "$GUEST:C:/Users/Hermes/stage/HusnisseGateClientProbe.dll" || return 1
  bash "$HERE/run.sh" install | tee "$LOG/install.txt"
  bash "$HERE/run.sh" reset | tee -a "$LOG/install.txt"
  sha256sum "$BUILD/artifacts/debug/Husnisse.dll" "$BUILD/probe/gate-client/artifacts/HusnisseGateClientProbe.dll"
}

launch() {
  note "launch: point the join task at 2956, start it, wait for this launch's ready line"
  set_task_port "$GAME_PORT"
  cat > "$LOG/start.ps1" <<'PS'
Start-ScheduledTask -TaskName 'HusnisseLegsLaunch'
Write-Output ('launched HusnisseLegsLaunch at ' + (Get-Date).ToString('HH:mm:ss'))
PS
  psclient "$LOG/start.ps1" | tee "$LOG/launch.txt"
  local i ready=""
  for i in $(seq 1 40); do
    sleep 10
    ready=$(bash "$HERE/run.sh" grep 2>/dev/null | grep -a 'gc ready' | tail -1)
    if [ -n "$ready" ]; then echo "$ready" | tee -a "$LOG/launch.txt"; break; fi
  done
  [ -n "$ready" ] || { echo "TIMEOUT waiting for gc ready"; return 1; }
  local pid
  pid=$(printf '%s' "$ready" | sed -n 's/.* id=\(-*[0-9]*\).*/\1/p')
  printf 'PLAYER_ID=%s\n' "$pid" > "$STATE"
  printf 'LAUNCH_LINE=%s\n' "$ready" >> "$STATE"
  echo "player id $pid" | tee -a "$LOG/launch.txt"
  P launch-state "state" "god on"
}

scene() {
  note "scene: wipe the last card's scene, then build the 0.3.1 fixture at the client's feet"
  S pre-clean "$HERE/pre-clean.cmd"
  sleep 4
  P scene-where "god on" "heal" "where"
  local where x y z deck
  where=$(grep -a 'gc where player=' "$LOG/scene-where.txt" | tail -1)
  echo "$where" | tee -a "$LOG/scene.txt"
  read -r x y z <<< "$(printf '%s' "$where" | sed -E 's/.*player=(-?[0-9.]+),(-?[0-9.]+),(-?[0-9.]+).*/\1 \2 \3/')"
  [ -n "${z:-}" ] || { echo "could not parse the client position: $where"; return 1; }
  deck=$(awk "BEGIN{printf \"%.2f\", $y+0.25}")
  echo "base x=$x y=$y z=$z -> deck $deck" | tee -a "$LOG/scene.txt"
  bash "$HERE/scene-031.sh" "$(printf '%.0f' "$x")" "$deck" "$(printf '%.0f' "$z")" "${PLAYER_ID:?}" \
       /home/hermes/.hermes/cache/scratch/gate-031-scene.cmd \
       "$HERE/gate-031-indices.env" | tee -a "$LOG/scene.txt"
  cp /home/hermes/.hermes/cache/scratch/gate-031-scene.cmd "$HERE/gate-031-scene.cmd"
  S scene "$HERE/gate-031-scene.cmd"
  . "$HERE/gate-031-indices.env"
  sleep 2
  P scene-containers "containers"
}

teach() {
  note "teach: Alt the nisse, show it a metal and a wood, then prove it stops watching"
  local copper="${IDX_copper:-3}" wood="${IDX_wood:-0}" control="${IDX_control:-2}"

  P t0 "hover husnisse_nisse" "alt husnisse_nisse"
  P t1 "hover husnisse_nisse"
  P t2 "give CopperScrap 5" "stand piece_chest_wood $copper" "open piece_chest_wood $copper"
  P t3 "ui"
  P t4 "move piece_chest_wood CopperScrap 5" "inv"
  P t5 "close"
  P t6 "give Wood 5" "stand piece_chest_wood $wood" "open piece_chest_wood $wood"
  P t7 "move piece_chest_wood Wood 5" "inv"
  P t8 "close"
  P t9 "containers"
  P t10 "alt husnisse_nisse"
  P t11 "give Coal 5" "stand piece_chest_wood $control" "open piece_chest_wood $control"
  P t12 "move piece_chest_wood Coal 5" "inv"
  P t13 "close"
  P t14 "containers"
}

ledger() {
  note "ledger: open it, classify, open the chest it edited, then net/blacklist/pickup"
  local copper="${IDX_copper:-3}" control="${IDX_control:-2}"

  P l0 "close" "ledger"
  P l1 "ledgerpanel"
  P l2 "click HusnisseLedgerRow$copper"
  P l3 "ledgerpanel"
  P l4 "click HusnisseLedgerCatMetals"
  P l5 "ledgerpanel"
  P l6 "close" "stand piece_chest_wood $copper" "open piece_chest_wood $copper"
  P l7 "ui"
  shot ledger-rule
  P l8 "close" "ledger"
  P l9 "click HusnisseLedgerRow$control"
  P l10 "ledgerpanel"
  P l11 "click HusnisseLedgerNet"
  P l12 "ledgerpanel"
  P l13 "containers"
  P l14 "click HusnisseLedgerBlacklist"
  P l15 "ledgerpanel"
  P l16 "close" "stand piece_chest_wood $control" "open piece_chest_wood $control"
  P l17 "ui"
  P l18 "close" "ledger"
  P l19 "click HusnisseLedgerPickupMetals"
  P l20 "click HusnisseLedgerPickupMetals"
  P l21 "ledgerpanel"
  shot ledger
}

collect() {
  note "collect: client evidence, server logs, and hand the rig back"
  bash "$HERE/run.sh" stage | tee "$LOG/stage.txt"
  scp -q -i "$KEY" "$GUEST:C:/Users/Hermes/stage/client-evidence.zip" "$HERE/client-evidence.zip" && \
    echo "client-evidence.zip $(stat -c%s "$HERE/client-evidence.zip") bytes"
  cat > "$LOG/server-logs.ps1" <<'PS'
$dst = 'C:\Users\WinGSM\stage\031'
New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($pair in @(@('V:\Gate031\BepInEx\BSBLogs\Husnisse.log', 'server-husnisse.log'),
                    @('V:\Gate031\BepInEx\LogOutput.log', 'server-bepinex.log'),
                    @('V:\Gate031\BepInEx\BSBLogs\Harness.log', 'server-harness.log'))) {
  if (Test-Path -LiteralPath $pair[0]) {
    Copy-Item -LiteralPath $pair[0] -Destination (Join-Path $dst $pair[1]) -Force
    Write-Output ('copied ' + $pair[1])
  } else { Write-Output ('missing ' + $pair[0]) }
}
PS
  psserver "$LOG/server-logs.ps1" | tee "$LOG/server-logs.txt"
  for f in server-husnisse.log server-bepinex.log; do
    ssh -i "$HOME/.ssh/valheim_server_ed25519" -o BatchMode=yes WinGSM@192.168.1.136 \
      "powershell -NoProfile -Command \"Get-Content -LiteralPath 'C:\\Users\\WinGSM\\stage\\031\\$f' -Raw\"" \
      | tr -d '\r' > "$HERE/$f"
    echo "$f $(wc -l < "$HERE/$f") lines"
  done
  P quit "quit"
  set_task_port 2556
  echo "client freed; join task restored to 2556"
}

case "${1:-all}" in
  prep|install|launch|scene|teach|ledger|collect) "$1" ;;
  all) prep && install && launch && scene && teach && ledger && collect ;;
  *) echo "usage: gate.sh prep|install|launch|scene|teach|ledger|collect|all"; exit 2 ;;
esac
