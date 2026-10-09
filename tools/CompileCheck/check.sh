#!/usr/bin/env bash
# Compile-checks Assets/Scripts without Unity (see README.md).
# Usage: tools/CompileCheck/check.sh            -> both variants
#        tools/CompileCheck/check.sh Editor     -> one variant (Editor | PlayerInputSystem)
# Exit code 0 = no compile errors.
set -uo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../.." && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet not found - installing the .NET 8 SDK..." >&2
    if command -v apt-get >/dev/null 2>&1; then
        SUDO=""; [ "$(id -u)" -ne 0 ] && SUDO="sudo"
        $SUDO apt-get update -qq >/dev/null 2>&1
        $SUDO apt-get install -y -qq dotnet-sdk-8.0 >/dev/null 2>&1
    fi
    command -v dotnet >/dev/null 2>&1 || { echo "Could not install dotnet. Install the .NET 8 SDK and re-run." >&2; exit 2; }
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

variants=("$@")
[ ${#variants[@]} -eq 0 ] && variants=(Editor PlayerInputSystem)

status=0
for v in "${variants[@]}"; do
    out="$(dotnet build "$here/PixelClicker.Check.csproj" -nologo -v q -p:CheckVariant="$v" 2>&1)"
    rc=$?
    errors="$(printf '%s\n' "$out" | grep -E ': error ' | sed "s|$root/||g; s| \[[^]]*\]$||" | sort -u)"
    warnings="$(printf '%s\n' "$out" | grep -E 'Assets/Scripts/.*: warning ' | sed "s|$root/||g; s| \[[^]]*\]$||" | sort -u)"
    if [ $rc -eq 0 ]; then
        echo "[$v] OK - no compile errors"
    else
        status=1
        echo "[$v] FAILED"
        if [ -n "$errors" ]; then printf '%s\n' "$errors"; else printf '%s\n' "$out" | tail -20; fi
    fi
    [ -n "$warnings" ] && printf '[%s] warnings:\n%s\n' "$v" "$warnings"
done
exit $status
