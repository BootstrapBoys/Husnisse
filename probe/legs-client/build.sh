#!/usr/bin/env bash
# Build the throwaway 0.1.4 client observer and drop the DLL in artifacts/.
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build HusnisseLegsClientProbe.csproj -c Release
mkdir -p artifacts
cp bin/Release/net48/HusnisseLegsClientProbe.dll artifacts/HusnisseLegsClientProbe.dll
rm -rf bin obj

echo "artifacts/HusnisseLegsClientProbe.dll"
