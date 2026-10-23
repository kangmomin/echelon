# GUT 단위 테스트를 헤드리스로 실행한다.
# 사용: $env:GODOT = "C:\...\Godot_v4.7.2-stable_win64_console.exe"; .\tools\run_tests.ps1
param([string]$Godot = $(if ($env:GODOT) { $env:GODOT } else { "godot" }))
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
# 클래스 캐시(.godot/)가 없으면 class_name 을 찾지 못하므로 먼저 import 한다.
& $Godot --headless --path . --import *> $null
& $Godot --headless --path . -s res://addons/gut/gut_cmdln.gd -gconfig=res://.gutconfig.json
exit $LASTEXITCODE
