#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-$ROOT/tools/.dotnet/dotnet}"
if [ ! -f "$DOTNET" ]; then
  DOTNET="$ROOT/tools/.dotnet/dotnet.exe"
fi
if [ ! -f "$DOTNET" ]; then
  DOTNET=dotnet
fi
"$DOTNET" test "$ROOT/tests/csharp/Echelon.Core.Tests.csproj" --nologo "$@"
