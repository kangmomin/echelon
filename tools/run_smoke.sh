#!/usr/bin/env bash
# 메인 씬을 헤드리스로 N틱 돌리고 결과를 출력한다(샌드박스, 그다음 기본 편성 스커미시).
# 사용: GODOT=/path/to/godot ./tools/run_smoke.sh [틱 수]
set -euo pipefail
GODOT="${GODOT:-godot}"
TICKS="${1:-50}"
cd "$(dirname "$0")/.."
"$GODOT" --headless --path . --import >/dev/null 2>&1 || true
"$GODOT" --headless --path . -- --smoke-ticks="$TICKS" --scene=sandbox
"$GODOT" --headless --path . -- --smoke-ticks="$TICKS" --scene=skirmish --start
