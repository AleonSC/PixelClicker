#!/usr/bin/env bash
# Headless test suites. Usage: tools/tests/run.sh [filter]   (filter = part of "Suite/test name"; exit code 1 on failures)
# Builds the game with the compile check first, then runs tools/tests/Suites.cs against that DLL under Mono (no Unity needed).
set -e
cd "$(dirname "$0")"
../compile-check/check.sh
command -v mono >/dev/null 2>&1 || { echo "installing mono (apt)..." >&2; apt-get install -y -qq mono-runtime >/dev/null 2>&1 || { echo "could not install mono" >&2; exit 2; }; }
OUT=$(dotnet build Suites.csproj -nologo -v q -clp:NoSummary 2>&1 || true)
if echo "$OUT" | grep -qE "error"; then echo "$OUT" | grep -E "error" | sort -u; exit 1; fi
cp ../compile-check/.cache/out/net48/*.dll .cache/out/net48/ 2>/dev/null || true
mono .cache/out/net48/Suites.exe "$@"
