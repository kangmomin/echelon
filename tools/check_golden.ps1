# 8개 시드 AI 대 AI 전투를 C# 헤드리스로 돌려 tests/golden 과 비교한다.
# 사용: .\tools\check_golden.ps1 [-Seeds 1,2,3,4,5,6,7,8]
param([string]$Seeds = "1,2,3,4,5,6,7,8")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$out = Join-Path $root "build/golden_check"
& (Join-Path $PSScriptRoot "run_headless.ps1") --snapshots "--seeds=$Seeds" --detail=1,8 "--out=$out"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
# 엔진 상태 해시는 Godot 내부 알고리즘이라 재현 대상이 아니다. 나머지 상태는 1e-12 안에서 같아야 한다.
py -3 tools/compare_golden.py "--actual=$out" "--seeds=$Seeds" --ignore-prefix=engine_hashes --float-tolerance=1e-12 "--json=$(Join-Path $root 'build/validation/golden_check.json')"
exit $LASTEXITCODE
