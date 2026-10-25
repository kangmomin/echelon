param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$UnityArgs
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$unity = $env:UNITY
if (-not $unity) {
    $unity = Join-Path $root "tools/.engines/unity-6000.3.27f1/Editor/Unity.exe"
}
if (-not (Test-Path $unity)) {
    throw "Unity를 찾을 수 없습니다: $unity"
}
$arguments = @("-projectPath", (Join-Path $root "unity")) + @($UnityArgs)
$quotedArguments = @($arguments | ForEach-Object { '"' + $_ + '"' })
$process = Start-Process -FilePath $unity -ArgumentList $quotedArguments -Wait -PassThru -NoNewWindow
exit $process.ExitCode
