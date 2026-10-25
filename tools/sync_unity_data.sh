#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if python3 -c "pass" >/dev/null 2>&1; then
  python3 tools/sync_unity_data.py
elif command -v py >/dev/null 2>&1; then
  py -3 tools/sync_unity_data.py
else
  python tools/sync_unity_data.py
fi
