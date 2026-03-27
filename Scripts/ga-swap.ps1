#!/usr/bin/env pwsh
# Scripts/ga-swap.ps1
# Move the active junction to the other slot, restart server

param(
    [switch]$Force,  # Swap even if health check fails
    [int]$HealthTimeout = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\lib\SlotState.ps1"

$state = Get-SlotState
if (-not $state) {
    Write-Host "ERROR: No .slot-state.json found. Run ga-bootstrap.ps1 first." -ForegroundColor Red
    exit 1
}

$oldActive = Get-ActiveSlot
$newActive = Get-InactiveSlot

Write-Host "`n=== Blue/Green Swap ===" -ForegroundColor Cyan
Write-Host "  Swapping: $oldActive -> $newActive"

# Step 1: Stop server
Write-Host "`n[1/4] Stopping server..." -ForegroundColor White
Stop-GaApiServer

# Step 2: Move junction
Write-Host "[2/4] Moving junction..." -ForegroundColor White
$activePath = Get-ActiveBinPath
$newTarget = Get-SlotBinPath -Slot $newActive

Remove-Junction -Path $activePath | Out-Null
$created = New-Junction -Link $activePath -Target $newTarget

if (-not $created) {
    Write-Host "  FAILED to create junction. Rolling back..." -ForegroundColor Red
    $oldTarget = Get-SlotBinPath -Slot $oldActive
    New-Junction -Link $activePath -Target $oldTarget | Out-Null
    Write-Host "  Rolled back to $oldActive" -ForegroundColor Yellow
    exit 1
}
Write-Host "  active -> $newActive" -ForegroundColor Green

# Step 3: Update state
$state.activeSlot = $newActive
$state.buildTarget = $oldActive
$state.lastSwap = (Get-Date).ToUniversalTime().ToString("o")
Set-SlotState -State $state
Set-SlotTarget -Slot $oldActive  # Next build targets the now-inactive old slot

# Step 4: Start server and verify
Write-Host "[3/4] Starting server from $newActive slot..." -ForegroundColor White
$slotDll = Join-Path (Get-SlotBinPath -Slot $newActive) "net10.0\GaApi.dll"
Start-Process -FilePath "dotnet" -ArgumentList $slotDll -WindowStyle Hidden

Write-Host "[4/4] Health check..." -ForegroundColor White
$healthy = Test-ServerHealth -TimeoutSeconds $HealthTimeout

if ($healthy) {
    Write-Host "`n=== Swap Complete ===" -ForegroundColor Green
    Write-Host "  Active: $newActive | Build target: $oldActive"
} elseif (-not $Force) {
    Write-Host "`n[Rollback] Health check failed. Rolling back to $oldActive..." -ForegroundColor Red
    & "$PSScriptRoot\ga-rollback.ps1"
} else {
    Write-Host "`n[Warning] Health check failed but -Force was specified." -ForegroundColor Yellow
    Write-Host "  Active: $newActive (UNHEALTHY)" -ForegroundColor Red
}
