#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-$ROOT/tools/.engines/unity-6000.3.27f1/Editor/Unity.exe}"
if [ ! -f "$UNITY" ]; then
  echo "Unity를 찾을 수 없습니다: $UNITY" >&2
  exit 1
fi
if command -v cygpath >/dev/null 2>&1; then
  PROJECT="$(cygpath -m "$ROOT/unity")"
else
  PROJECT="$ROOT/unity"
fi
"$UNITY" -projectPath "$PROJECT" "$@"
