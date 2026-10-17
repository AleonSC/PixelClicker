#!/usr/bin/env bash
# Compile check for Assets/Scripts without Unity. Usage: tools/compile-check/check.sh   (prints only errors; exit code 1 if any)
# First run: installs the .NET SDK (apt) if missing and downloads the Unity module assemblies (NuGet UnityEngine.Modules) into .cache/ (ignored by git).
set -e
cd "$(dirname "$0")"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "installing the .NET 8 SDK (apt)..." >&2
  (apt-get update -qq && apt-get install -y -qq dotnet-sdk-8.0) >/dev/null 2>&1 || { echo "could not install dotnet" >&2; exit 2; }
fi
if [ ! -d .cache/unity/lib/net45 ]; then
  echo "downloading the Unity module assemblies..." >&2
  mkdir -p .cache
  curl -sS -m 300 -o .cache/unity.nupkg https://api.nuget.org/v3-flatcontainer/unityengine.modules/2021.3.33/unityengine.modules.2021.3.33.nupkg
  unzip -o -q .cache/unity.nupkg -d .cache/unity
fi
OUT=$(dotnet build CompileCheck.csproj -nologo -v q -clp:NoSummary 2>&1 || true)
ERRS=$(echo "$OUT" | grep -E "error (CS|MSB|NU)" | sed -E 's#^.*/Assets/Scripts/##' | sort -u)
RULES=$(./rules.sh || true)   # project rules the compiler can't check (see rules.sh)
ERRS=$(printf '%s\n%s' "$ERRS" "$RULES" | sed '/^$/d')
if [ -n "$ERRS" ]; then echo "$ERRS"; echo "$(echo "$ERRS" | wc -l) error(s)"; exit 1; fi
echo "compile check passed (no errors)"
