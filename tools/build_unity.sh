#!/usr/bin/env bash
set -euo pipefail
TARGET="${1:-all}"
case "$TARGET" in
  windows) METHOD=BuildWindows ;;
  linux) METHOD=BuildLinux ;;
  web|webgl) METHOD=BuildWeb ;;
  all) METHOD=BuildAll ;;
  *) echo "사용법: $0 [all|windows|linux|web]" >&2; exit 2 ;;
esac
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$ROOT/build/validation"
LOG="$ROOT/build/validation/unity_${TARGET}.log"
if command -v cygpath >/dev/null 2>&1; then
  LOG="$(cygpath -m "$LOG")"
fi
"$ROOT/tools/run_unity.sh" -batchmode -nographics -quit -executeMethod "Echelon.Unity.Editor.EchelonBuild.$METHOD" -logFile "$LOG"
