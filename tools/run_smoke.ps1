# 메인 씬을 헤드리스로 N틱 돌리고 결과를 출력한다.
# 사용: $env:GODOT = "C:\...\Godot_v4.7.2-stable_win64_console.exe"; .\tools\run_smoke.ps1 [-Ticks 50]
param(
    [int]$Ticks = 50,
    [string]$Godot = $(if ($env:GODOT) { $env:GODOT } else { "godot" })
)
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
& $Godot --headless --path . --import *> $null
& $Godot --headless --path . -- "--smoke-ticks=$Ticks"
exit $LASTEXITCODE
