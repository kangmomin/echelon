#!/usr/bin/env bash
# 정적 타입·안전성 경고를 "오류"로 올려 프로젝트 스크립트(addons 제외)를 검사한다.
# 프로젝트 설정은 잠시 바꿨다가 끝나면 되돌린다. 사용: GODOT=/path/to/godot ./tools/check_warnings.sh
set -uo pipefail
GODOT="${GODOT:-godot}"
cd "$(dirname "$0")/.."
cp project.godot /tmp/echelon_project.godot.bak
trap 'cp /tmp/echelon_project.godot.bak project.godot' EXIT
KEYS="untyped_declaration unsafe_property_access unsafe_method_access unsafe_cast unsafe_call_argument unused_variable unused_local_constant unused_private_class_variable unused_parameter shadowed_variable shadowed_variable_base_class shadowed_global_identifier unreachable_code standalone_expression integer_division narrowing_conversion int_as_enum_without_cast static_called_on_instance confusable_identifier native_method_override"
python3 - "$KEYS" <<'PY'
import re, sys
keys = sys.argv[1].split()
s = open("project.godot").read()
block = "[debug]\n\n" + "\n".join(f"gdscript/warnings/{k}=2" for k in keys) + "\n\n"
s = re.sub(r"\[debug\]\n\n(.*?)\n\n", lambda m: block, s, flags=re.S)
open("project.godot", "w").write(s)
PY
"$GODOT" --headless --path . --import >/dev/null 2>&1 || true
fail=0; n=0
for f in $(find sim services game tests -name "*.gd" | sort); do
  n=$((n+1))
  out=$("$GODOT" --headless --path . --check-only -s "res://$f" 2>&1 | sed 's/\x1b\[[0-9;]*m//g' | grep -vE "^Godot|^\s*$" | grep -v "Failed to compile depended")
  if [ -n "$out" ]; then echo "## $f"; echo "$out" | head -10; fail=1; fi
done
echo "검사한 파일: $n"
exit $fail
