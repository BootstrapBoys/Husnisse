#!/usr/bin/env bash
# 0.3b client gate — the panel on a real client.
#
# Shape: ONE harness scenario builds the scene (every new scenario wipes the previous one), and
# everything after it is driven from the client: clicks through each control's own Button.onClick,
# items carried into the chests by the client's own `move` (the same inventory write a player's
# drag makes), and every reading taken from the client process (`ui`, `panel`, `containers`). The
# routing facts are read back from the client's own container dump and from the mod's diagnostics
# file copied off the host at the end — so no later scenario can destroy the scene mid-run.
#
#   usage: driver.sh <phase> [...]
#   phases: install launch scene p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 all
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
# The build under test comes from a clean worktree at HEAD, never the shared checkout (sibling
# cards edit src/ there while gates run, and their work-in-progress would ride along in the DLL).
BUILD="${GATE_BUILD:-/home/hermes/projects/valheim/nisse-gate03b}"
RIG="${RIG_DIR:-/home/hermes/projects/valheim/testrig}"
LOG="$HERE/client/logs"
BASE_FILE=/home/hermes/.hermes/cache/scratch/gate-base.txt
mkdir -p "$LOG"

# Role -> container index, computed by scene.sh from the layout (the harness addresses containers
# by position, so a hard-coded index drifts the moment a chest moves).
if [ -f "$HERE/client/gate-03b-indices.env" ]; then
  # shellcheck disable=SC1090
  . "$HERE/client/gate-03b-indices.env"
fi
for role in panel copyA copyB metals wood overflow deposit dump plain outside; do
  var="IDX_$role"
  [ -n "${!var:-}" ] || printf -v "$var" '%s' 0
done
C() { local v="IDX_$1"; printf '%s' "${!v}"; }

STATUS=0
note() { printf '\n== %s\n' "$*" | tee -a "$LOG/driver.txt"; }

P() {
  local name="$1"; shift
  printf -- '--- %s\n' "$name" >> "$LOG/driver.txt"
  bash "$HERE/pc.sh" "$@" 2>&1 | tee "$LOG/$name.txt" | sed 's/^/   /'
}

S() {
  local label="$1" file="$2" out attempt
  for attempt in $(seq 1 90); do
    out=$(bash "$RIG/rig.sh" scenario "$file" 2>&1)
    if printf '%s' "$out" | grep -q 'another rig command'; then
      sleep 10
      continue
    fi
    printf '%s\n' "$out" | tail -40 | tee "$LOG/server-$label.txt" | sed 's/^/   /'
    return 0
  done
  echo "S: gave up waiting for the rig lock ($label)" | tee -a "$LOG/driver.txt"
  return 1
}

shot() { P "shot-$1" "shot $1"; }

# A `watch` blocks the probe's command queue until it ends, so the batch that carries one must wait
# at least as long as the watch — otherwise the NEXT batch is what gets its output, and the reading
# that follows a watch is silently lost (this cost p10, p11 and p12 their `containers` dumps).
PW() {
  local name="$1" wait="$2"; shift 2
  printf -- '--- %s\n' "$name" >> "$LOG/driver.txt"
  PROBE_WAIT="$wait" bash "$HERE/pc.sh" "$@" 2>&1 | tee "$LOG/$name.txt" | sed 's/^/   /'
}

# --------------------------------------------------------------------------- phases

install() {
  note "install: push the build and the probe to the client"
  ls -l "$BUILD/artifacts/debug/Husnisse.dll" "$BUILD/probe/gate-client/artifacts/HusnisseGateClientProbe.dll"
  scp -q -i "$HOME/.ssh/husnisse_client_ed25519" "$BUILD/artifacts/debug/Husnisse.dll" \
      "Hermes@192.168.1.172:C:/Users/Hermes/stage/Husnisse.dll"
  scp -q -i "$HOME/.ssh/husnisse_client_ed25519" "$BUILD/probe/gate-client/artifacts/HusnisseGateClientProbe.dll" \
      "Hermes@192.168.1.172:C:/Users/Hermes/stage/HusnisseGateClientProbe.dll"
  bash "$HERE/run.sh" install | tee "$LOG/install.txt"
  bash "$HERE/run.sh" reset | tee -a "$LOG/install.txt"
  sha256sum "$BUILD/artifacts/debug/Husnisse.dll" "$BUILD/probe/gate-client/artifacts/HusnisseGateClientProbe.dll"
}

stopclient() {
  note "stopclient: stop the client (a running client locks its plugin DLLs)"
  cat > /home/hermes/.hermes/cache/scratch/client-stop.ps1 <<'PS'
$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) {
  $p | ForEach-Object { $_.CloseMainWindow() | Out-Null }
  Start-Sleep -Seconds 8
  $p = Get-Process -Name valheim -ErrorAction SilentlyContinue
  if ($p) { $p | Stop-Process -Force; Start-Sleep -Seconds 5; Write-Output "force killed" } else { Write-Output "closed" }
} else { Write-Output "not running" }
PS
  bash "$REPO/tooling/client-rig/psrun.sh" /home/hermes/.hermes/cache/scratch/client-stop.ps1 | tee -a "$LOG/stopclient.txt"
}

launch() {
  note "launch: quit any client, start the join task, wait for this launch's ready line"
  # The join task is one global scheduled task and another card's gate may have pointed it at its
  # own tree's port (0.3.1 used 2956). Point it at this card's server before starting it, or the
  # client joins a stranger's world and every reading below is of that world.
  cat > /home/hermes/.hermes/cache/scratch/client-port.ps1 <<PS
\$t = Get-ScheduledTask -TaskName 'HusnisseLegsLaunch'
\$a = \$t.Actions[0]
\$a.Arguments = [regex]::Replace(\$a.Arguments, '192\.168\.1\.136:\d+', '192.168.1.136:${GATE_PORT:-2556}')
Set-ScheduledTask -TaskName 'HusnisseLegsLaunch' -Action \$a | Out-Null
Write-Output ('task args: ' + (Get-ScheduledTask -TaskName 'HusnisseLegsLaunch').Actions[0].Arguments)
PS
  bash "$REPO/tooling/client-rig/psrun.sh" /home/hermes/.hermes/cache/scratch/client-port.ps1 | tee -a "$LOG/launch.txt"
  cat > /home/hermes/.hermes/cache/scratch/client-kill.ps1 <<'PS'
$p = Get-Process -Name valheim -ErrorAction SilentlyContinue
if ($p) {
  $p | ForEach-Object { $_.CloseMainWindow() | Out-Null }
  Start-Sleep -Seconds 8
  $p = Get-Process -Name valheim -ErrorAction SilentlyContinue
  if ($p) { $p | Stop-Process -Force; Start-Sleep -Seconds 5; Write-Output "force killed" } else { Write-Output "closed" }
} else { Write-Output "not running" }
Start-ScheduledTask -TaskName "HusnisseLegsLaunch"
Write-Output ("launched HusnisseLegsLaunch at " + (Get-Date).ToString("HH:mm:ss"))
PS
  bash "$REPO/tooling/client-rig/psrun.sh" /home/hermes/.hermes/cache/scratch/client-kill.ps1 | tee "$LOG/launch.txt"
  local i ready=""
  for i in $(seq 1 30); do
    sleep 10
    ready=$(bash "$HERE/run.sh" grep 2>/dev/null | grep -a 'gc ready' | tail -1)
    if [ -n "$ready" ]; then echo "$ready" | tee -a "$LOG/launch.txt"; break; fi
  done
  [ -n "$ready" ] || { echo "TIMEOUT waiting for gc ready"; STATUS=1; }
  P "launch-where" "where" "state" "god on"
}

scene() {
  note "scene: build the gate scene at the client's own position (this wipes earlier harness scenes)"
  # SCENE_GEN selects the layout: scene.sh (the wide p1-p7 fixture) or scene-compact.sh (p8-p13,
  # every piece within ~5 m so the rig's culling cannot eat one mid-phase).
  local gen="${SCENE_GEN:-scene.sh}"
  # A previous card's harness scene may still be standing under the client; wipe it first so the
  # player falls to real ground and the deck is generated at the height they will stand on.
  S pre-clean "$HERE/client/pre-clean.cmd"
  sleep 4
  P "scene-where" "god on" "heal" "where"
  local where x y z deck
  where=$(grep -a 'gc where player=' "$LOG/scene-where.txt" | tail -1)
  echo "$where" | tee -a "$LOG/scene.txt"
  # Three floats, parsed in one expression: a range-greedy sed on "player=[0-9-]*," silently
  # captures "0" out of "0.88" and leaves the whole line in the next field.
  read -r x y z <<< "$(printf '%s' "$where" | sed -E 's/.*player=(-?[0-9.]+),(-?[0-9.]+),(-?[0-9.]+).*/\1 \2 \3/')"
  deck=$(awk "BEGIN{printf \"%.2f\", $y+0.25}")
  echo "base x=$x y=$y z=$z -> deck $deck (generator $gen)" | tee -a "$LOG/scene.txt"
  bash "$HERE/$gen" "$(printf '%.0f' "$x")" "$deck" "$(printf '%.0f' "$z")" \
       /home/hermes/.hermes/cache/scratch/gate-scene.cmd \
       "$HERE/client/gate-03b-indices.env" | tee -a "$LOG/scene.txt"
  printf '%.0f %s %.0f\n' "$x" "$deck" "$z" > "$BASE_FILE"
  cp /home/hermes/.hermes/cache/scratch/gate-scene.cmd "$HERE/client/gate-03b-scene.cmd"
  S scene "$HERE/client/gate-03b-scene.cmd"
  # Re-read the map the generator just wrote, then prove the client can see the whole block.
  . "$HERE/client/gate-03b-indices.env"
  P "scene-containers" "containers"
}

p1() {
  note "p1: buttons on a claim chest, and none on a cart (the no-claim case is p13)"
  P "p1-where" "heal" "god on" "where"
  P "p1-open0" "close" "stand piece_chest_wood $(C panel)" "open piece_chest_wood $(C panel)"
  P "p1-ui0" "ui" "panel"
  shot gate-03b-strip-claim

  P "p1-open-cart" "close" "stand Container 0" "open Container 0"
  P "p1-ui-cart" "ui" "panel"
  shot gate-03b-strip-cart
  P "p1-back" "close" "stand piece_chest_wood $(C panel)" "open piece_chest_wood $(C panel)"
}

# The character keeps its inventory between sessions, so the gate starts from known pockets: every
# carried stack is left in the Overflow chest (a destination, never drained), and the run then puts
# exactly the stacks each test needs into them.
p0() {
  note "p0: empty the pockets into Overflow so every later count is the test's own"
  P "p0-inv" "inv"
  P "p0-dump" "stand piece_chest_wood $(C overflow)" "open piece_chest_wood $(C overflow)" \
              "move piece_chest_wood Coal 99" "move piece_chest_wood Wood 99" "move piece_chest_wood Stone 99" \
              "move piece_chest_wood Resin 99" "move piece_chest_wood Feathers 99" "inv"
  P "p0-close" "close"
}

p2() {
  note "p2: the network toggle is the world's, not the client's — close and reopen"
  P "p2-before" "ui"
  P "p2-toggle" "click HusnisseNet"
  P "p2-after" "ui"
  P "p2-close" "close"
  P "p2-reopen" "open piece_chest_wood $(C panel)"
  P "p2-read" "ui"
}

p3() {
  note "p3: the panel renders; nothing covers the vanilla grid at 1080p or at 1024x768"
  P "p3-panel" "click HusnisseRules"
  P "p3-panel-read" "ui" "panel"
  shot gate-03b-panel-1080p
  P "p3-res-narrow" "res 1024 768"
  P "p3-panel-narrow" "panel"
  shot gate-03b-panel-1024x768
  P "p3-res-back" "res 1920 1080"
  P "p3-panel-back" "panel"
}

p4() {
  note "p4: a category writes the rule and paints the button"
  P "p4-click" "click HusnisseCatMetals"
  P "p4-read" "ui" "panel"
  shot gate-03b-category-metals
}

p5() {
  note "p5: priority + and -"
  P "p5-plus" "click HusnissePriorityPlus"
  P "p5-plus-read" "ui" "panel"
  P "p5-minus" "click HusnissePriorityMinus"
  P "p5-minus-read" "ui" "panel"
}

# Items are carried in by the client: `give` puts a stack in the pockets, `move` writes it into the
# container the player has open — the same inventory write a drag makes.
p6() {
  note "p6: save contents — three kinds carried in, one unticked, applied, reopened"
  P "p6-fill" "give TrollHide 5" "move piece_chest_wood TrollHide 5" \
              "give BoneFragments 5" "move piece_chest_wood BoneFragments 5" \
              "give Coal 10" "move piece_chest_wood Coal 10" "ui"
  P "p6-open-custom" "click HusnisseCustom"
  P "p6-rows" "panel"
  shot gate-03b-save-contents
  # Row labels are localised item names, so the bone row is found by a fragment of its display name.
  local row
  row=$(grep -a 'panel el name=HusnisseCustomRow' "$LOG/p6-rows.txt" | grep -a 'vis=True' | grep -ai 'fragment' | sed 's/.*name=HusnisseCustomRow\([0-9]*\).*/\1/' | head -1)
  row="${row:-1}"
  echo "bone row = $row" | tee "$LOG/p6-row-index.txt"
  P "p6-untick" "click HusnisseCustomRow$row"
  P "p6-untick-read" "panel"
  P "p6-apply" "click HusnisseCustomApply"
  P "p6-applied" "ui" "panel"
  shot gate-03b-save-applied
  P "p6-reopen" "click HusnisseCustom"
  P "p6-reopen-read" "panel"
  shot gate-03b-save-reopen
  P "p6-close-custom" "click HusnisseCustomClose" "ui" "containers"
}

p7() {
  note "p7: the saved list routes — named item in, unnamed item to Overflow, deposit chest untouched"
  P "p7-pockets" "give TrollHide 3 1" "give BoneFragments 3 2" "inv"
  P "p7-deposit" "close" "stand piece_chest_wood $(C plain)" "open piece_chest_wood $(C plain)" "click HusnisseDepositAll"
  PW "p7-watch" 36 "watch 30 6"
  P "p7-containers" "close" "containers"
  shot gate-03b-routed
}

p8() {
  note "p8: use suggestion after clearing the rule on a non-empty chest"
  # The strip's controls resolve the *open* container. Opening and clicking in one batch can run
  # before Refresh has attached the chest (it clears _container on Hide), and the click then no-ops
  # while reporting "invoked" — so the click gets its own batch, as in p3/p6.
  P "p8-open" "close" "stand piece_chest_wood $(C panel)" "open piece_chest_wood $(C panel)"
  P "p8-rules" "click HusnisseRules"
  P "p8-metals" "give CopperScrap 5" "move piece_chest_wood CopperScrap 5" \
                "give IronScrap 5" "move piece_chest_wood IronScrap 5" "ui"
  # The transition the card asks for: a rule exists (no suggestion), the rule is cleared, the
  # suggestion appears and names the dominant category.
  P "p8-before-clear" "ui" "panel"
  P "p8-clear" "click HusnisseNoRule"
  P "p8-suggest-read" "ui" "panel"
  shot gate-03b-suggest-available
  P "p8-suggest" "click HusnisseSuggest"
  P "p8-suggest-done" "ui" "panel"
}

p9() {
  note "p9: copy to nearby — confirm line, cancel changes nothing, copy sets both"
  P "p9-copy" "click HusnisseCopy"
  P "p9-confirm" "ui" "panel"
  shot gate-03b-copy-confirm
  P "p9-cancel" "click HusnisseCopyNo"
  P "p9-cancelled" "ui" "panel" "containers"
  P "p9-copy2" "click HusnisseCopy"
  P "p9-yes" "click HusnisseCopyYes"
  P "p9-done" "ui" "panel"
  shot gate-03b-copy-done
  P "p9-containers" "close" "containers"
}

p10() {
  note "p10: deposit-all on a networked chest — the nisse files it elsewhere"
  P "p10-inv" "clear" "inv"
  P "p10-give" "give CopperScrap 5 1" "give Wood 5 2" "inv"
  P "p10-open" "close" "stand piece_chest_wood $(C plain)" "open piece_chest_wood $(C plain)"
  P "p10-click" "click HusnisseDepositAll"
  PW "p10-watch" 31 "watch 25 5"
  # The last trip can land as the watch ends, and the client's copy of the target ZDO follows one
  # revision later — read once and the timber is still one revision short (t_ab336800's first rerun
  # read zdosts=0 for a stack the server had written). Poll until the destinations show it.
  local attempt
  for attempt in $(seq 1 10); do
    P "p10-containers" "close" "containers"
    if grep -aq " container piece_chest_wood(Clone) idx=[0-9]* .*copperscrap.*zdosts=[1-9]" "$LOG/p10-containers.txt" &&
       grep -aq " container piece_chest_wood(Clone) idx=$(C wood) .*zdosts=[1-9]" "$LOG/p10-containers.txt"; then
      break
    fi
    sleep 3
  done
}

p11() {
  note "p11: deposit-all on an off-network chest — everything lands here at once and stays"
  P "p11-give" "clear" "give CopperScrap 4 1" "give Wood 4 2" "give Coal 6 3" "inv"
  P "p11-open" "close" "stand piece_chest_wood $(C dump)" "open piece_chest_wood $(C dump)"
  P "p11-click" "click HusnisseDepositAll"
  P "p11-read" "inv" "ui"
  shot gate-03b-deposit-offnet
  PW "p11-wait" 18 "watch 12 4"
  P "p11-reopen" "close" "open piece_chest_wood $(C dump)" "ui" "containers"
}

p12() {
  note "p12: the off-network chest with one slot left — one stack fits, two do not"
  P "p12-fill" "give Stone 20 1" "move piece_chest_wood Stone 20" \
               "give Resin 20 2" "move piece_chest_wood Resin 20" \
               "give Feathers 20 3" "move piece_chest_wood Feathers 20" \
               "give GreydwarfEye 20 4" "move piece_chest_wood GreydwarfEye 20" \
               "give LoxPelt 20 5" "move piece_chest_wood LoxPelt 20" \
               "give Flint 20 6" "move piece_chest_wood Flint 20" "containers"
  P "p12-give" "give WolfPelt 4 1" "give Obsidian 4 2" "give DeerHide 4 3" "inv"
  P "p12-click" "click HusnisseDepositAll"
  P "p12-read" "inv" "ui"
  shot gate-03b-deposit-full
}

p13() {
  note "p13: no claim at all, then a bowl somebody else placed (the last scenario; wipes the base)"
  P "p13-copy-log" "containers"
  bash "$HERE/readonly-scene.sh" /home/hermes/.hermes/cache/scratch/gate-readonly.cmd "$(cat "$BASE_FILE")" | tee "$LOG/p13-scene-gen.txt"
  cp /home/hermes/.hermes/cache/scratch/gate-readonly.cmd "$HERE/client/gate-03b-readonly.cmd"
  S p13-scene "$HERE/client/gate-03b-readonly.cmd"
  P "p13-containers" "containers"
  # index 0 sits inside the 424242 claim (next to the player, read first); index 1 is the chest
  # with no bowl near it, 28 m out (the rig culls far pieces, so it is read second and the
  # no-claim case has a second witness in the p13b run).
  P "p13-open-readonly" "close" "stand piece_chest_wood 0" "open piece_chest_wood 0"
  P "p13-read" "ui" "panel"
  shot gate-03b-readonly
  P "p13-click" "click HusnisseNet"
  P "p13-click-rules" "click HusnisseRules"
  P "p13-after" "ui"
  P "p13-open-noclaim" "close" "stand piece_chest_wood 1" "open piece_chest_wood 1" "ui"
  shot gate-03b-strip-noclaim
  P "p13-containers-final" "close" "containers"
}

p10relog() {
  note "p10relog: quit the client, rejoin, and read the containers again (the ZDOs, not a cache)"
  # The acceptance for t_ab336800 asks for the deposit to still be there after a relog: a fresh
  # process reading the world's ZDOs, not the same client's cached inventories. The scene is
  # untouched between the two dumps, so the role->index map still addresses the same chests.
  launch
  P "p10-relog" "close" "containers"
}

# --------------------------------------------------------------------------- dispatch

for phase in "$@"; do
  case "$phase" in
    install) install ;;
    stopclient) stopclient ;;
    launch)  launch ;;
    scene)   scene ;;
    p0)      p0 ;;
    p1)      p1 ;;
    p2)      p2 ;;
    p3)      p3 ;;
    p4)      p4 ;;
    p5)      p5 ;;
    p6)      p6 ;;
    p7)      p7 ;;
    p8)      p8 ;;
    p9)      p9 ;;
    p10)     p10 ;;
    p10relog) p10relog ;;
    p11)     p11 ;;
    p12)     p12 ;;
    p13)     p13 ;;
    all)     install; launch; scene; p0; p1; p2; p3; p4; p5; p6; p7; p8; p9; p10; p10relog; p11; p12; p13 ;;
    *)       echo "unknown phase: $phase"; exit 2 ;;
  esac
done
exit "$STATUS"
