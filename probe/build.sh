#!/usr/bin/env bash
# Build the throwaway runtime probe and drop the DLL in artifacts/.
# The probe is not part of the mod; it exists to answer one question on the rig.
set -euo pipefail
cd "$(dirname "$0")"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build NisseProbe.csproj -c Release
mkdir -p artifacts
cp bin/Release/net48/NisseProbe.dll artifacts/NisseProbe.dll
rm -rf bin obj

echo "artifacts/NisseProbe.dll"
