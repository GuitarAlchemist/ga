#!/usr/bin/env pwsh
# Scripts/ga-bootstrap.ps1
# First-time setup for blue/green binary slot system

param(
    [switch]$Force  # Re-bootstrap even if already set up
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\lib\SlotState.ps1"

Write-Host "`n=== Blue/Green Build System Bootstrap ===" -ForegroundColor Cyan

# Step 1: Test junction capability
Write-Host "`n[1/6] Testing NTFS junction support..." -ForegroundColor White
$testDir = Join-Path $Script:SolutionRoot ".junction-test-target"
$testLink = Join-Path $Script:SolutionRoot ".junction-test-link"
try {
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    New-Junction -Link $testLink -Target $testDir | Out-Null
    if (-not (Test-Junction $testLink)) { throw "Junction creation failed" }
    Remove-Junction $testLink
    Remove-Item $testDir -Force
    Write-Host "  Junction support OK" -ForegroundColor Green
} catch {
    Write-Host "  FAILED: Cannot create NTFS junctions. Enable Developer Mode in Windows Settings." -ForegroundColor Red
    exit 1
}

# Step 2: Create slot directories
Write-Host "[2/6] Creating slot directories..." -ForegroundColor White
$binPath = Get-GaApiBinPath
$bluePath = Get-SlotBinPath -Slot "blue"
$greenPath = Get-SlotBinPath -Slot "green"

foreach ($dir in @("$bluePath\net10.0", "$greenPath\net10.0")) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Gray
    } else {
        Write-Host "  Exists:  $dir" -ForegroundColor Gray
    }
}

# Step 3: Copy current trusted binaries to blue slot
Write-Host "[3/6] Seeding blue slot with current binaries..." -ForegroundColor White
$debugPath = Join-Path $binPath "Debug\net10.0"
$blueNet = "$bluePath\net10.0"

if (Test-Path $debugPath) {
    $count = (Get-ChildItem $debugPath -File).Count
    Copy-Item "$debugPath\*" $blueNet -Recurse -Force
    Write-Host "  Copied $count files from bin/Debug/net10.0/ to bin/blue/net10.0/" -ForegroundColor Green
} else {
    Write-Host "  WARNING: No existing binaries at $debugPath" -ForegroundColor Yellow
    Write-Host "  You'll need to build from Visual Studio first to seed trusted binaries." -ForegroundColor Yellow
}

# Step 4: Create junction
Write-Host "[4/6] Creating active junction..." -ForegroundColor White
$activePath = Get-ActiveBinPath
if (Test-Junction $activePath) {
    if (-not $Force) {
        Write-Host "  Junction already exists. Use -Force to recreate." -ForegroundColor Yellow
    } else {
        Remove-Junction $activePath
        New-Junction -Link $activePath -Target $bluePath
        Write-Host "  Recreated: active -> blue" -ForegroundColor Green
    }
} else {
    if (Test-Path $activePath) {
        Write-Host "  ERROR: $activePath exists but is not a junction. Remove it manually." -ForegroundColor Red
        exit 1
    }
    New-Junction -Link $activePath -Target $bluePath
    Write-Host "  Created: active -> blue" -ForegroundColor Green
}

# Step 5: Create state files
Write-Host "[5/6] Creating state files..." -ForegroundColor White
Set-SlotTarget -Slot "green"
Write-Host "  .slot-target = green (next build targets green)" -ForegroundColor Gray

$state = New-SlotState
if (Test-Path $debugPath) {
    $state.slots.blue.builtAt = (Get-Date).ToUniversalTime().ToString("o")
    $state.slots.blue.healthy = $true
    try { $state.slots.blue.commitHash = (git -C $Script:SolutionRoot rev-parse --short HEAD 2>$null) } catch { }
}
Set-SlotState -State $state
Write-Host "  .slot-state.json created" -ForegroundColor Gray

# Step 6: Tag baseline
Write-Host "[6/6] Tagging trusted baseline..." -ForegroundColor White
$tagName = "trusted-baseline-$(Get-Date -Format 'yyyy-MM-dd')"
try {
    git -C $Script:SolutionRoot tag $tagName HEAD 2>$null
    Write-Host "  Tagged: $tagName" -ForegroundColor Green
} catch {
    Write-Host "  Tag $tagName already exists (skipped)" -ForegroundColor Yellow
}

Write-Host "`n=== Bootstrap Complete ===" -ForegroundColor Cyan
Write-Host "  Active slot:  blue"
Write-Host "  Build target: green"
Write-Host "  Next step:    .\Scripts\ga-build.ps1"
Write-Host ""
