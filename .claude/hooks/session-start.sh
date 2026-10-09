#!/bin/bash
# Cloud sessions only: install the .NET SDK and restore the Unity reference DLLs so
# tools/CompileCheck/check.sh (compile check for Assets/Scripts) is ready to run.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
  SUDO=""; [ "$(id -u)" -ne 0 ] && SUDO="sudo"
  $SUDO apt-get update -qq >/dev/null 2>&1
  $SUDO apt-get install -y -qq dotnet-sdk-8.0 >/dev/null 2>&1
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1' >> "${CLAUDE_ENV_FILE:-/dev/null}"

dotnet restore "$CLAUDE_PROJECT_DIR/tools/CompileCheck/PixelClicker.Check.csproj" -v q >/dev/null
