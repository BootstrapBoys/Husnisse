#!/usr/bin/env bash
# Build the throwaway gate client driver and drop the DLL in artifacts/.
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build HusnisseGateClientProbe.csproj -c Release
mkdir -p artifacts
cp bin/Release/net48/HusnisseGateClientProbe.dll artifacts/HusnisseGateClientProbe.dll
rm -rf bin obj

echo "artifacts/HusnisseGateClientProbe.dll"
