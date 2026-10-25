param(
    [string]$Platform = "EditMode",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$UnityArgs
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$results = Join-Path $root "build/validation/unity_$($Platform.ToLower()).xml"
$log = Join-Path $root "build/validation/unity_$($Platform.ToLower()).log"
& (Join-Path $PSScriptRoot "run_unity.ps1") -batchmode -nographics -runTests -testPlatform $Platform -testResults $results -logFile $log @UnityArgs
exit $LASTEXITCODE
