#!/usr/bin/env pwsh
# Scripts/ga-build.ps1
# Build to inactive slot, health check, auto-swap

param(
    [switch]$NoSwap,       # Build only, don't auto-swap
    [switch]$SkipHealthCheck,
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

$inactive = Get-InactiveSlot
$active = Get-ActiveSlot

Write-Host "`n=== Blue/Green Build ===" -ForegroundColor Cyan
Write-Host "  Active slot:  $active (running)"
Write-Host "  Build target: $inactive"

# Step 1: Ensure .slot-target points to inactive
Set-SlotTarget -Slot $inactive

# Step 2: Build
Write-Host "`n[Build] dotnet build -> $inactive slot..." -ForegroundColor White
$buildStart = Get-Date
$buildResult = dotnet build "$Script:SolutionRoot\Apps\ga-server\GaApi\GaApi.csproj" --configuration Debug 2>&1
$buildExitCode = $LASTEXITCODE

if ($buildExitCode -ne 0) {
    Write-Host "`n[Build] FAILED (exit code $buildExitCode)" -ForegroundColor Red
    Write-Host "  Active slot '$active' is unaffected." -ForegroundColor Yellow
    $buildResult | Where-Object { $_ -match "error " } | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "[Build] SUCCESS ($(((Get-Date) - $buildStart).TotalSeconds.ToString('F1'))s)" -ForegroundColor Green

# Step 3: Update state
$commitHash = try { git -C $Script:SolutionRoot rev-parse --short HEAD 2>$null } catch { "unknown" }
$state.lastBuild = (Get-Date).ToUniversalTime().ToString("o")
$state.slots.$inactive.builtAt = $state.lastBuild
$state.slots.$inactive.commitHash = $commitHash
$state.slots.$inactive.healthy = $null
Set-SlotState -State $state

# Step 4: Health check
if (-not $SkipHealthCheck) {
    Write-Host "`n[Health] Starting server from $inactive slot for health check..." -ForegroundColor White

    $slotBin = Join-Path (Get-SlotBinPath -Slot $inactive) "net10.0\GaApi.dll"
    if (-not (Test-Path $slotBin)) {
        Write-Host "[Health] WARNING: $slotBin not found. Skipping health check." -ForegroundColor Yellow
        $state.slots.$inactive.healthy = $false
        Set-SlotState -State $state
    } else {
        # Start temp server on a different port to avoid conflict with running active server
        $healthCheckPort = 5299
        $tempProc = Start-Process -FilePath "dotnet" -ArgumentList "$slotBin","--urls","http://localhost:$healthCheckPort" -PassThru -WindowStyle Hidden
        try {
            $healthy = Test-ServerHealth -TimeoutSeconds $HealthTimeout -Port $healthCheckPort
            $state.slots.$inactive.healthy = $healthy

            if ($healthy) {
                Write-Host "[Health] PASS - $inactive slot is healthy" -ForegroundColor Green
            } else {
                Write-Host "[Health] FAIL - $inactive slot did not respond within ${HealthTimeout}s" -ForegroundColor Red
            }
        } finally {
            # Always kill health check process to prevent file locks
            if (-not $tempProc.HasExited) {
                $tempProc | Stop-Process -Force
            }
        }
        Set-SlotState -State $state

        # Update belief state
        if ($state.slots.$inactive.healthy -eq $true) {
            Update-SlotBelief -Slot $inactive -TruthValue "P" -Confidence 0.7 -EvidenceClaim "Health check passed: /api/health/ping returned 200"
            Emit-AlgedonicSignal -Type "pleasure" -Severity "info" -Description "Build slot '$inactive' passed health check" -NodeId "build-slot-$inactive"
        } elseif ($state.slots.$inactive.healthy -eq $false) {
            Update-SlotBelief -Slot $inactive -TruthValue "D" -Confidence 0.3 -EvidenceClaim "Health check failed: server did not respond"
            Emit-AlgedonicSignal -Type "pain" -Severity "warning" -Description "Build slot '$inactive' failed health check" -NodeId "build-slot-$inactive"
        }
    }
} else {
    Write-Host "`n[Health] Skipped" -ForegroundColor Yellow
}

# Step 5: Auto-swap
if (-not $NoSwap -and $state.slots.$inactive.healthy -eq $true) {
    Write-Host "`n[Swap] Auto-swapping to $inactive..." -ForegroundColor Cyan
    & "$PSScriptRoot\ga-swap.ps1"
} elseif (-not $NoSwap -and $state.slots.$inactive.healthy -ne $true) {
    Write-Host "`n[Swap] Skipped (health check did not pass)" -ForegroundColor Yellow
    Write-Host "  Run 'ga-swap.ps1' manually to force swap." -ForegroundColor Gray
}

Write-Host "`n=== Build Complete ===" -ForegroundColor Cyan
