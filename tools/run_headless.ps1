param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$HeadlessArgs
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$dotnet = $env:DOTNET
if (-not $dotnet) {
    $dotnet = Join-Path $root "tools/.dotnet/dotnet.exe"
    if (-not (Test-Path $dotnet)) {
        $dotnet = "dotnet"
    }
}
& $dotnet run --project (Join-Path $root "src/Echelon.Headless/Echelon.Headless.csproj") -- @HeadlessArgs
exit $LASTEXITCODE
