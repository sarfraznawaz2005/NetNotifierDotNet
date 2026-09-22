# Cleans and runs the app in dev mode (Debug build via `dotnet run`).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'NetNotifier.csproj'

# Only stop an instance launched from this project's own bin/publish folders, so we
# never touch an unrelated NetNotifier.exe elsewhere (e.g. the old AutoHotkey build).
$existing = Get-Process -Name 'NetNotifier' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase) }
if ($existing) {
    Write-Host 'Stopping previous running instance...' -ForegroundColor Cyan
    $existing | Stop-Process -Force
    Start-Sleep -Milliseconds 300
}

Write-Host 'Cleaning previous build output...' -ForegroundColor Cyan
foreach ($dir in @((Join-Path $root 'bin'), (Join-Path $root 'obj'))) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

Write-Host 'Running in dev mode...' -ForegroundColor Cyan
dotnet run --project $project -c Debug

exit $LASTEXITCODE
