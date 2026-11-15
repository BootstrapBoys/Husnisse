#!/usr/bin/env bash
# Build the throwaway 0.2b client claim probe and drop the DLL in artifacts/.
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build HusnisseClaimProbe.csproj -c Release
mkdir -p artifacts
cp bin/Release/net48/HusnisseClaimProbe.dll artifacts/HusnisseClaimProbe.dll
rm -rf bin obj

echo "artifacts/HusnisseClaimProbe.dll"
