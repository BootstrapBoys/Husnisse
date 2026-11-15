#!/usr/bin/env bash
# Collect the client-side evidence for the 0.3b gate into this folder and run the verifier.
#   - stages the client's BepInEx log + screenshot folder on the guest, zips it, pulls it back
#   - pulls the mod's diagnostics file and the harness log from the rig host
#   - runs verify.sh and analysis.py, writing client/verify.txt and client/layout.txt
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
OUT="$HERE/client"
mkdir -p "$OUT"

echo "== stage on the guest"
bash "$HERE/run.sh" stage | tee "$OUT/stage.txt"

echo "== pull the zip"
scp -q -i "$HOME/.ssh/husnisse_client_ed25519" "Hermes@192.168.1.172:C:/Users/Hermes/stage/client-evidence.zip" /home/hermes/.hermes/cache/scratch/client-evidence.zip

echo "== unpack"
rm -rf /home/hermes/.hermes/cache/scratch/client-evidence
mkdir -p /home/hermes/.hermes/cache/scratch/client-evidence
python3 - "$OUT" <<'PY'
import os, sys, zipfile
out = sys.argv[1]
src = '/home/hermes/.hermes/cache/scratch/client-evidence.zip'
dst = '/home/hermes/.hermes/cache/scratch/client-evidence'
with zipfile.ZipFile(src) as z:
    bad = z.testzip()
    print('zip entries:', len(z.namelist()), 'crc:', 'ok' if bad is None else f'BAD {bad}')
    z.extractall(dst)
for name in sorted(os.listdir(dst)):
    path = os.path.join(dst, name)
    if name.lower().endswith('.png'):
        target = os.path.join(out, 'gate-03b-' + name[4:] if name.startswith('00-') else name)
        os.replace(path, target)
        print('  screenshot ->', os.path.basename(target), os.path.getsize(target), 'bytes')
    else:
        target = os.path.join(out, name)
        os.replace(path, target)
        print('  file       ->', os.path.basename(target), os.path.getsize(target), 'bytes')
PY

echo "== server logs"
bash "$REPO/tooling/client-rig/rigrun.sh" - <<'PS' > "$OUT/server-logs.txt" 2>&1 || true
Write-Output "== BSBLogs/Husnisse.log (tail) =="
Get-Content "V:\ValheimTest\BepInEx\BSBLogs\Husnisse.log" -Tail 120
Write-Output "== BSBLogs/Harness.log (tail) =="
Get-Content "V:\ValheimTest\BepInEx\BSBLogs\Harness.log" -Tail 120
PS
grep -a '\[Flow\]\|\[Orders\]\|\[Rules\]' "$OUT/server-logs.txt" > "$OUT/server-mod-lines.txt" || true
echo "  mod lines: $(wc -l < "$OUT/server-mod-lines.txt")"

echo "== verify"
bash "$HERE/verify.sh" | tee "$OUT/verify.txt" | tail -5

echo "== layout"
python3 "$HERE/analysis.py" "$HERE/client/logs/p3-panel-read.txt" "$HERE/client/logs/p3-panel-narrow.txt" | tee "$OUT/layout.txt"
