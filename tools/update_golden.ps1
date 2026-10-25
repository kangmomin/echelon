# 규칙을 의도적으로 바꾼 뒤 8개 시드 골든 기준값(tests/golden/battle)을 C# 출력으로 다시 만든다.
# 사용: .\tools\update_golden.ps1  → .\tools\check_golden.ps1 로 확인하고, 바꾼 이유를 docs/DECISIONS.md 에 남긴다.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "build/golden_update"
$golden = Join-Path $root "tests/golden/battle"
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
& (Join-Path $PSScriptRoot "run_headless.ps1") --snapshots --seeds=1,2,3,4,5,6,7,8 --detail=1,8 "--out=$out"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
foreach ($seed in 1..8) {
    $target = Join-Path $golden "seed_$seed"
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    Copy-Item -Recurse (Join-Path $out "seed_$seed") $target
}
# Godot 실행 기록(엔진·입력 해시)은 C# 로 다시 만든 기준값과 맞지 않으므로 지운다.
Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $golden "run_manifest.json"), (Join-Path $golden "validation.json")
Write-Output "[golden] updated $golden from C# output"
