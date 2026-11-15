#!/usr/bin/env bash
# Stop, clean, deploy and boot V:\ValheimTest for the 0.3b client gate, waiting out the shared
# rig lock (rig.sh exits 3 when another card's command holds it — contention, not failure).
#   usage: boot-tree.sh <Husnisse.dll>
set -uo pipefail
DLL="${1:?path to Husnisse.dll}"
RIG="${RIG_DIR:-/home/hermes/projects/valheim/testrig}"
cd "$RIG"

try() {   # try <label> <args...> -- returns nonzero when the lock was busy
  local label="$1"; shift
  local out
  out=$("./rig.sh" "$@" 2>&1)
  if printf '%s' "$out" | grep -q 'another rig command'; then
    echo "  $label: rig lock busy"
    return 3
  fi
  printf '%s\n' "$out" | tail -8 | sed "s/^/  $label: /"
  return 0
}

run() {   # run <label> <args...> -- retry the lock wait, fail the step on a real error
  local label="$1"; shift
  local attempt rc
  for attempt in $(seq 1 60); do
    try "$label" "$@"; rc=$?
    [ "$rc" -eq 3 ] || return "$rc"
    sleep 15
  done
  echo "  $label: gave up waiting for the rig lock"
  return 1
}

run down down || exit 1
run fixture fixture restore || exit 1
run deploy deploy "$DLL" BootstrapBoys-Husnisse || exit 1
run up up || exit 1
run status status || true
