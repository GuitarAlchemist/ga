# Blue/Green Binary Swap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a blue/green binary slot system using NTFS junctions so that `dotnet build` never overwrites trusted SAC binaries, with automatic swap and rollback.

**Architecture:** Two physical build output directories (`bin/blue/`, `bin/green/`) per opted-in project. An NTFS junction `bin/active` points to the live slot. MSBuild reads a `.slot-target` text file to direct output to the inactive slot. PowerShell scripts manage the lifecycle. Build events emit algedonic signals and update belief states in the governance graph.

**Tech Stack:** MSBuild (Directory.Build.props), PowerShell 7, NTFS junctions, .NET 10

**Deferred to Phase 2:** IXQL queries for build slot visualization, governance graph node registration (`build-slot-blue`, `build-slot-green`, `build-junction`), SignalR live broadcast of slot events

**Spec:** `docs/superpowers/specs/2026-03-27-blue-green-build-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Create | `.slot-target` | Plain text: build target slot name ("blue" or "green") |
| Create | `.slot-state.json` | Full metadata: active slot, timestamps, health, commits |
| Modify | `Directory.Build.props:19` | Add blue/green OutputPath PropertyGroup |
| Modify | `Apps/ga-server/GaApi/GaApi.csproj:14` | Add `<UseBlueGreenSlots>true</UseBlueGreenSlots>` |
| Modify | `.gitignore:163` | Add .slot-target, .slot-state.json patterns |
| Modify | `Apps/ga-server/GaApi/Program.cs:43` | IHealthCheckService DI registration (already done) |
| Create | `Scripts/ga-bootstrap.ps1` | First-time setup: create dirs, junction, state files |
| Create | `Scripts/ga-build.ps1` | Build to inactive slot, health check, auto-swap |
| Create | `Scripts/ga-start.ps1` | Start server from active slot |
| Create | `Scripts/ga-swap.ps1` | Move junction, restart, rollback on failure |
| Create | `Scripts/ga-rollback.ps1` | Emergency swap to other slot |
| Create | `Scripts/ga-status.ps1` | Show slot health, active/inactive, timestamps |
| Create | `Scripts/lib/SlotState.ps1` | Shared functions: read/write state, atomic file ops |

---

### Task 1: Shared State Library

**Files:**
- Create: `Scripts/lib/SlotState.ps1`

- [ ] **Step 1: Create the shared library with slot state functions**

```powershell
# Scripts/lib/SlotState.ps1
# Shared functions for blue/green slot management

$Script:SolutionRoot = (Resolve-Path "$PSScriptRoot\..").Path
$Script:SlotTargetPath = Join-Path $SolutionRoot ".slot-target"
$Script:SlotStatePath = Join-Path $SolutionRoot ".slot-state.json"

function Get-SlotTarget {
    if (Test-Path $Script:SlotTargetPath) {
        return (Get-Content $Script:SlotTargetPath -Raw).Trim()
    }
    return $null
}

function Set-SlotTarget {
    param([ValidateSet("blue","green")][string]$Slot)
    $temp = "$Script:SlotTargetPath.tmp"
    Set-Content -Path $temp -Value $Slot -NoNewline
    Move-Item -Path $temp -Destination $Script:SlotTargetPath -Force
}

function Get-SlotState {
    if (Test-Path $Script:SlotStatePath) {
        return Get-Content $Script:SlotStatePath -Raw | ConvertFrom-Json
    }
    return $null
}

function Set-SlotState {
    param([PSCustomObject]$State)
    $temp = "$Script:SlotStatePath.tmp"
    $State | ConvertTo-Json -Depth 5 | Set-Content -Path $temp
    Move-Item -Path $temp -Destination $Script:SlotStatePath -Force
}

function Get-ActiveSlot {
    $state = Get-SlotState
    if ($state) { return $state.activeSlot }
    return "blue"
}

function Get-InactiveSlot {
    $active = Get-ActiveSlot
    return $(if ($active -eq "blue") { "green" } else { "blue" })
}

function Get-GaApiBinPath {
    return Join-Path $Script:SolutionRoot "Apps\ga-server\GaApi\bin"
}

function Get-SlotBinPath {
    param([ValidateSet("blue","green")][string]$Slot)
    return Join-Path (Get-GaApiBinPath) $Slot
}

function Get-ActiveBinPath {
    return Join-Path (Get-GaApiBinPath) "active"
}

function Test-Junction {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    $item = Get-Item $Path -Force
    return ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
}

function New-Junction {
    param([string]$Link, [string]$Target)
    cmd /c "mklink /J `"$Link`" `"$Target`"" | Out-Null
    return $LASTEXITCODE -eq 0
}

function Remove-Junction {
    param([string]$Path)
    if (Test-Junction $Path) {
        cmd /c rmdir "`"$Path`"" | Out-Null
        return $LASTEXITCODE -eq 0
    }
    return $true
}

function Test-ServerHealth {
    param([int]$TimeoutSeconds = 30, [int]$Port = 5232)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:$Port/api/health/ping" -TimeoutSec 3 -ErrorAction Stop
            return $true
        } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Get-GaApiProcess {
    Get-Process -Name "GaApi" -ErrorAction SilentlyContinue
}

function Stop-GaApiServer {
    param([int]$GracePeriod = 10)
    $proc = Get-GaApiProcess
    if ($proc) {
        $proc | Stop-Process -Force:$false
        if (-not $proc.WaitForExit($GracePeriod * 1000)) {
            $proc | Stop-Process -Force
        }
        Write-Host "  Server stopped (PID $($proc.Id))" -ForegroundColor Yellow
    }
}

function New-SlotState {
    return [PSCustomObject]@{
        activeSlot      = "blue"
        lastSwap        = $null
        lastBuild       = $null
        buildTarget     = "green"
        trustedBaseline = $null
        slots           = [PSCustomObject]@{
            blue  = [PSCustomObject]@{ builtAt = $null; healthy = $null; commitHash = $null }
            green = [PSCustomObject]@{ builtAt = $null; healthy = $null; commitHash = $null }
        }
    }
}
```

- [ ] **Step 2: Verify the library loads without errors**

Run: `powershell -Command ". Scripts/lib/SlotState.ps1; Write-Host 'Loaded OK'; Get-ActiveSlot"` from the solution root.
Expected: "Loaded OK" and either "blue" (if .slot-state.json exists) or "blue" (default).

- [ ] **Step 3: Commit**

```bash
git add Scripts/lib/SlotState.ps1
git commit -m "feat: add shared PowerShell library for blue/green slot state management"
```

---

### Task 2: MSBuild + Project Configuration

**Files:**
- Modify: `Directory.Build.props` (insert after line 19)
- Modify: `Apps/ga-server/GaApi/GaApi.csproj` (insert before line 15)
- Modify: `.gitignore` (insert after line 162)

- [ ] **Step 1: Add blue/green PropertyGroup to Directory.Build.props**

Insert after the closing `</PropertyGroup>` at line 19, before the package ItemGroup:

```xml

  <!-- Blue/Green build slot support (opt-in per project via UseBlueGreenSlots) -->
  <PropertyGroup Condition="'$(UseBlueGreenSlots)' == 'true' AND Exists('$(MSBuildThisFileDirectory).slot-target')">
    <_SlotTarget>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory).slot-target').Trim())</_SlotTarget>
    <OutputPath>bin\$(_SlotTarget)\</OutputPath>
  </PropertyGroup>
```

- [ ] **Step 2: Add UseBlueGreenSlots to GaApi.csproj**

Insert before the closing `</PropertyGroup>` at line 15:

```xml
    <UseBlueGreenSlots>true</UseBlueGreenSlots>
```

- [ ] **Step 3: Add slot files to .gitignore**

Insert after the debug/scratch section (around line 163):

```
# Blue/Green deployment state files
.slot-target
.slot-state.json
.slot-lock
```

- [ ] **Step 4: Verify MSBuild respects the override**

Create a temporary `.slot-target` file and run a dry build:

```powershell
[System.IO.File]::WriteAllText("$PWD\.slot-target", "green")
dotnet build Apps/ga-server/GaApi/GaApi.csproj --configuration Debug -v:minimal 2>&1 | Select-String -Pattern "OutputPath|GaApi ->"
Remove-Item .slot-target
```

Expected: Output path should reference `bin\green\` instead of `bin\Debug\`.

- [ ] **Step 5: Commit**

```bash
git add Directory.Build.props Apps/ga-server/GaApi/GaApi.csproj .gitignore
git commit -m "feat: MSBuild blue/green OutputPath override with project opt-in"
```

---

### Task 3: Bootstrap Script

**Files:**
- Create: `Scripts/ga-bootstrap.ps1`

- [ ] **Step 1: Create the bootstrap script**

```powershell
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
```

- [ ] **Step 2: Run bootstrap and verify**

Run: `powershell -ExecutionPolicy Bypass -File Scripts/ga-bootstrap.ps1` from the solution root.
Expected: 6 steps complete, junction created, state files written.

Verify: `dir Apps\ga-server\GaApi\bin\active\net10.0\GaApi.dll` should resolve through junction.

- [ ] **Step 3: Commit**

```bash
git add Scripts/ga-bootstrap.ps1
git commit -m "feat: add blue/green bootstrap script with junction setup and baseline tagging"
```

---

### Task 4: Build Script

**Files:**
- Create: `Scripts/ga-build.ps1`

- [ ] **Step 1: Create the build script**

```powershell
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
```

- [ ] **Step 2: Commit**

```bash
git add Scripts/ga-build.ps1
git commit -m "feat: add blue/green build script with health check and auto-swap"
```

---

### Task 5: Swap and Rollback Scripts

**Files:**
- Create: `Scripts/ga-swap.ps1`
- Create: `Scripts/ga-rollback.ps1`

- [ ] **Step 1: Create the swap script**

```powershell
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
```

- [ ] **Step 2: Create the rollback script**

```powershell
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
```

- [ ] **Step 3: Commit**

```bash
git add Scripts/ga-swap.ps1 Scripts/ga-rollback.ps1
git commit -m "feat: add blue/green swap and rollback scripts with automatic health-check failover"
```

---

### Task 6: Start and Status Scripts

**Files:**
- Create: `Scripts/ga-start.ps1`
- Create: `Scripts/ga-status.ps1`

- [ ] **Step 1: Create the start script**

```powershell
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
$dll = Join-Path $activePath "net10.0\GaApi.dll"

if (-not (Test-Path $dll)) {
    Write-Host "ERROR: GaApi.dll not found at $dll" -ForegroundColor Red
    Write-Host "  Run ga-build.ps1 to build, or ga-bootstrap.ps1 to set up." -ForegroundColor Yellow
    exit 1
}

Write-Host "Starting GaApi from $active slot..." -ForegroundColor Cyan
Start-Process -FilePath "dotnet" -ArgumentList $dll -WindowStyle Hidden

if (Test-ServerHealth -TimeoutSeconds $HealthTimeout) {
    $proc = Get-GaApiProcess
    Write-Host "Server running (PID $($proc.Id)) on http://localhost:5232" -ForegroundColor Green
} else {
    Write-Host "Server started but health check timed out after ${HealthTimeout}s" -ForegroundColor Yellow
    Write-Host "  Check logs or try: curl http://localhost:5232/api" -ForegroundColor Gray
}
```

- [ ] **Step 2: Create the status script**

```powershell
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
        $age = [math]::Round(((Get-Date) - [DateTimeOffset]::Parse($info.builtAt)).TotalHours, 1)
        Write-Host "    Built:  $($info.builtAt) (${age}h ago)" -ForegroundColor Gray
    } else {
        Write-Host "    Built:  never" -ForegroundColor DarkGray
    }

    Write-Host "    Commit: $($info.commitHash ?? 'unknown')" -ForegroundColor Gray
    Write-Host "    Health: $($info.healthy ?? 'untested')" -ForegroundColor Gray

    # Check DLL count
    $slotBin = Join-Path (Get-SlotBinPath -Slot $slot) "net10.0"
    if (Test-Path $slotBin) {
        $dlls = (Get-ChildItem $slotBin -Filter "*.dll" -ErrorAction SilentlyContinue).Count
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
    $swapAge = [math]::Round(((Get-Date) - [DateTimeOffset]::Parse($state.lastSwap)).TotalMinutes, 0)
    Write-Host "  Last swap: $($state.lastSwap) (${swapAge}m ago)" -ForegroundColor Gray
}

Write-Host ""
```

- [ ] **Step 3: Commit**

```bash
git add Scripts/ga-start.ps1 Scripts/ga-status.ps1
git commit -m "feat: add blue/green start and status scripts"
```

---

### Task 7: Governance Integration — Algedonic Signals + Belief States

**Files:**
- Create: `governance/demerzel/state/beliefs/slot-blue-health.belief.json`
- Create: `governance/demerzel/state/beliefs/slot-green-health.belief.json`
- Modify: `Scripts/lib/SlotState.ps1` (add signal emission functions)

- [ ] **Step 1: Create belief state files for build slots**

`governance/demerzel/state/beliefs/slot-blue-health.belief.json`:
```json
{
  "id": "belief-slot-blue-health",
  "proposition": "Blue build slot is safe to run",
  "truth_value": "U",
  "confidence": 0.0,
  "evidence": {
    "supporting": [],
    "contradicting": []
  },
  "last_updated": "2026-03-27T00:00:00Z",
  "evaluated_by": "ga-build-system"
}
```

`governance/demerzel/state/beliefs/slot-green-health.belief.json`:
```json
{
  "id": "belief-slot-green-health",
  "proposition": "Green build slot is safe to run",
  "truth_value": "U",
  "confidence": 0.0,
  "evidence": {
    "supporting": [],
    "contradicting": []
  },
  "last_updated": "2026-03-27T00:00:00Z",
  "evaluated_by": "ga-build-system"
}
```

- [ ] **Step 2: Add governance update functions to SlotState.ps1**

Append to `Scripts/lib/SlotState.ps1`:

```powershell

# --- Governance Integration ---

function Update-SlotBelief {
    param(
        [ValidateSet("blue","green")][string]$Slot,
        [ValidateSet("T","P","U","D","F","C")][string]$TruthValue,
        [double]$Confidence,
        [string]$EvidenceClaim,
        [string]$EvidenceSource = "ga-build-system"
    )

    $beliefDir = Join-Path $Script:SolutionRoot "governance\demerzel\state\beliefs"
    $beliefFile = Join-Path $beliefDir "slot-$Slot-health.belief.json"

    if (-not (Test-Path $beliefFile)) { return }

    $belief = Get-Content $beliefFile -Raw | ConvertFrom-Json
    $belief.truth_value = $TruthValue
    $belief.confidence = $Confidence
    $belief.last_updated = (Get-Date).ToUniversalTime().ToString("o")

    $evidence = [PSCustomObject]@{
        source    = $EvidenceSource
        claim     = $EvidenceClaim
        timestamp = $belief.last_updated
        reliability = $Confidence
    }

    if ($TruthValue -in @("T","P")) {
        if (-not $belief.evidence.supporting) { $belief.evidence.supporting = @() }
        $belief.evidence.supporting += $evidence
    } else {
        if (-not $belief.evidence.contradicting) { $belief.evidence.contradicting = @() }
        $belief.evidence.contradicting += $evidence
    }

    $belief | ConvertTo-Json -Depth 5 | Set-Content $beliefFile
}

function Emit-AlgedonicSignal {
    param(
        [ValidateSet("pain","pleasure")][string]$Type,
        [ValidateSet("info","warning","emergency","critical")][string]$Severity,
        [string]$Description,
        [string]$NodeId
    )

    $signalDir = Join-Path $Script:SolutionRoot "governance\demerzel\state\algedonic"
    if (-not (Test-Path $signalDir)) { New-Item -ItemType Directory -Path $signalDir -Force | Out-Null }

    $timestamp = (Get-Date).ToUniversalTime().ToString("o")
    $guid = [guid]::NewGuid().ToString().Substring(0, 8)
    $signal = [PSCustomObject]@{
        id          = "sig-build_slot_health-$((Get-Date).ToString('yyyyMMddHHmmss'))-$guid"
        timestamp   = $timestamp
        signal      = "build_slot_health"
        type        = $Type
        severity    = $Severity
        status      = "active"
        source      = "ga"
        description = $Description
        node_id     = $NodeId
    }

    $fileName = "sig-build_slot_health-$((Get-Date).ToString('yyyy-MM-dd'))-$guid.signal.json"
    $signal | ConvertTo-Json -Depth 3 | Set-Content (Join-Path $signalDir $fileName)

    $color = if ($Type -eq "pain") { "Red" } else { "Green" }
    Write-Host "  [Algedonic] $Type/$Severity - $Description" -ForegroundColor $color
}
```

- [ ] **Step 3: Wire governance calls into ga-build.ps1**

Add after the health check result in `ga-build.ps1` (after `Set-SlotState -State $state` in the health check section):

```powershell
# Update belief state
if ($state.slots.$inactive.healthy -eq $true) {
    Update-SlotBelief -Slot $inactive -TruthValue "P" -Confidence 0.7 -EvidenceClaim "Health check passed: /api/health/ping returned 200"
    Emit-AlgedonicSignal -Type "pleasure" -Severity "info" -Description "Build slot '$inactive' passed health check" -NodeId "build-slot-$inactive"
} elseif ($state.slots.$inactive.healthy -eq $false) {
    Update-SlotBelief -Slot $inactive -TruthValue "D" -Confidence 0.3 -EvidenceClaim "Health check failed: server did not respond"
    Emit-AlgedonicSignal -Type "pain" -Severity "warning" -Description "Build slot '$inactive' failed health check" -NodeId "build-slot-$inactive"
}
```

- [ ] **Step 4: Wire governance calls into ga-swap.ps1**

Add after successful swap in `ga-swap.ps1` (after "Swap Complete" message):

```powershell
Update-SlotBelief -Slot $newActive -TruthValue "T" -Confidence 0.9 -EvidenceClaim "Swap succeeded, server healthy"
Emit-AlgedonicSignal -Type "pleasure" -Severity "info" -Description "Swapped to '$newActive' slot successfully" -NodeId "build-slot-$newActive"
```

- [ ] **Step 5: Wire governance calls into ga-rollback.ps1**

Add after failed rollback in `ga-rollback.ps1` (after "BOTH SLOTS UNHEALTHY"):

```powershell
Emit-AlgedonicSignal -Type "pain" -Severity "emergency" -Description "Both build slots unhealthy - manual intervention required" -NodeId "build-junction"
```

- [ ] **Step 6: Commit**

```bash
git add governance/demerzel/state/beliefs/slot-blue-health.belief.json governance/demerzel/state/beliefs/slot-green-health.belief.json Scripts/lib/SlotState.ps1 Scripts/ga-build.ps1 Scripts/ga-swap.ps1 Scripts/ga-rollback.ps1
git commit -m "feat: governance integration - algedonic signals and belief states for build slots"
```

---

### Task 8: IHealthCheckService DI Fix + Verify

**Files:**
- Verify: `Apps/ga-server/GaApi/Program.cs:43` (already modified)

- [ ] **Step 1: Verify the DI registration is in place**

Read `Apps/ga-server/GaApi/Program.cs` around line 43 and confirm `builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();` is present.

- [ ] **Step 2: Build and verify health endpoint works**

```powershell
dotnet build Apps/ga-server/GaApi/GaApi.csproj --configuration Debug 2>&1 | Select-Object -Last 3
```

Expected: 0 Errors.

- [ ] **Step 3: Commit the DI fix if not already committed**

```bash
git add Apps/ga-server/GaApi/Program.cs
git commit -m "fix: register IHealthCheckService in DI container"
```

---

### Task 9: End-to-End Validation

- [ ] **Step 1: Run bootstrap**

```powershell
.\Scripts\ga-bootstrap.ps1
```

Expected: All 6 steps pass, junction created.

- [ ] **Step 2: Check status**

```powershell
.\Scripts\ga-status.ps1
```

Expected: Blue active, green as build target, junction pointing to blue.

- [ ] **Step 3: Run a build**

```powershell
.\Scripts\ga-build.ps1 -SkipHealthCheck -NoSwap
```

Expected: Build succeeds, output goes to green slot.

- [ ] **Step 4: Verify green slot has DLLs**

```powershell
(Get-ChildItem Apps\ga-server\GaApi\bin\green\net10.0\*.dll).Count
```

Expected: ~235 DLLs.

- [ ] **Step 5: Verify blue slot is unchanged**

```powershell
(Get-ChildItem Apps\ga-server\GaApi\bin\blue\net10.0\*.dll).Count
```

Expected: Same count as before build (seeded from bootstrap).

- [ ] **Step 6: Run status again**

```powershell
.\Scripts\ga-status.ps1
```

Expected: Green shows recent build timestamp, blue unchanged.

- [ ] **Step 7: Commit any adjustments**

```bash
git add -A
git commit -m "feat: blue/green build system - end-to-end validated"
```
