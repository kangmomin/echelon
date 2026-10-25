#!/usr/bin/env bash
# 규칙을 의도적으로 바꾼 뒤 8개 시드 골든 기준값(tests/golden/battle)을 C# 출력으로 다시 만든다.
# 사용: ./tools/update_golden.sh  → ./tools/check_golden.sh 로 확인하고, 바꾼 이유를 docs/DECISIONS.md 에 남긴다.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/build/golden_update"
GOLDEN="$ROOT/tests/golden/battle"
rm -rf "$OUT"
"$ROOT/tools/run_headless.sh" --snapshots --seeds=1,2,3,4,5,6,7,8 --detail=1,8 --out="$OUT"
for seed in 1 2 3 4 5 6 7 8; do
  rm -rf "$GOLDEN/seed_$seed"
  cp -R "$OUT/seed_$seed" "$GOLDEN/seed_$seed"
done
# Godot 실행 기록(엔진·입력 해시)은 C# 로 다시 만든 기준값과 맞지 않으므로 지운다.
rm -f "$GOLDEN/run_manifest.json" "$GOLDEN/validation.json"
echo "[golden] updated $GOLDEN from C# output"
