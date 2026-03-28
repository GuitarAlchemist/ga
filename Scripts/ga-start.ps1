#!/usr/bin/env pwsh
# Scripts/ga-start.ps1
# Start server from the active slot

param([int]$HealthTimeout = 30)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\lib\SlotState.ps1"

# Check if already running
$existing = Get-GaApiProcess
if ($existing) {
    Write-Host "GaApi is already running (PID $($existing.Id))" -ForegroundColor Yellow
    Write-Host "  Use Stop-Process -Name GaApi to stop it first." -ForegroundColor Gray
    exit 0
}

# Check for bootstrap
$activePath = Get-ActiveBinPath
if (-not (Test-Junction $activePath)) {
    Write-Host "ERROR: No active junction found at $activePath" -ForegroundColor Red
    Write-Host "  Run ga-bootstrap.ps1 first." -ForegroundColor Yellow
    exit 1
}

$active = Get-ActiveSlot
$slotDir = Join-Path $activePath "net10.0"
$dll = Join-Path $slotDir "GaApi.dll"

if (-not (Test-Path $dll)) {
    Write-Host "ERROR: GaApi.dll not found at $dll" -ForegroundColor Red
    Write-Host "  Run ga-build.ps1 to build, or ga-bootstrap.ps1 to set up." -ForegroundColor Yellow
    exit 1
}

# Ensure runtime config files exist (build redirect sometimes misses these)
$debugDir = Join-Path (Get-GaApiBinPath) "Debug\net10.0"
foreach ($f in @("GaApi.runtimeconfig.json", "GaApi.deps.json")) {
    $target = Join-Path $slotDir $f
    $source = Join-Path $debugDir $f
    if (-not (Test-Path $target) -and (Test-Path $source)) {
        Copy-Item $source $target
        Write-Host "  Copied missing $f from Debug" -ForegroundColor Gray
    }
}

Write-Host "Starting GaApi from $active slot..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList $dll,"--urls","http://localhost:5232" -WindowStyle Hidden

if (Test-ServerHealth -TimeoutSeconds $HealthTimeout) {
    $proc = Get-GaApiProcess
    Write-Host "Server running (PID $($proc.Id)) on http://localhost:5232" -ForegroundColor Green
} else {
    Write-Host "Server started but health check timed out after ${HealthTimeout}s" -ForegroundColor Yellow
    Write-Host "  Check logs or try: curl http://localhost:5232/api" -ForegroundColor Gray
}
