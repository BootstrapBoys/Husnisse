#!/usr/bin/env bash
# Judge the 0.3b client gate from the collected logs. One line per check, PASS/FAIL, final counts.
# Reads client/logs/*.txt exactly as captured: the client's own `ui` / `panel` / `containers`
# readings and the HUD lines the probe sampled. Nothing is re-run.
#
# p1-p7 are the wide `scene.sh` fixture and still match by coordinate; p8-p13 are the compact
# fixture and match by role, because the two layouts put the same chest at different coordinates
# (see `chest` below).
#
#   usage: verify.sh
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG="$HERE/client/logs"
BASE_FILE="${GATE_BASE:-$HOME/.hermes/cache/scratch/gate-base.txt}"
pass=0; fail=0

if [ ! -f "$BASE_FILE" ]; then
  echo "no gate base ($BASE_FILE) — run driver.sh scene first"; exit 2
fi
read -r X Y Z <<< "$(cat "$BASE_FILE")"
PIECE=$(awk "BEGIN{printf \"%.2f\", $Y+0.10}")
pos() { printf '%d.00,%s,%d.00' "$((X + $1))" "$PIECE" "$((Z + $2))"; }

expect() {
  local file="$LOG/$1"; local pattern="$2"; local what="$3"
  if [ -f "$file" ] && grep -aqE "$pattern" "$file"; then
    printf 'PASS  %s\n' "$what"; pass=$((pass+1))
  else
    printf 'FAIL  %-56s  [%s]\n' "$what" "$pattern"; fail=$((fail+1))
  fi
}

refute() {
  local file="$LOG/$1"; local pattern="$2"; local what="$3"
  if [ -f "$file" ] && grep -aqE "$pattern" "$file"; then
    printf 'FAIL  %-56s  (unexpected %s)\n' "$what" "$pattern"; fail=$((fail+1))
  else
    printf 'PASS  %s\n' "$what"; pass=$((pass+1))
  fi
}

# The container line for one chest, from a client `containers` dump.
#
# The probe prints `idx=N` on every container line and the scene generator writes the role->index
# map beside the scenario, so a check names a role and not a coordinate. The coordinates in the
# older p8-p13 checks were the wide `scene.sh` layout: against the compact fixture every one of them
# missed, which is why this file reported 32 failures on a run that had none.
INDICES="${GATE_INDICES:-$HERE/client/gate-03b-indices.env}"
if [ -f "$INDICES" ]; then
  # shellcheck disable=SC1090
  . "$INDICES"
fi
C() { local v="IDX_$1"; printf '%s' "${!v:-}"; }

# Only the chest prefab: the world also holds TreasureChest_* at their own index 0, and a bare
# " idx=0 " would match one of those first.
chest() {
  local f="$LOG/$1" role="$2" prefab="${3:-piece_chest_wood}" idx
  idx=$(C "$role")
  [ -n "$idx" ] && grep -a " container $prefab(Clone) idx=$idx " "$f" | tail -1
}

# expect_chest <file> <role> <regex> <what> — the newest dump line for that role must match.
expect_chest() {
  local file="$LOG/$1" role="$2" pattern="$3" what="$4" line
  line=$(chest "$1" "$role")
  if [ -n "$line" ] && printf '%s' "$line" | grep -qE "$pattern"; then
    printf 'PASS  %s\n' "$what"; pass=$((pass+1))
  else
    printf 'FAIL  %-56s  [%s idx=%s]\n' "$what" "$pattern" "$(C "$role")"; fail=$((fail+1))
  fi
}

# refute_chest <file> <role> <regex> <what> — the line must exist and must not match. A missing
# reading fails: "the chest got nothing" proved by a dump that never ran is not a proof.
refute_chest() {
  local file="$LOG/$1" role="$2" pattern="$3" what="$4" line
  line=$(chest "$1" "$role")
  if [ -z "$line" ]; then
    printf 'FAIL  %-56s  (no dump line for idx=%s)\n' "$what" "$(C "$role")"; fail=$((fail+1))
  elif printf '%s' "$line" | grep -qE "$pattern"; then
    printf 'FAIL  %-56s  (unexpected %s in idx=%s)\n' "$what" "$pattern" "$(C "$role")"; fail=$((fail+1))
  else
    printf 'PASS  %s\n' "$what"; pass=$((pass+1))
  fi
}

# expect_any_chest <file> <regex> <what> — the newest dump of every chest must contain a line that
# matches. Used where the router legitimately has more than one home: p9's copy gives the two
# neighbours the metals rule, and the tie-break is nearest the bowl, so naming one chest would
# assert a choice the router is allowed to make the other way.
expect_any_chest() {
  local file="$LOG/$1" pattern="$2" what="$3"
  if [ -f "$file" ] && grep -a " container piece_chest_wood(Clone) idx=" "$file" | grep -qE "$pattern"; then
    printf 'PASS  %s\n' "$what"; pass=$((pass+1))
  else
    printf 'FAIL  %-56s  [%s]\n' "$what" "$pattern"; fail=$((fail+1))
  fi
}

# expect_any <what> <regex> <file>... — a line in any of these captured batches. A server message
# lands in whichever batch was listening when it arrived (5 s after the click is the common case,
# so the click batch and the watch that follows are one reading).
expect_any() {
  local what="$1" pattern="$2"; shift 2
  local f
  for f in "$@"; do
    if [ -f "$LOG/$f" ] && grep -aqE "$pattern" "$LOG/$f"; then
      printf 'PASS  %s\n' "$what"; pass=$((pass+1)); return
    fi
  done
  printf 'FAIL  %-56s  [%s]\n' "$what" "$pattern"; fail=$((fail+1))
}

echo "base $X $Y $Z — chest pieces at y=$PIECE"

# Sections 1-7 are the wide `scene.sh` fixture and were proven on it (card t_3f054844); they read
# device geometry and dump coordinates that only exist in that layout, in that world. Set
# GATE_WIDE=1 to judge them from its own logs; a compact (p8-p13) run skips them rather than
# comparing its readings against another world's coordinates.
if [ "${GATE_WIDE:-0}" = "1" ]; then
echo
echo "=== 1. the strip: claim chest / outside the claim / a cart"
expect p1-ui0.txt        "ui container=.* strip=True netClickable=True rulesClickable=True" "claim chest: strip drawn and clickable"
expect p1-ui0.txt        "panel el name=HusnisseNet .*label='Off the network'"              "button 1 reads Off the network"
expect p1-ui0.txt        "panel el name=HusnisseDepositAll .*label='Deposit all'"          "button 2 is Deposit all (0.3c)"
expect p1-ui0.txt        "panel el name=HusnisseRules .*label='Rules: No rule'"            "button 3 reads Rules: No rule"
expect p1-ui-outside.txt "strip=False"                                                     "chest outside the claim: no strip"
expect p1-ui-cart.txt    "strip=False"                                                     "cart: no strip (blacklisted)"

echo
echo "=== 2. the network toggle is world state and survives closing the window"
expect p2-before.txt "net='Off the network'"       "before the click: off"
expect p2-after.txt  "net='On the network'"        "after the click: on"
expect p2-after.txt  "zdo net=True"                "the ZDO carries it"
expect p2-read.txt   "net='On the network'"        "closed and reopened: still on"
expect p2-read.txt   "zdo net=True"                "and the ZDO still carries it"

echo
echo "=== 3. the panel renders and never covers the vanilla grid"
expect p3-panel-read.txt "panel el name=HusnisseCatMetals .*vis=True"        "categories drawn (Metals)"
expect p3-panel-read.txt "panel el name=HusnisseCatTools .*vis=True"         "categories drawn (Tools)"
expect p3-panel-read.txt "panel el name=HusnisseOverflow .*label='Overflow'"  "Overflow"
expect p3-panel-read.txt "panel el name=HusnisseDeposit .*label='Deposit'"    "Deposit"
expect p3-panel-read.txt "panel el name=HusnisseNoRule .*label='No rule'"     "No rule"
expect p3-panel-read.txt "panel el name=HusnisseCustom .*label='Save contents'" "Save contents"
expect p3-panel-read.txt "panel el name=HusnissePriorityLabel .*label='Priority'" "Priority label"
expect p3-panel-read.txt "panel el name=HusnissePriorityValue .*label='0'"    "Priority value 0"
expect p3-panel-read.txt "panel el name=HusnisseCopy .*label='Copy to nearby'" "Copy to nearby"
expect p3-panel-read.txt "panel el name=HusnisseSuggest .*vis=False"          "Use suggestion hidden (no rule pending)"
expect p3-panel-read.txt "panel overlap HusnisseRulesPanel vs grid = 0.0"     "1080p: panel does not cover the grid"
expect p3-panel-read.txt "panel overlap HusnisseRulesPanel vs grid = 0.0 vs window = 0.0" "1080p: panel beside the window, off the grid"
expect p3-panel-read.txt "panel pairs HusnisseRulesPanel leaves=[0-9]+ overlapping=0" "1080p: no two controls overlap"
expect p3-panel-narrow.txt "screen=1024x768"                                  "narrow window: 1024x768 took effect"
expect p3-panel-narrow.txt "panel overlap HusnisseRulesPanel vs grid = 0.0"    "narrow: panel does not cover the grid"
expect p3-panel-narrow.txt "panel overlap HusnisseRulesPanel vs grid = 0.0 vs window = 0.0" "narrow: panel beside the window, off the grid"
expect p3-panel-narrow.txt "panel pairs HusnisseRulesPanel leaves=[0-9]+ overlapping=0" "narrow: no two controls overlap"

echo
echo "=== 4. a category click writes the rule and paints the button"
expect p4-read.txt "panel el name=HusnisseCatMetals .*color=#FFD65C" "Metals painted amber"
expect p4-read.txt "panel title='Rules: Metals'"                     "title bar reads Rules: Metals"
expect p4-read.txt "zdo net=True rule='v1;k=cat;c=metals'"           "ZDO carries v1;k=cat;c=metals"
expect p4-read.txt "panel el name=HusnisseSuggest .*vis=False"       "Use suggestion still hidden while a rule exists"

echo
echo "=== 5. priority + and -"
expect p5-plus-read.txt  "priority='1'"                               "the + click reads 1"
expect p5-plus-read.txt  "zdo net=True rule='v1;k=cat;c=metals;p=1'"  "ZDO carries p=1"
expect p5-plus-read.txt  "panel title='Rules: Metals p1'"             "title bar shows the priority"
expect p5-minus-read.txt "priority='0'"                               "the - click reads 0"
expect p5-minus-read.txt "zdo net=True rule='v1;k=cat;c=metals'"      "ZDO is back to no priority"

echo
echo "=== 6. save contents: untick one, apply, reopen"
ROW=$(sed 's/.*= *//' "$LOG/p6-row-index.txt" 2>/dev/null | head -1)
ROW="${ROW:-1}"
expect p6-rows.txt        "panel el name=HusnisseCustomTitle .*label='Save these items \(3\)'" "three rows for three kinds"
expect p6-rows.txt        "panel el name=HusnisseCustomRow0 .*label='\[x\] .* x"               "rows open ticked, with counts"
expect p6-untick-read.txt "panel el name=HusnisseCustomRow$ROW .*label='\[ \] "                 "the clicked row (bone) shows [ ]"
expect p6-untick-read.txt "panel el name=HusnisseCustomRow0 .*label='\[x\] "                  "the troll-hide row is still ticked"
expect p6-applied.txt     "panel title='Rules: Save contents \(2\)'"                           "title bar names the saved list and its size"
expect p6-applied.txt     "zdo net=True rule='v1;k=custom;c=none;i="                          "ZDO carries the custom list"
expect p6-reopen-read.txt "panel el name=HusnisseCustomRow$ROW .*label='\[ \] "                 "reopened: the unticked row is still unticked"
expect p6-reopen-read.txt "panel el name=HusnisseCustomRow0 .*label='\[x\] "                    "reopened: the ticked row is still ticked"

echo
echo "=== 7. the saved list routes; what it does not name goes to Overflow"
expect p7-deposit.txt     "click HusnisseDepositAll invoked"                 "the deposit-all click reached the handler"
expect p7-watch.txt       "hud center='The nisse takes [0-9]+ stack"         "server acknowledged the order with a count"
expect p7-watch.txt       "hud center='The nisse files [0-9]+ stack"         "the nisse filed the stacks"
expect p7-containers.txt  "container piece_chest_wood\(Clone\) at $(pos 2 2) .*\[.*trollhide"  "the troll hide is in the chest whose list names it"
expect p7-containers.txt  "container piece_chest_wood\(Clone\) at $(pos 9 -10) .*bonefragments" "the bone is in Overflow"
else
  echo "SKIP  sections 1-7: the wide scene.sh fixture, proven on card t_3f054844 (set GATE_WIDE=1)"
fi

echo
echo "=== 8. use suggestion on a non-empty chest with no rule"
expect p8-suggest-read.txt "suggestActive=True"                       "the suggestion appears once the rule is cleared"
expect p8-suggest-read.txt "suggest='Use suggestion: Metals'"         "and it names the dominant category"
expect p8-suggest-done.txt "rule='v1;k=cat;c=metals'"                 "clicking it writes that category"

echo
echo "=== 9. copy to nearby behind a confirm"
expect p9-confirm.txt   "status='Copy this rule to 2 nearby container\(s\)\?'" "confirm line names the two neighbours"
expect p9-cancelled.txt "status=''"                                          "Cancel clears the confirm"
refute_chest p9-cancelled.txt copyA "rule='v1;k=cat;c=metals'" "Cancel wrote nothing to copy target A"
refute_chest p9-cancelled.txt copyB "rule='v1;k=cat;c=metals'" "Cancel wrote nothing to copy target B"
expect p9-yes.txt      "hud center='Rule copy sent for 2 container\(s\)\.'"  "Copy reports the two writes"
expect_chest p9-containers.txt copyA "rule='v1;k=cat;c=metals'" "copy target A now carries the rule"
expect_chest p9-containers.txt copyB "rule='v1;k=cat;c=metals'" "copy target B now carries the rule"

echo
echo "=== 10. deposit-all on a networked chest files the pockets elsewhere"
expect p10-give.txt     "inv stacks=2 items=10"                    "pockets hold a metal and a timber"
expect p10-click.txt    "click HusnisseDepositAll invoked"          "the click reached the handler"
expect_any "the server accepted both stacks" "hud center='The nisse takes 2 stack" p10-click.txt p10-watch.txt
expect_any_chest p10-containers.txt "copperscrap"                   "the copper went into a metals chest"
expect_chest p10-containers.txt wood   "\[.*woodx[0-9]"             "the timber went to the wood chest"
refute_chest p10-containers.txt plain  "copperscrap"                "the chest the button was pressed on got no copper"
refute_chest p10-containers.txt plain  "woodx[0-9]"                 "…and no timber"
# The persistence half of the same row (t_ab336800): the ZDO carries the stacks too, not just the
# live inventory. A deposit that only ever existed in memory reads zdosts=0 here and is gone on relog.
# The copper is checked with expect_any_chest because p9's copy gave the two neighbours the metals
# rule, and the router's tie-break (nearest the bowl) may legitimately choose one of them.
expect_any_chest p10-containers.txt "copperscrap.*zdosts=[1-9]"     "…and the copper's chest carries it in its ZDO"
expect_chest p10-containers.txt wood   "zdosts=[1-9]"               "…and the wood chest's ZDO carries the timber"
# And after a relog — a fresh client process reading the world's ZDOs. This is the half the old
# headless gate could not see and the half that failed on d35096e2 (0.3b item 9).
expect_any_chest p10-relog.txt "copperscrap.*zdosts=[1-9]"          "relog: the copper's chest still carries it"
expect_chest p10-relog.txt wood   "woodx[0-9].*zdosts=[1-9]"   "relog: the wood chest still holds the timber"
refute_chest p10-relog.txt plain  "copperscrap"                     "relog: the chest the button was pressed on still got no copper"
refute_chest p10-relog.txt plain  "woodx[0-9]"                      "relog: …and still no timber"

echo
echo "=== 11. deposit-all on an off-network chest dumps at once and stays"
expect p11-click.txt   "click HusnisseDepositAll invoked"          "the click reached the handler"
expect_any "the HUD reports the dump" "hud center='Deposited 3 stack\(s\)\." p11-click.txt p11-read.txt
expect_chest p11-reopen.txt dump "stacks=3"                         "all three stacks are in that chest"
expect_chest p11-reopen.txt dump "zdosts=3"                         "…and its ZDO carries them"
expect p11-reopen.txt  "ui container="                             "the chest reopened"

echo
echo "=== 12. the off-network chest with one slot left: one fits, two do not"
expect_chest p12-fill.txt dump "stacks=9"                          "nine of ten slots are full"
expect p12-read.txt "inv .*stacks=2"                                               "the two that did not fit are still in the pockets"
expect_any "the message names both numbers" "hud center='Deposited 1 stack\(s\); 2 would not fit\.'" p12-click.txt p12-read.txt

echo
echo "=== 13. read-only: a bowl somebody else placed"
# p13 builds its own scene and needs a world where no earlier scene is still standing: the client
# keeps chests whose ZDO the server destroyed (the pieces come back when it walks near), so in a
# world that already ran p8-p12 `stand piece_chest_wood 0` opens the old scene's chest and every
# reading below is about the wrong chest. That run is a separate world by design — set GATE_P13=1
# when the logs came from it, and the checks are skipped (not failed) when they did not.
if [ "${GATE_P13:-0}" = "1" ]; then
  expect p13-read.txt  "rules='Rules: No rule . read only'"            "the rule button says read only"
  expect p13-read.txt  "netClickable=False"                            "the network button is greyed"
  expect p13-read.txt  "rulesClickable=False"                          "the rule button is greyed"
  expect p13-click.txt "click HusnisseNet skipped: not interactable"   "a click on it does nothing"
else
  echo "SKIP  p13: needs its own fresh world (set GATE_P13=1 when the logs are from that run)"
fi

echo
echo "verified=$pass failed=$fail"
[ "$fail" -eq 0 ]
