# Supervised Autonomous Loop Preflight (GA)
#
# Deterministic gate that runs BEFORE the supervised-loop skill enters its
# cycle on the Guitar Alchemist repo. Exits 0 if the repo is safe to loop,
# 1 otherwise. Always prints a final line of the form
# `LOOP_READY=true|false reason=<code>` followed by a one-line reason.
#
# Mirror the hard refusals in .claude/skills/supervised-loop/SKILL.md.
#
# NOTE: -SkipVerify defaults to $true on GA because the canonical verify
# oracle is `dotnet build AllProjects.slnx -c Debug && dotnet test
# AllProjects.slnx`, which is too heavy for a preflight gate. The supervised
# loop's own Step 5 still requires running the oracle command before
# committing. Pass `-RunVerify` to run a build inside the preflight.

[CmdletBinding()]
param(
    [int]$OverseerMaxAgeHours = 24,
    [string]$OverseerPath = "state/governance/dev-process-overseer.json",
    [string]$LoopPolicyPath = "ga.loop-policy.json",
    [string]$RiskPolicyPath = "agent-blackbox.policy.json",
    [string]$StopMarkerPath = ".STOP",
    [string]$HaltAllPath = "$HOME/.demerzel/HALT-ALL",
    [switch]$RunVerify
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

function Emit-Result {
    param(
        [Parameter(Mandatory = $true)][bool]$Ready,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    $verdict = if ($Ready) { 'true' } else { 'false' }
    # Final line is the machine-readable verdict. Anything above is human-only.
    Write-Host "LOOP_READY=$verdict reason=$Reason"
    if ($Ready) { exit 0 } else { exit 1 }
}

Write-Host "[preflight] supervised-loop preflight starting (GA)"
Write-Host "[preflight] root: $root"

# 1. Loop policy parses and exposes the required arrays.
$loopPolicyFull = Join-Path $root $LoopPolicyPath
if (-not (Test-Path -LiteralPath $loopPolicyFull)) {
    Emit-Result -Ready:$false -Reason "loop_policy_missing"
}

try {
    $loopPolicy = Get-Content -LiteralPath $loopPolicyFull -Raw | ConvertFrom-Json
} catch {
    Emit-Result -Ready:$false -Reason "loop_policy_invalid_json"
}

if (-not $loopPolicy.allow_edit -or $loopPolicy.allow_edit.Count -eq 0) {
    Emit-Result -Ready:$false -Reason "loop_policy_missing_allow_edit"
}
if (-not $loopPolicy.protected_paths -or $loopPolicy.protected_paths.Count -eq 0) {
    Emit-Result -Ready:$false -Reason "loop_policy_missing_protected_paths"
}
Write-Host "[preflight] loop policy ok: $($loopPolicy.allow_edit.Count) allow_edit, $($loopPolicy.protected_paths.Count) protected"

# 2. Risk policy still exists and parses (load-bearing for the oracle).
$riskPolicyFull = Join-Path $root $RiskPolicyPath
if (-not (Test-Path -LiteralPath $riskPolicyFull)) {
    Emit-Result -Ready:$false -Reason "risk_policy_missing"
}
try {
    Get-Content -LiteralPath $riskPolicyFull -Raw | ConvertFrom-Json | Out-Null
} catch {
    Emit-Result -Ready:$false -Reason "risk_policy_invalid_json"
}

# 3. .STOP marker
$stopFull = Join-Path $root $StopMarkerPath
if (Test-Path -LiteralPath $stopFull) {
    Emit-Result -Ready:$false -Reason "stop_marker_present"
}

# 4. HALT-ALL cross-repo marker
if (Test-Path -LiteralPath $HaltAllPath) {
    try {
        $haltContent = Get-Content -LiteralPath $HaltAllPath -Raw
        if ($haltContent -match '^\s*$') {
            # empty file is enough to halt
            Emit-Result -Ready:$false -Reason "halt_all_present"
        }
        # If it's JSON with active:false / expired, allow. Otherwise treat as active.
        try {
            $halt = $haltContent | ConvertFrom-Json
            $expired = $false
            if ($halt.expiresAt) {
                try { $expired = ([datetimeoffset]::Parse($halt.expiresAt) -lt [datetimeoffset]::UtcNow) } catch { $expired = $false }
            }
            $active = $true
            if ($null -ne $halt.active) { $active = [bool]$halt.active }
            if ($active -and -not $expired) {
                Emit-Result -Ready:$false -Reason "halt_all_active"
            }
        } catch {
            # not JSON, treat as active
            Emit-Result -Ready:$false -Reason "halt_all_active_unparsed"
        }
    } catch {
        Emit-Result -Ready:$false -Reason "halt_all_unreadable"
    }
}

# 5. Overseer file fresh + loop-eligible
$overseerFull = Join-Path $root $OverseerPath
if (-not (Test-Path -LiteralPath $overseerFull)) {
    Emit-Result -Ready:$false -Reason "overseer_missing"
}

try {
    $overseer = Get-Content -LiteralPath $overseerFull -Raw | ConvertFrom-Json
} catch {
    Emit-Result -Ready:$false -Reason "overseer_invalid_json"
}

if (-not $overseer.emittedAt) {
    Emit-Result -Ready:$false -Reason "overseer_missing_emittedAt"
}

try {
    $emitted = [datetimeoffset]::Parse($overseer.emittedAt)
} catch {
    Emit-Result -Ready:$false -Reason "overseer_unparseable_emittedAt"
}

$ageHours = ([datetimeoffset]::UtcNow - $emitted).TotalHours
if ($ageHours -gt $OverseerMaxAgeHours) {
    Emit-Result -Ready:$false -Reason "overseer_stale_${ageHours}h"
}

if ($overseer.workflowMode -ne 'loop-eligible') {
    Emit-Result -Ready:$false -Reason "overseer_mode_$($overseer.workflowMode)"
}

if ($overseer.haltAll -and $overseer.haltAll.active -eq $true) {
    Emit-Result -Ready:$false -Reason "overseer_halt_all_active"
}

Write-Host "[preflight] overseer ok: mode=$($overseer.workflowMode), age=$([math]::Round($ageHours, 2))h"

# 6. Clean worktree (intersected against allow_edit + protected_paths)
$strictMode = $false
if ($null -eq $loopPolicy -or -not $loopPolicy.allow_edit -or -not $loopPolicy.protected_paths) {
    $strictMode = $true
}

function Convert-GitGlobToRegex {
    param([string]$Pattern)

    $normalized = ($Pattern -replace '\\', '/').Trim()
    if ($normalized.EndsWith('/')) {
        $directory = $normalized.TrimEnd('/')
        $escapedDirectory = [regex]::Escape($directory)
        return "^$escapedDirectory(?:/.*)?$"
    }

    $escaped = [regex]::Escape($normalized)
    $escaped = $escaped -replace '\\\*\\\*', '.*'
    $escaped = $escaped -replace '\\\*', '[^/]*'
    $escaped = $escaped -replace '\\\?', '[^/]'
    return "^$escaped$"
}

function Test-PathMatchesAnyPattern {
    param(
        [string]$Path,
        [object[]]$Patterns
    )

    $normalized = ($Path -replace '\\', '/').Trim()
    foreach ($pattern in @($Patterns)) {
        if (-not $pattern) { continue }
        $regex = Convert-GitGlobToRegex -Pattern ([string]$pattern)
        if ($normalized -match $regex) { return $true }
    }
    return $false
}

try {
    # --ignore-submodules=all: submodule directories (governance/demerzel,
    # mcp-servers/**) are in protected_paths precisely because the loop must
    # never touch them. Their *internal* dirty state is operator-managed
    # cross-repo work, not a GA loop concern, so we hide submodule lines from
    # the worktree-clean gate. If the loop ever modifies a submodule pointer
    # in the parent index that will still surface as a tracked-file change
    # outside the submodule directory.
    $porcelain = & git status --porcelain --ignore-submodules=all 2>&1
} catch {
    Emit-Result -Ready:$false -Reason "git_status_failed"
}
if ($LASTEXITCODE -ne 0) {
    Emit-Result -Ready:$false -Reason "git_status_exit_$LASTEXITCODE"
}

if ($porcelain) {
    # Parse porcelain into path entries (drop the two-char status prefix + space).
    $dirtyPaths = @()
    foreach ($line in @($porcelain)) {
        if (-not $line -or $line.Length -lt 4) { continue }
        $path = $line.Substring(3)
        if ($path -match ' -> ') {
            $path = ($path -split ' -> ')[-1]
        }
        $dirtyPaths += ($path -replace '\\', '/')
    }
    $totalDirty = $dirtyPaths.Count

    if ($strictMode) {
        Emit-Result -Ready:$false -Reason "policy_missing_strict_mode_dirty_${totalDirty}_files"
    }

    $protectedDirty = @()
    $allowEditDirty = @()
    $ignoredDirty = @()
    foreach ($p in $dirtyPaths) {
        $inProtected = Test-PathMatchesAnyPattern -Path $p -Patterns $loopPolicy.protected_paths
        $inAllowEdit = Test-PathMatchesAnyPattern -Path $p -Patterns $loopPolicy.allow_edit
        if ($inProtected) {
            $protectedDirty += $p
        } elseif ($inAllowEdit) {
            $allowEditDirty += $p
        } else {
            $ignoredDirty += $p
        }
    }

    if ($protectedDirty.Count -gt 0) {
        Write-Host "[preflight] dirty protected paths:" -ForegroundColor Yellow
        foreach ($p in $protectedDirty) { Write-Host "  $p" }
        Emit-Result -Ready:$false -Reason "worktree_dirty_protected_$($protectedDirty.Count)_files"
    }
    if ($allowEditDirty.Count -gt 0) {
        Write-Host "[preflight] dirty allow_edit paths:" -ForegroundColor Yellow
        foreach ($p in $allowEditDirty) { Write-Host "  $p" }
        Emit-Result -Ready:$false -Reason "worktree_dirty_allow_edit_$($allowEditDirty.Count)_files"
    }
    if ($ignoredDirty.Count -gt 0) {
        # Soft warning: dirty files outside both allow_edit and protected_paths.
        # These will not affect the cycle's edits or protected surfaces.
        [Console]::Error.WriteLine("[preflight] WARNING: $($ignoredDirty.Count) dirty file(s) outside allow_edit and protected_paths -- allowing cycle to proceed:")
        foreach ($p in $ignoredDirty) {
            [Console]::Error.WriteLine("  $p")
        }
        Emit-Result -Ready:$true -Reason "all_gates_pass_with_$($ignoredDirty.Count)_ignored_dirty"
    }
}

# 7. Optional verify oracle.
# On GA, verify = `dotnet build AllProjects.slnx -c Debug && dotnet test
# AllProjects.slnx`, which is too heavy to run on every preflight invocation.
# Default is SkipVerify; pass -RunVerify to opt in (e.g. CI cron).
if ($RunVerify) {
    Write-Host "[preflight] running oracle: dotnet build AllProjects.slnx -c Debug"
    & dotnet build AllProjects.slnx -c Debug *> $null
    if ($LASTEXITCODE -ne 0) {
        Emit-Result -Ready:$false -Reason "verify_build_exit_$LASTEXITCODE"
    }
    Write-Host "[preflight] oracle ok (build only; tests intentionally skipped in preflight)"
}

Emit-Result -Ready:$true -Reason "all_gates_pass"
