#!/usr/bin/env bash
# Build the 0.1.4 visual-gate server driver and drop the DLL in artifacts/.
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build HusnisseLegsVisual.csproj -c Release
mkdir -p artifacts
cp bin/Release/net48/HusnisseLegsVisual.dll artifacts/HusnisseLegsVisual.dll
rm -rf bin obj

echo "artifacts/HusnisseLegsVisual.dll"
