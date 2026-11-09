#!/usr/bin/env bash
# Dev build: compile and drop the DLL in artifacts/debug for in-game testing.
set -euo pipefail
cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build Husnisse.csproj -c Release
mkdir -p artifacts/debug
cp bin/Release/net48/Husnisse.dll artifacts/debug/Husnisse.dll

echo "artifacts/debug/Husnisse.dll"
