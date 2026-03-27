#!/usr/bin/env pwsh
# Scripts/ga-status.ps1
# Show blue/green slot status dashboard

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\lib\SlotState.ps1"

$state = Get-SlotState

Write-Host "`n=== Blue/Green Status ===" -ForegroundColor Cyan

if (-not $state) {
    Write-Host "  Not initialized. Run ga-bootstrap.ps1" -ForegroundColor Yellow
    exit 0
}

# Slot info
$active = $state.activeSlot
$target = $state.buildTarget

Write-Host ""
foreach ($slot in @("blue", "green")) {
    $info = $state.slots.$slot
    $isActive = $slot -eq $active
    $marker = if ($isActive) { " [ACTIVE]" } else { " [build target]" }
    $color = if ($isActive) { "Green" } else { "Gray" }

    Write-Host "  $($slot.ToUpper())$marker" -ForegroundColor $color

    if ($info.builtAt) {
        $age = [math]::Round(((Get-Date) - [DateTime]::Parse($info.builtAt)).TotalHours, 1)
        Write-Host "    Built:  $($info.builtAt) (${age}h ago)" -ForegroundColor Gray
    } else {
        Write-Host "    Built:  never" -ForegroundColor DarkGray
    }

    Write-Host "    Commit: $(if ($info.commitHash) { $info.commitHash } else { 'unknown' })" -ForegroundColor Gray
    Write-Host "    Health: $(if ($null -ne $info.healthy) { $info.healthy } else { 'untested' })" -ForegroundColor Gray

    # Check DLL count
    $slotBin = Join-Path (Get-SlotBinPath -Slot $slot) "net10.0"
    if (Test-Path $slotBin) {
        $dlls = @(Get-ChildItem $slotBin -Filter "*.dll" -ErrorAction SilentlyContinue).Count
        Write-Host "    DLLs:   $dlls files" -ForegroundColor Gray
    } else {
        Write-Host "    DLLs:   (empty)" -ForegroundColor DarkGray
    }
    Write-Host ""
}

# Junction status
$activePath = Get-ActiveBinPath
if (Test-Junction $activePath) {
    $junctionTarget = [string](Get-Item $activePath).Target
    Write-Host "  Junction: active -> $junctionTarget" -ForegroundColor Green
} else {
    Write-Host "  Junction: MISSING" -ForegroundColor Red
}

# Server status
$proc = Get-GaApiProcess
if ($proc) {
    Write-Host "  Server:   Running (PID $($proc.Id))" -ForegroundColor Green
} else {
    Write-Host "  Server:   Not running" -ForegroundColor Yellow
}

# Last swap
if ($state.lastSwap) {
    $swapAge = [math]::Round(((Get-Date) - [DateTime]::Parse($state.lastSwap)).TotalMinutes, 0)
    Write-Host "  Last swap: $($state.lastSwap) (${swapAge}m ago)" -ForegroundColor Gray
}

Write-Host ""
