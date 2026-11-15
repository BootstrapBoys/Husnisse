#!/usr/bin/env bash
# Wait for the shared rig lock, then restore the fixture and boot. Retries the whole sequence:
# rig.sh exits 3 when another card's command holds the lock, which is contention, not failure.
set -uo pipefail
cd /home/hermes/projects/valheim/testrig
for attempt in $(seq 1 40); do
  out=$(./rig.sh down 2>&1 | tail -2)
  if echo "$out" | grep -q 'another rig command'; then echo "attempt $attempt: lock busy"; sleep 20; continue; fi
  echo "down: $out"
  out=$(./rig.sh fixture restore 2>&1 | tail -2)
  if echo "$out" | grep -q 'another rig command'; then echo "attempt $attempt: lock busy at restore"; sleep 20; continue; fi
  echo "restore: $out"
  out=$(./rig.sh up 2>&1 | tail -8)
  if echo "$out" | grep -q 'another rig command'; then echo "attempt $attempt: lock busy at up"; sleep 20; continue; fi
  echo "$out"
  echo "clean-boot-done"
  exit 0
done
echo "gave up waiting for the rig lock"
exit 1
