#!/usr/bin/env pwsh
# Scripts/ga-rollback.ps1
# Emergency swap to the other slot

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\lib\SlotState.ps1"

$state = Get-SlotState
if (-not $state) {
    Write-Host "ERROR: No .slot-state.json found." -ForegroundColor Red
    exit 1
}

$current = Get-ActiveSlot
$target = Get-InactiveSlot

Write-Host "`n=== Emergency Rollback ===" -ForegroundColor Red
Write-Host "  Rolling back: $current -> $target"

# Stop server
Stop-GaApiServer

# Move junction
$activePath = Get-ActiveBinPath
Remove-Junction -Path $activePath | Out-Null
$created = New-Junction -Link $activePath -Target (Get-SlotBinPath -Slot $target)
if (-not $created) {
    Write-Host "  CRITICAL: Failed to create junction during rollback!" -ForegroundColor Red
    exit 1
}

# Update state
$state.activeSlot = $target
$state.buildTarget = $current
$state.lastSwap = (Get-Date).ToUniversalTime().ToString("o")
Set-SlotState -State $state
Set-SlotTarget -Slot $current

# Start server
$slotDll = Join-Path (Get-SlotBinPath -Slot $target) "net10.0\GaApi.dll"
Start-Process -FilePath "dotnet" -ArgumentList $slotDll -WindowStyle Hidden

$healthy = Test-ServerHealth -TimeoutSeconds 30
if ($healthy) {
    Write-Host "`n=== Rollback Complete ===" -ForegroundColor Green
    Write-Host "  Active: $target"
} else {
    Write-Host "`n=== BOTH SLOTS UNHEALTHY ===" -ForegroundColor Red
    Write-Host "  Recovery options:"
    Write-Host "    1. Build from Visual Studio (SAC-whitelisted)"
    Write-Host "    2. Run via Docker: docker compose up -d gaapi"
    Write-Host "    3. Wait for SAC trust to build on current binaries"
}
