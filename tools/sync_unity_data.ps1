$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (Get-Command py -ErrorAction SilentlyContinue) {
    py -3 tools/sync_unity_data.py
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    python tools/sync_unity_data.py
} else {
    python3 tools/sync_unity_data.py
}
