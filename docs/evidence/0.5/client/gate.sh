#!/usr/bin/env bash
# 0.5 client gate — craft from the claim, on a real client with the consumer pack installed.
#
# Shape: the dedicated gate tree (V:\Gate031, port 2956) runs the gate build and the same consumer
# mods the client runs. The harness builds a floor, a workbench, a bowl and a chest; everything after
# that is asked from the client process — the crafting window, the craft press, the delivery the
# nisse walks, and the panel state after it.
#
#   usage: gate.sh <phase> [...]
#   phases: prep client scene p1 p2 p3 p4 collect all
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="${REPO:-/home/hermes/projects/valheim/nisse}"
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
  bash "$HERE/s05.sh" "$file" 2>&1 | tee "$LOG/server-$label.txt" | sed 's/^/   /'
}
shot() { P "shot-$1" "shot $1"; }
psclient() { bash "$REPO/tooling/client-rig/psrun.sh" "$1"; }

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
  # The probe replays whatever is in its command file on startup, so a `quit` left from the previous
  # session ends the new client before it has drawn a frame. Reset it while nothing is reading it.
  bash "$HERE/run.sh" reset >/dev/null 2>&1
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
  note "prep: stop the tree, side-line its world, deploy the gate build to both sides, boot"
  rig down | tail -4
  bash "$REPO/tooling/client-rig/rigrun.sh" "$HERE/fresh-world.ps1" 2>/dev/null | tail -3
  rig deploy "$REPO/artifacts/debug/Husnisse.dll" BootstrapBoys-Husnisse | tail -1
  rig deploy "$RIG/artifacts/harness/TestHarness.dll" BootstrapBoys-TestHarness | tail -1

  # The client's plugins are deployed from its stage folder; a running client locks its DLLs, which
  # is why this phase refuses to run with one up.
  local st
  st="$(client_running)"
  echo "client $st" | tee -a "$LOG/driver.txt"
  case "$st" in
    running*) echo "prep: stop the client first (quit via the probe)" | tee -a "$LOG/driver.txt"; return 1 ;;
  esac
  scp -q -i "$HOME/.ssh/husnisse_client_ed25519" -o BatchMode=yes \
    "$REPO/artifacts/debug/Husnisse.dll" "$REPO/probe/gate-client/artifacts/HusnisseGateClientProbe.dll" \
    "Hermes@192.168.1.172:C:/Users/Hermes/stage/" || { echo "prep: client scp failed"; return 1; }
  bash "$HERE/run.sh" install | tail -12
  bash "$HERE/run.sh" reset | tail -1

  rig up | tail -8 | tee "$LOG/server-up.txt"
}

client() {
  note "client: launch into the gate tree and wait for the world"
  set_task_port "$GAME_PORT" | tee "$LOG/task-port.txt"
  launch_client | tee "$LOG/launch.txt"
  for i in $(seq 1 60); do
    sleep 5
    local tail_text
    tail_text="$(bash "$HERE/run.sh" tail -Lines 160 2>/dev/null | tr -d '\r')"
    if printf '%s' "$tail_text" | grep -q 'gc ready'; then
      printf '%s\n' "$tail_text" | grep -E 'gc ready|gc loaded|Husnisse 0\.5' | tail -5 | tee "$LOG/client-ready.txt"
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
  note "scene: a floor, a workbench and the claim, at the client's own position"
  local line x y z
  line="$(P "where" "where" | tr -d '\r' | grep -oE 'player=[-0-9.]+,[-0-9.]+,[-0-9.]+' | tail -1)"
  echo "client $line" | tee "$LOG/where.txt"
  x="$(printf '%s' "$line" | cut -d= -f2 | cut -d, -f1)"
  y="$(printf '%s' "$line" | cut -d= -f2 | cut -d, -f2)"
  z="$(printf '%s' "$line" | cut -d= -f2 | cut -d, -f3)"
  if [ -z "${x:-}" ]; then echo "scene: no position"; return 1; fi
  printf 'PX=%s\nPY=%s\nPZ=%s\n' "$x" "$y" "$z" >> "$STATE"
  if [ -z "${CLIENT_PID:-}" ]; then echo "scene: CLIENT_PID is not set (run gate.sh client first)"; return 1; fi

  local f="$HERE/gate-05-scene.cmd"
  {
    echo "# 0.5 client gate scene, generated at ($x, $y, $z)."
    echo "# A floor for support, a workbench for the craft, the bowl, and a chest holding the claim's stock."
    echo "cleanup"
    echo "hold on"
    local dx dz fx fy fz
    for dx in -2 -1 0 1 2; do
      for dz in -2 -1 0 1 2; do
        read -r fx fy fz <<< "$(awk -v a="$x" -v b="$y" -v c="$z" -v i="$dx" -v j="$dz" 'BEGIN{printf "%.2f %.2f %.2f", a+i, b+0.10, c+j}')"
        echo "spawn wood_floor $fx $fy $fz"
      done
    done
    read -r fx fy fz <<< "$(awk -v a="$x" -v b="$y" -v c="$z" 'BEGIN{printf "%.2f %.2f %.2f", a+2, b+0.20, c-1}')"
    echo "spawn piece_workbench $fx $fy $fz $CLIENT_PID"
    read -r fx fy fz <<< "$(awk -v a="$x" -v b="$y" -v c="$z" 'BEGIN{printf "%.2f %.2f %.2f", a-1.5, b+0.20, c}')"
    echo "spawn husnisse_bowl $fx $fy $fz $CLIENT_PID"
    read -r fx fy fz <<< "$(awk -v a="$x" -v b="$y" -v c="$z" 'BEGIN{printf "%.2f %.2f %.2f", a, b+0.20, c+2.5}')"
    echo "spawn piece_chest_wood $fx $fy $fz"
    echo "wait 8"
    echo "nostruct"
    echo "zdo husnisse_bowl husnisse_bowl_init"
    echo "husnisse claims"
    echo "expect_claim owner=$CLIENT_PID"
    echo "husnisse containers"
    echo "expect_ge husnisse containers found=(\d+) 1"
  } > "$f"
  S scene "$f"

  # The chest's contents must reach *both* sides: a harness fill writes the server's in-memory copy,
  # and Container.Save is owner-gated. Fill it from the client, the way a player would.
  note "scene: fill the chest from the client (clear, give, open, move, close)"
  P "fill-clear" "god" "clear"
  P "fill-give" "give Wood 20 3"
  P "fill-open" "open piece_chest_wood"
  P "fill-move" "move piece_chest_wood Wood 200"
  P "fill-close" "close"
  sleep 2
  P "scene-inv" "inv" "containers" "contract Wood"
}

p1() {
  note "p1: the crafting window with an empty pocket and a stocked claim"
  P p1-open "close" "craftopen"
  sleep 2
  P p1-available "available Club" "available Torch"
  P p1-select "select Club"
  sleep 2
  P p1-state "craftstate"
  shot 05-before-craft
}

p2() {
  note "p2: the press, the nisse's load, and the craft it pays for"
  P p2-craft "craft"
  # Screenshots through the errand: the nisse walks from the chest to the player, carrying the load.
  P p2-burst "burst 05-supply 16 0.9"
  sleep 16
  P p2-state "craftstate" "inv" "containers" "contract Wood"
  shot 05-after-craft
}

p3() {
  note "p3: AAA Crafting's batch fetches for the whole batch"
  P p3-open "close" "craftopen"
  sleep 2
  P p3-aaa "aaa 2" "select Club"
  sleep 2
  P p3-craft "craft"
  P p3-burst "burst 05-batch 14 0.9"
  sleep 16
  P p3-state "aaa 1" "craftstate" "inv" "containers" "contract Wood"
  shot 05-after-batch
}

p4() {
  note "p4: integrations, and the panel the player is left looking at"
  P p4-integrations "integrations" "contract" "craftstate"
  shot 05-final
}

collect() {
  note "collect: pull every log"
  bash "$HERE/run.sh" stage 2>&1 | tee "$LOG/client-stage.txt" | tail -8
  scp -q -i "$HOME/.ssh/husnisse_client_ed25519" -o BatchMode=yes \
    "Hermes@192.168.1.172:C:/Users/Hermes/stage/client-evidence.zip" "$LOG/client-evidence.zip" \
    && echo "pulled $LOG/client-evidence.zip"
  cat > "$LOG/pull-server.ps1" <<'PS'
Copy-Item 'V:\Gate031\BepInEx\BSBLogs\Husnisse.log' 'V:\drop\gate05-husnisse.log' -Force
Copy-Item 'V:\Gate031\BepInEx\LogOutput.log' 'V:\drop\gate05-bepinex.log' -Force
Copy-Item 'V:\Gate031\BepInEx\BSBLogs\Harness.log' 'V:\drop\gate05-harness.log' -Force
Write-Output 'server logs staged'
PS
  bash "$REPO/tooling/client-rig/rigrun.sh" "$LOG/pull-server.ps1" | tail -2
  for f in gate05-husnisse.log gate05-bepinex.log gate05-harness.log; do
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
