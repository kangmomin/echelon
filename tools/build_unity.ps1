param(
    [ValidateSet("all", "windows", "linux", "web")]
    [string]$Target = "all"
)
$ErrorActionPreference = "Stop"
$method = switch ($Target) {
    "windows" { "BuildWindows" }
    "linux" { "BuildLinux" }
    "web" { "BuildWeb" }
    default { "BuildAll" }
}
$root = Split-Path -Parent $PSScriptRoot
$log = Join-Path $root "build/validation/unity_$Target.log"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $log) | Out-Null
& (Join-Path $PSScriptRoot "run_unity.ps1") -batchmode -nographics -quit -executeMethod "Echelon.Unity.Editor.EchelonBuild.$method" -logFile $log
exit $LASTEXITCODE
