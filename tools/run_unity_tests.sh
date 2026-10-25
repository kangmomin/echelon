#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PLATFORM="${1:-EditMode}"
if [ "$#" -gt 0 ]; then shift; fi
mkdir -p "$ROOT/build/validation"
if command -v cygpath >/dev/null 2>&1; then
  RESULTS="$(cygpath -m "$ROOT/build/validation/unity_${PLATFORM,,}.xml")"
  LOG="$(cygpath -m "$ROOT/build/validation/unity_${PLATFORM,,}.log")"
else
  RESULTS="$ROOT/build/validation/unity_${PLATFORM,,}.xml"
  LOG="$ROOT/build/validation/unity_${PLATFORM,,}.log"
fi
"$ROOT/tools/run_unity.sh" -batchmode -nographics -runTests -testPlatform "$PLATFORM" -testResults "$RESULTS" -logFile "$LOG" "$@"
