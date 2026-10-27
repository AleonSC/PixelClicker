#!/usr/bin/env bash
# Project rules the compiler can't check (run by check.sh after compiling; prints one line per broken rule, exit code 1 if any).
# Each rule = a text search over Assets/Scripts. Add a new 'one place for this' rule here as one more block.
cd "$(dirname "$0")/../../Assets/Scripts"
FAIL=0

# Rule: a Button component is only ever added inside PixelUIKit - real buttons by PixelUIKit.CreateButton, clickable rows /
# pictures / catchers by PixelUIKit.MakeClickable - so the button look, custom button colour and click sound live in one place.
HITS=$(grep -rnE --include='*.cs' 'AddComponent<([A-Za-z_.]*\.)?Button>|typeof\(([A-Za-z_.]*\.)?Button\)' . \
       | grep -v '^\./PixelUIKit\.cs:' | grep -vE 'RequireComponent\(typeof\(([A-Za-z_.]*\.)?Button\)\)' | grep -vE '^[^:]+:[0-9]+:\s*//' | sed 's#^\./##')
if [ -n "$HITS" ]; then
  echo "$HITS" | sed -E 's#^([^:]+:[0-9]+):.*#\1: rule: a Button is built by hand - use PixelUIKit.CreateButton (a button) or PixelUIKit.MakeClickable (a clickable row / picture)#'
  FAIL=1
fi

# Rule: a script that registers with PixelWindows (so Escape closes it) builds its window with PixelUIKit.CreateWindow (the one standard
# window: header, title, X, chrome, theme colour). A registration that only takes Escape for a MODE (placing, first person, a show...) says so
# on its line with a comment containing 'escape-only:' and the reason; a window built with CreateWindow in another file of the same class says
# 'window-built-in: <file>'.
for f in $(grep -rl --include='*.cs' 'PixelWindows\.Register(' .); do
  case "$f" in ./PixelWindows.cs) continue;; esac
  grep -q 'CreateWindow(' "$f" && continue
  BAD=$(grep -n 'PixelWindows\.Register(' "$f" | grep -vE '^[0-9]+:\s*//' | grep -v 'escape-only:' | grep -v 'window-built-in:' | cut -d: -f1)
  for n in $BAD; do
    echo "${f#./}:$n: rule: a window registered with PixelWindows must be built with PixelUIKit.CreateWindow (or mark the line '// escape-only: <reason>' / '// window-built-in: <file>')"
    FAIL=1
  done
done

# Rule: never tell 'was a husk made?' by comparing OldPixels.Count before / after a spawn - at the floor cap a new husk pushes the OLDEST off
# the list, so the count stays the same although a husk WAS made (machines then kept their husk and spawned it again: endless copies, the
# 2026-10-27 audit). Use PixelClicker.SpawnStoredHusk, which hands back the husk it made (null = none).
HITS=$(grep -rnE --include='*.cs' 'OldPixels\.Count\s*(<=|>|==|!=)\s*before|before\s*=\s*[A-Za-z_.]*OldPixels\.Count' . | grep -vE '^[^:]+:[0-9]+:\s*//' | sed 's#^\./##')
if [ -n "$HITS" ]; then
  echo "$HITS" | sed -E 's#^([^:]+:[0-9]+):.*#\1: rule: a husk spawn checked by comparing OldPixels.Count - use PixelClicker.SpawnStoredHusk (returns the husk it made; the count does not change at the floor cap)#'
  FAIL=1
fi

# Rule: object id numbers come from PixelObjectId.Of - GetInstanceID() is an error on newer Unity (6.3+: GetEntityId) and the offline check
# compiles against older Unity, so it would not notice; the version switch lives only in PixelObjectId.cs.
HITS=$(grep -rnE --include='*.cs' 'GetInstanceID\(|GetEntityId\(' . | grep -v '^\./PixelObjectId\.cs:' | grep -vE '^[^:]+:[0-9]+:\s*//' | sed 's#^\./##')
if [ -n "$HITS" ]; then
  echo "$HITS" | sed -E 's#^([^:]+:[0-9]+):.*#\1: rule: GetInstanceID / GetEntityId called directly - use PixelObjectId.Of(obj) (works on old and new Unity)#'
  FAIL=1
fi

# Rule: a system whose progress moved into the GameState (listed in GameState.MovedSystems, docs/systems/PixelGameState.md) no longer
# needs no restart reset for it: its script must not use PixelStatics.ResetOnRestart (the old IPixelSaved / PixelSaveRegistry are gone since 2026-10-24).
# A restart reset that clears something that is NOT progress (e.g. the old scene's event listeners) says so on its line:
# '// restart-reset-not-progress: <what>'.
MOVED=$(sed -n 's/.*MovedSystems = {\(.*\)};.*/\1/p' GameState.cs | tr -d ' "' | tr ',' ' ')
for sys in $MOVED; do
  [ -f "$sys.cs" ] || { echo "GameState.cs: rule: GameState.MovedSystems names '$sys' but there is no $sys.cs"; FAIL=1; continue; }
  BAD=$(grep -nE 'PixelStatics\.ResetOnRestart' "$sys.cs" | grep -vE '^[0-9]+:\s*//' | grep -v 'restart-reset-not-progress:' | cut -d: -f1)
  for n in $BAD; do
    echo "$sys.cs:$n: rule: $sys moved to the GameState (GameState.MovedSystems) - its progress saves and resets with PixelGameState, so no ResetOnRestart here"
    FAIL=1
  done
done

exit $FAIL
