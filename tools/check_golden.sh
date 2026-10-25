#!/usr/bin/env bash
# 8개 시드 AI 대 AI 전투를 C# 헤드리스로 돌려 tests/golden 과 비교한다.
# 사용: ./tools/check_golden.sh [시드 목록, 기본 1,2,3,4,5,6,7,8]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SEEDS="${1:-1,2,3,4,5,6,7,8}"
OUT="$ROOT/build/golden_check"
PYTHON=python3
python3 -c "pass" >/dev/null 2>&1 || PYTHON="py -3"
cd "$ROOT"
"$ROOT/tools/run_headless.sh" --snapshots --seeds="$SEEDS" --detail=1,8 --out="$OUT"
# 엔진 상태 해시는 Godot 내부 알고리즘이라 재현 대상이 아니다. 나머지 상태는 1e-12 안에서 같아야 한다.
$PYTHON tools/compare_golden.py --actual="$OUT" --seeds="$SEEDS" \
  --ignore-prefix=engine_hashes --float-tolerance=1e-12 --json="$ROOT/build/validation/golden_check.json"
