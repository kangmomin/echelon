#!/usr/bin/env bash
# GUT 단위 테스트를 헤드리스로 실행한다. 사용: GODOT=/path/to/godot ./tools/run_tests.sh
set -euo pipefail
GODOT="${GODOT:-godot}"
cd "$(dirname "$0")/.."
# 클래스 캐시(.godot/)가 없으면 class_name 을 찾지 못하므로 먼저 import 한다.
"$GODOT" --headless --path . --import >/dev/null 2>&1 || true
"$GODOT" --headless --path . -s res://addons/gut/gut_cmdln.gd -gconfig=res://.gutconfig.json
