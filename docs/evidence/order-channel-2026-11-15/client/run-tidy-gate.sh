#!/usr/bin/env bash
# Drive the two client checks (Tidy, and the items landing in the destination chest) in one
# pass, inside the scene's 240 s hold. Run only when the client is free.
#
#   ./run-tidy-gate.sh <px> <py> <pz> <playerId>
#
# The scene is built around the position the client reports at `gc ready` - the player is
# never moved (RESULT.md, rig finding 4).
set -uo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
EV="$(cd "$HERE/.." && pwd)"
ROOT="/home/hermes/projects/valheim/testrig"
KEY="${CLIENT_KEY:-$HOME/.ssh/husnisse_client_ed25519}"
GUEST="${CLIENT_HOST:-Hermes@192.168.1.172}"
CLOG='C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/LogOutput.log'
RIG_DST_TREE="${RIG_DST:-V:\\ValheimOrders}"
RIG_PORT_TREE="${RIG_PORT:-2856}"

px="${1:?usage: run-tidy-gate.sh <px> <py> <pz> <playerId>}"
py="${2:?}"
pz="${3:?}"
id="${4:?}"

probe() { bash "$HERE/probe.sh" -f "$1" >/dev/null 2>&1 || echo "send failed: $1"; }

fetch_log() {
  scp -q -i "$KEY" -o BatchMode=yes "$GUEST:$CLOG" "$HERE/logs/client-bepinex-orders.log" 2>/dev/null || return 0
  grep -a "gc " "$HERE/logs/client-bepinex-orders.log" | sed 's/^\[Info   :Husnisse Gate Client Probe\] //' | tail -"${1:-40}"
}

harness() {
  ssh -i "$KEY_HOST" -o BatchMode=yes -o ConnectTimeout=15 WinGSM@192.168.1.136 \
      "Get-Content 'V:\\ValheimOrders\\BepInEx\\BSBLogs\\Harness.log' -Tail ${1:-40}" 2>&1 | tr -d '\r'
}
KEY_HOST="$HOME/.ssh/valheim_server_ed25519"

echo "== scene for ($px, $py, $pz) player $id =="
python3 "$EV/make-scene-at.py" "$px" "$py" "$pz" "$id" > "$HERE/gate-orders-tidy.cmd"

echo "== starting the scenario (240 s hold) =="
(
  cd "$ROOT"
  RIG_DST="$RIG_DST_TREE" RIG_PORT="$RIG_PORT_TREE" ./rig.sh scenario "$HERE/gate-orders-tidy.cmd" \
      > /tmp/scenario-tidy.log 2>&1
  echo "done=$?" >> /tmp/scenario-tidy.log
) &
DRIVER=$!

for i in $(seq 1 30); do
  sleep 3
  if harness 80 | grep -q "husnisse status lines="; then echo "scene built (${i}x3 s)"; break; fi
done
harness 90 | grep -E "^(> )?(husnisse|fill|container|zdo|console|wait|spawn husnisse|spawn piece)" | tail -32

echo
echo "== t01 scene as the client sees it =="
probe "$HERE/batches/t01-scene.cmd"
sleep 5
fetch_log 24

echo
echo "== t02 Alt the nearest chest: Tidy =="
probe "$HERE/batches/t02-tidy.cmd"
sleep 16
fetch_log 14

echo
echo "== t03 after the tidy trip =="
probe "$HERE/batches/t03-after-tidy.cmd"
sleep 5
fetch_log 14

echo
echo "== t04 seed pockets =="
probe "$HERE/batches/t04-give.cmd"
sleep 5
fetch_log 8

echo
echo "== t05 Empty Pockets =="
probe "$HERE/batches/t05-empty-pockets.cmd"
sleep 20
fetch_log 8

echo
echo "== t06 after the empty-pockets trip =="
probe "$HERE/batches/t06-after-pockets.cmd"
sleep 8
fetch_log 20

echo
echo "== server: orders + flow lines =="
ssh -i "$KEY_HOST" -o BatchMode=yes -o ConnectTimeout=15 WinGSM@192.168.1.136 \
    "Get-Content 'V:\\ValheimOrders\\BepInEx\\BSBLogs\\Husnisse.log' | Select-String -Pattern '\\[Orders\\]|\\[Flow\\]|\\[Bowl\\]'" 2>&1 | tr -d '\r'

echo
echo "== server: the chests after the gate (in-scene dump, index 0 = destination) =="
# The scene dumps the chests 120 s after it is built; the gate takes ~90 s, so wait for it.
sleep 40
harness 260 | grep -E "^(> )?(container|husnisse queue|husnisse status claim)" | tail -24

echo "== scenario driver still running in the background ($DRIVER); log: /tmp/scenario-tidy.log =="
