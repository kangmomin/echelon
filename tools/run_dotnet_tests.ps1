param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotnetArgs
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dotnet = $env:DOTNET
if (-not $dotnet) {
    $dotnet = Join-Path $root "tools/.dotnet/dotnet.exe"
    if (-not (Test-Path $dotnet)) {
        $dotnet = "dotnet"
    }
}
& $dotnet test (Join-Path $root "tests/csharp/Echelon.Core.Tests.csproj") --nologo @DotnetArgs
exit $LASTEXITCODE
