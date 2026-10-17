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

exit $FAIL
