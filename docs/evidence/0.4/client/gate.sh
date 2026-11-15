#!/usr/bin/env bash
# 0.4 client gate — the read-only storage contract, on a real client with the consumer mods.
#
# Shape: the dedicated gate tree (V:\Gate031, port 2956) runs the gate build plus the same seven
# consumer mods the client runs; ONE harness scenario builds the fixture (every scenario load runs
# Cleanup() and wipes the previous scene, so nothing server-side runs after it); everything after is
# asked from the client process — the mod's own contract, the game's own requirement-row builder, and
# the integrations each report their bound state.
#
#   usage: gate.sh <phase> [...]
#   phases: prep client scene p1 p2 p3 p4 collect all
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="/home/hermes/projects/valheim/.worktrees/04-readonly"
RIG="${RIG_DIR:-/home/hermes/projects/valheim/testrig}"
LOG="$HERE/logs"
STATE="$HERE/state.env"
mkdir -p "$LOG"

RIG_DST2='V:\Gate031'
RIG_PORT2=2956
GAME_PORT=2956

# shellcheck disable=SC1090
[ -f "$STATE" ] && . "$STATE"

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
  bash "$HERE/s04.sh" "$file" 2>&1 | tee "$LOG/server-$label.txt" | sed 's/^/   /'
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

client_running() {
  cat > "$LOG/ps-running.ps1" <<'PS'
$p = Get-Process valheim -ErrorAction SilentlyContinue
if ($p) { Write-Output ('running ' + $p.Id) } else { Write-Output 'idle' }
PS
  psclient "$LOG/ps-running.ps1" | tr -d '\r' | grep -E 'running|idle' | tail -1
}

launch_client() {
  cat > "$LOG/ps-launch.ps1" <<'PS'
$p = Get-Process valheim -ErrorAction SilentlyContinue
if ($p) { Write-Output ('REFUSING: a client is already running (pid ' + $p.Id + ')'); exit 0 }
Start-ScheduledTask -TaskName 'HusnisseLegsLaunch'
Write-Output 'launch requested'
PS
  psclient "$LOG/ps-launch.ps1"
}

# --------------------------------------------------------------------------- phases

prep() {
  note "prep: stop the tree, side-line its world, deploy the gate build, boot"
  rig down | tail -4
  psserver "$HERE/fresh-world.ps1"
  rig deploy "$REPO/bin/Release/net48/Husnisse.dll" BootstrapBoys-Husnisse | tail -1
  rig deploy "$RIG/artifacts/harness/TestHarness.dll" BootstrapBoys-TestHarness | tail -1
  rig up | tail -8 | tee "$LOG/server-up.txt"
}

client() {
  note "client: launch into the gate tree and wait for the world"
  set_task_port "$GAME_PORT" | tee "$LOG/task-port.txt"
  launch_client | tee "$LOG/launch.txt"
  for i in $(seq 1 60); do
    sleep 5
    local tail_text
    tail_text="$(bash "$HERE/run.sh" tail -Lines 120 2>/dev/null | tr -d '\r')"
    if printf '%s' "$tail_text" | grep -q 'gc ready'; then
      printf '%s\n' "$tail_text" | grep -E 'gc ready|gc loaded|Husnisse 0\.4' | tail -5 | tee "$LOG/client-ready.txt"
      local st pid
      st="$(P "state" "state" | tr -d '\r' | grep -oE 'state player=.* id=[-0-9]+' | tail -1)"
      pid="$(printf '%s' "$st" | grep -oE 'id=[-0-9]+' | cut -d= -f2)"
      echo "client $st" | tee "$LOG/client-state.txt"
      if [ -n "${pid:-}" ]; then
        printf 'CLIENT_PID=%s\n' "$pid" >> "$STATE"
        echo "client pid $pid" | tee -a "$LOG/client-state.txt"
      fi
      return 0
    fi
  done
  echo "client: never reached gc ready" | tee "$LOG/client-ready.txt"
  return 1
}

scene() {
  note "scene: build the fixture at the client's own position"
  local line x y z
  line="$(P "where" "where" | tr -d '\r' | grep -oE 'player=[-0-9.]+,[-0-9.]+,[-0-9.]+' | tail -1)"
  echo "client $line" | tee "$LOG/where.txt"
  x="$(printf '%s' "$line" | cut -d= -f2 | cut -d, -f1)"
  y="$(printf '%s' "$line" | cut -d= -f2 | cut -d, -f2)"
  z="$(printf '%s' "$line" | cut -d= -f2 | cut -d, -f3)"
  if [ -z "${x:-}" ]; then echo "scene: no position"; return 1; fi
  printf 'PX=%s\nPY=%s\nPZ=%s\n' "$x" "$y" "$z" >> "$STATE"
  if [ -z "${CLIENT_PID:-}" ]; then echo "scene: CLIENT_PID is not set (run gate.sh client first)"; return 1; fi

  local f="$HERE/gate-04-scene.cmd"
  {
    echo "# 0.4 client gate scene, generated at ($x, $y, $z)."
    echo "# A floor for support, one bowl owned by the client, one chest to hold the claim's stock."
    echo "cleanup"
    echo "hold on"
    local dx dz fx fy fz bx by bz cx cy cz
    for dx in -2 -1 0 1 2; do
      for dz in -2 -1 0 1 2; do
        read -r fx fy fz <<< "$(awk -v a="$x" -v b="$y" -v c="$z" -v i="$dx" -v j="$dz" 'BEGIN{printf "%.2f %.2f %.2f", a+i, b+0.10, c+j}')"
        echo "spawn wood_floor $fx $fy $fz"
      done
    done
    read -r bx by bz <<< "$(awk -v a="$x" -v b="$y" -v c="$z" 'BEGIN{printf "%.2f %.2f %.2f", a+1, b+0.20, c}')"
    read -r cx cy cz <<< "$(awk -v a="$x" -v b="$y" -v c="$z" 'BEGIN{printf "%.2f %.2f %.2f", a+2, b+0.20, c}')"
    echo "spawn husnisse_bowl $bx $by $bz $CLIENT_PID"
    echo "spawn piece_chest_wood $cx $cy $cz"
    echo "wait 6"
    echo "nostruct"
    echo "zdo husnisse_bowl husnisse_bowl_init"
    echo "husnisse claims"
    echo "expect_claim owner=$CLIENT_PID"
    echo "fill piece_chest_wood Wood 30"
    echo "container piece_chest_wood 0"
  } > "$f"
  S scene "$f"
}

p1() {
  note "p1: the contract, standing in the claim"
  P p1-inv "inv" "contract Wood" "containers"
}

p2() {
  note "p2: the requirement row the player would see"
  P p2-row "row Wood 5" "row Iron 2"
}

p3() {
  note "p3: out of the claim the claim leg disappears"
  # shellcheck disable=SC1090
  [ -f "$STATE" ] && . "$STATE"
  local fx fy fz
  read -r fx fy fz <<< "$(awk -v a="${PX:-0}" -v b="${PY:-40}" -v c="${PZ:-0}" 'BEGIN{printf "%.1f %.1f %.1f", a+30, b+2, c}')"
  P p3-far "setpos $fx $fy $fz" "where" "contract Wood"
  sleep 3
  P p3-back "setpos $PX $PY $PZ" "where" "contract Wood"
}

p4() {
  note "p4: integrations and a screenshot"
  P p4-integrations "integrations" "contract"
  shot contract
}

collect() {
  note "collect: pull every log"
  bash "$HERE/run.sh" stage 2>&1 | tee "$LOG/client-stage.txt" | tail -6
  scp -q -i "$HOME/.ssh/husnisse_client_ed25519" -o BatchMode=yes \
    "Hermes@192.168.1.172:C:/Users/Hermes/stage/client-evidence.zip" "$LOG/client-evidence.zip" \
    && echo "pulled $LOG/client-evidence.zip"
  cat > "$LOG/pull-server.ps1" <<'PS'
Copy-Item 'V:\Gate031\BepInEx\BSBLogs\Husnisse.log' 'V:\drop\gate04-husnisse.log' -Force
Copy-Item 'V:\Gate031\BepInEx\LogOutput.log' 'V:\drop\gate04-bepinex.log' -Force
Copy-Item 'V:\Gate031\BepInEx\BSBLogs\Harness.log' 'V:\drop\gate04-harness.log' -Force
Write-Output 'server logs staged'
PS
  psserver "$LOG/pull-server.ps1" | tail -2
  for f in gate04-husnisse.log gate04-bepinex.log gate04-harness.log; do
    scp -q -i "$HOME/.ssh/valheim_server_ed25519" -o BatchMode=yes "WinGSM@192.168.1.136:V:/drop/$f" "$LOG/$f" \
      && echo "pulled $f"
  done
}

case "${1:-}" in
  prep) prep ;;
  client) client ;;
  scene) scene ;;
  p1) p1 ;;
  p2) p2 ;;
  p3) p3 ;;
  p4) p4 ;;
  collect) collect ;;
  all) prep && client && scene && p1 && p2 && p3 && p4 && collect ;;
  *) echo "usage: gate.sh prep|client|scene|p1|p2|p3|p4|collect|all"; exit 2 ;;
esac
