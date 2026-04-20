param(
    [switch]$OpenFrontend
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendPath = Join-Path $repoRoot 'src\presentation'
$frontendPath = Join-Path $repoRoot 'src\presentation\ClientApp'

if (-not (Test-Path $backendPath)) {
    throw "Backend path not found: $backendPath"
}

if (-not (Test-Path $frontendPath)) {
    throw "Frontend path not found: $frontendPath"
}

$backendCommand = "Set-Location '$backendPath'; dotnet run"
$frontendCommand = "Set-Location '$frontendPath'; npm run start"

if ($OpenFrontend) {
    $frontendCommand = "Set-Location '$frontendPath'; npm run start -- --open"
}

Write-Host "Starting backend in a new PowerShell window..." -ForegroundColor Cyan
Start-Process powershell.exe -ArgumentList @(
    '-NoExit',
    '-ExecutionPolicy', 'Bypass',
    '-Command', $backendCommand
)

Write-Host "Starting frontend in a new PowerShell window..." -ForegroundColor Cyan
Start-Process powershell.exe -ArgumentList @(
    '-NoExit',
    '-ExecutionPolicy', 'Bypass',
    '-Command', $frontendCommand
)

Write-Host "Done. Backend and frontend launch commands were started." -ForegroundColor Green
