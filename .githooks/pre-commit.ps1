#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pre-commit hook for Guitar Alchemist

.DESCRIPTION
    Runs before each commit to ensure code quality:
    - Checks code formatting (dotnet format)
    - Runs quick build
    - Optionally runs quick tests
#>

Write-Host "`n🎸 Guitar Alchemist - Pre-commit Hook" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$ErrorActionPreference = "Continue"
$script:HasErrors = $false

# ============================================
# CHECK CODE FORMATTING (staged C# files only — advisory)
# ============================================
# Scope the check to STAGED .cs files (fast; only judges what the commit touches).
#
# PER PROJECT, not per solution: `dotnet format AllProjects.slnx` crashes opening
# the workspace (System.InvalidOperationException: Unexpected null -
# SolutionState.cs, a known dotnet-format/.slnx bug) and, in a fresh worktree,
# first triggers a whole-solution restore that has hung for 25+ minutes. So each
# staged file is mapped to its nearest owning .csproj and `dotnet format` runs
# once per affected project, with --no-restore when the project is already
# restored and a hard timeout after which the step warns and moves on.
#
# ADVISORY, not blocking: format results are WARNINGS only; CI runs the
# authoritative format check. The BUILD gate below stays strict and is what
# actually protects the branch.
Write-Host "▶ Checking code formatting (staged C# files, advisory)..." -ForegroundColor Blue

$stagedCs = git diff --cached --name-only --diff-filter=ACM 2>$null |
    Where-Object { $_ -match '\.cs$' -and (Test-Path $_) }

# Staged .cs files grouped by nearest owning .csproj (walk up directories).
# Shared by the format step and the build step below.
$stagedByProject = [ordered]@{}
foreach ($f in $stagedCs) {
    $dir = Split-Path -Parent $f
    while ($dir -and (Test-Path $dir)) {
        $proj = Get-ChildItem -Path $dir -Filter *.csproj -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($proj) {
            if (-not $stagedByProject.Contains($proj.FullName)) {
                $stagedByProject[$proj.FullName] = [System.Collections.Generic.List[string]]::new()
            }
            # Keep git's repo-relative path: `dotnet format --include` silently
            # matches nothing when given absolute paths (exit 0, nothing checked).
            $stagedByProject[$proj.FullName].Add($f)
            break
        }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
}

$formatTimeoutSec = 120
if (-not $stagedCs) {
    Write-Host "✓ No staged C# files — format check skipped" -ForegroundColor Green
} elseif ($stagedByProject.Count -eq 0) {
    Write-Host "✓ Staged C# files belong to no project — format check skipped" -ForegroundColor Green
} else {
    foreach ($proj in $stagedByProject.Keys) {
        $name     = Split-Path -Leaf $proj
        $files    = $stagedByProject[$proj]
        $restored = Test-Path (Join-Path (Split-Path -Parent $proj) 'obj' 'project.assets.json')

        $psi = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
        foreach ($a in @('format', $proj, '--verify-no-changes', '--verbosity', 'quiet')) { $psi.ArgumentList.Add($a) }
        if ($restored) { $psi.ArgumentList.Add('--no-restore') }
        $psi.ArgumentList.Add('--include')
        foreach ($file in $files) { $psi.ArgumentList.Add($file) }
        $psi.UseShellExecute        = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $true
        # No lingering MSBuild worker nodes that would outlive a timeout kill.
        $psi.Environment['MSBUILDDISABLENODEREUSE'] = '1'

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $p = [System.Diagnostics.Process]::Start($psi)
        $stdout = $p.StandardOutput.ReadToEndAsync()
        $stderr = $p.StandardError.ReadToEndAsync()
        if (-not $p.WaitForExit($formatTimeoutSec * 1000)) {
            try { $p.Kill($true) } catch { }
            Write-Host "⚠ Format check timed out after ${formatTimeoutSec}s in $name — skipped (advisory)" -ForegroundColor Yellow
            continue
        }
        $p.WaitForExit()
        $secs = [math]::Round($sw.Elapsed.TotalSeconds, 1)

        if ($p.ExitCode -eq 0) {
            Write-Host "✓ Staged files in $name are formatted (${secs}s)" -ForegroundColor Green
        } elseif ($p.ExitCode -eq 2) {
            Write-Host "⚠ Possible formatting drift in $name (${secs}s, advisory — not blocking):" -ForegroundColor Yellow
            Write-Host "  Run: dotnet format $(Resolve-Path -Relative -LiteralPath $proj) --include $($files -join ' ')" -ForegroundColor Yellow
        } else {
            Write-Host "⚠ dotnet format could not check $name (exit $($p.ExitCode), advisory — not blocking)" -ForegroundColor Yellow
            ("$($stdout.Result)`n$($stderr.Result)" -split "`r?`n") |
                Where-Object { $_.Trim() } | Select-Object -Last 5 |
                ForEach-Object { Write-Host "   $_" -ForegroundColor Yellow }
        }
    }
}

# ============================================
# BUILD CHECK (affected projects; dev-stack aware)
# ============================================
# Build only the projects that own staged .cs files, and classify the result so
# a running dev stack does not produce false failures: GaApi / GaMcpServer hold
# Common/GA.*.dll open at runtime, so the copy/link step fails with MSB3027/3021
# even when compilation is clean. We treat lock-only failures as a warning (CI
# runs the full build) but still BLOCK on real compiler errors (CS/FS) — those
# surface before the locked copy step, so genuine breaks are still caught.
Write-Host "`n▶ Building affected projects..." -ForegroundColor Blue

$affectedProjects = $stagedByProject

if ($affectedProjects.Count -eq 0) {
    Write-Host "✓ No buildable projects affected — build skipped" -ForegroundColor Green
} else {
    $hadRealError = $false
    $hadLockOnly  = $false
    foreach ($proj in $affectedProjects.Keys) {
        $name = Split-Path -Leaf $proj
        $buildOutput = dotnet build $proj --no-restore --nologo -v quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            $compileErrors = $buildOutput | Select-String -Pattern 'error (CS|FS)\d+'
            $lockErrors    = $buildOutput | Select-String -Pattern 'error (MSB3027|MSB3021|MSB4181)'
            if ($compileErrors) {
                Write-Host "✗ Build failed (compile errors) in ${name}:" -ForegroundColor Red
                $compileErrors | Select-Object -First 12 | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
                $hadRealError = $true
            } elseif ($lockErrors) {
                $hadLockOnly = $true
            } else {
                Write-Host "✗ Build failed in ${name}:" -ForegroundColor Red
                Write-Host $buildOutput
                $hadRealError = $true
            }
        }
    }
    if ($hadRealError) {
        $script:HasErrors = $true
    } elseif ($hadLockOnly) {
        Write-Host "⚠ Compile clean, but output DLLs are locked by a running dev stack —" -ForegroundColor Yellow
        Write-Host "  skipped the copy/link step (no compiler errors). CI runs the full build." -ForegroundColor Yellow
    } else {
        Write-Host "✓ Affected projects build clean" -ForegroundColor Green
    }
}

# ============================================
# ROP ENFORCEMENT CHECK
# ============================================
Write-Host "`n▶ Checking for naked throws in service files..." -ForegroundColor Blue

$stagedServiceFiles = git diff --cached --name-only 2>$null |
    Where-Object { $_ -match "Services/[^/]+\.cs$" -and (Test-Path $_) }

$ropViolations = @()
foreach ($file in $stagedServiceFiles) {
    $lines = Get-Content $file -ErrorAction SilentlyContinue
    $lineNum = 0
    $inCatch = $false
    $braceDepth = 0
    $catchDepth = 0
    foreach ($line in $lines) {
        $lineNum++
        # Track brace depth to detect when we leave a catch block
        $opens  = ([regex]::Matches($line, '\{')).Count
        $closes = ([regex]::Matches($line, '\}')).Count
        if ($inCatch) {
            $catchDepth += $opens - $closes
            if ($catchDepth -le 0) { $inCatch = $false }
        }
        if ($line -match '^\s*catch\b') { $inCatch = $true; $catchDepth = 0 }
        # Flag throw new outside catch blocks (skip comments and re-throws)
        if (-not $inCatch -and
            $line -match '^\s+throw\s+new\s+' -and
            $line -notmatch '^\s*//')
        {
            $ropViolations += "${file}:${lineNum}: $($line.Trim())"
        }
    }
}

if ($ropViolations.Count -gt 0) {
    Write-Host "⚠  Naked throw detected in service layer (use Result/Try/Option instead):" -ForegroundColor Yellow
    $ropViolations | ForEach-Object { Write-Host "   $_" -ForegroundColor Yellow }
    Write-Host "   See .agent/skills/rop-patterns/SKILL.md for guidance." -ForegroundColor Yellow
    # Warning only — does not block the commit
} else {
    Write-Host "✓ No naked throws in staged service files" -ForegroundColor Green
}

# ============================================
# FRONTEND TYPECHECK (warning-only, harness plan item #2)
# ============================================
# Reports TypeScript errors when any .tsx/.ts file under
# ReactComponents/ga-react-components/ is staged. Currently warning-only:
# baseline at the time this hook was added carried ~185 pre-existing errors
# (BSPDoomExplorer, ForceRadiant, StringedInstrumentFretboard, etc.), so
# making this fatal would gate every commit. Promote to fatal once the
# baseline reaches zero — see docs/plans/2026-05-23-arch-harness-engineering-
# adoption-plan.md item #2.
$stagedTsFiles = git diff --cached --name-only 2>$null |
    Where-Object { $_ -match "^ReactComponents/ga-react-components/.*\.(ts|tsx)$" }
if ($stagedTsFiles.Count -gt 0) {
    Write-Host "`n▶ Running frontend typecheck (warning-only)..." -ForegroundColor Blue
    Push-Location ReactComponents/ga-react-components
    try {
        $typecheckOutput = npm run --silent typecheck 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Frontend typecheck clean" -ForegroundColor Green
        } else {
            $errorLines = $typecheckOutput | Select-String -Pattern 'error TS'
            $errorCount = $errorLines.Count
            $topOffenders = $errorLines |
                ForEach-Object { ($_.Line -split '\(')[0] } |
                Group-Object | Sort-Object Count -Descending |
                Select-Object -First 3 |
                ForEach-Object { "$($_.Name) ($($_.Count))" }
            $topStr = $topOffenders -join ', '
            Write-Host "⚠ Frontend typecheck reports $errorCount type errors (baseline ~185 — see plan item #2)" -ForegroundColor Yellow
            Write-Host "  Top offenders: $topStr" -ForegroundColor Yellow
            Write-Host "  Run: cd ReactComponents/ga-react-components; npm run typecheck" -ForegroundColor Yellow
            # Warning only — does not block the commit
        }
    } finally {
        Pop-Location
    }
} else {
    # No frontend files staged; skip silently to keep hook fast on backend commits.
}

# ============================================
# SYNC AGENTS.md FROM CLAUDE.md
# ============================================
Write-Host "`n▶ Syncing AGENTS.md from CLAUDE.md..." -ForegroundColor Blue
$syncScript = Join-Path $PSScriptRoot '..' 'Scripts' 'sync-agents-md.ps1'

# Safety: never auto-sync while a merge/rebase/cherry-pick is in progress, or
# when AGENTS.md has an unresolved conflict (UU/AA in porcelain). Auto-sync
# during merge resolution silently clobbers the human's resolution choice.
$inMerge = (Test-Path '.git/MERGE_HEAD') -or (Test-Path '.git/REBASE_HEAD') -or (Test-Path '.git/CHERRY_PICK_HEAD')
$agentsConflict = $false
if (Test-Path AGENTS.md) {
    $porcelain = git status --porcelain AGENTS.md 2>$null
    if ($porcelain -and $porcelain -match '^(UU|AA|DD|AU|UA|DU|UD)') { $agentsConflict = $true }
}

if ($inMerge -or $agentsConflict) {
    Write-Host "⚠ Merge/conflict state detected — skipping AGENTS.md sync to preserve manual resolution" -ForegroundColor Yellow
} elseif (Test-Path $syncScript) {
    & pwsh -NoProfile -File $syncScript
    if ($LASTEXITCODE -eq 0) {
        # If sync produced changes, stage AGENTS.md so the commit includes the update.
        $agentsStatus = git diff --name-only AGENTS.md 2>$null
        if ($agentsStatus) {
            git add AGENTS.md
            Write-Host "  (AGENTS.md was out of sync — restaged)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "✗ AGENTS.md sync failed" -ForegroundColor Red
        $script:HasErrors = $true
    }
} else {
    Write-Host "⚠ Sync script missing at $syncScript" -ForegroundColor Yellow
}

# ============================================
# CONTEXT STALENESS CHECK (warning-only, never blocks)
# ============================================
Write-Host "`n▶ Checking context staleness..." -ForegroundColor Blue

$stalenessScript = Join-Path $PSScriptRoot ".." "Scripts" "check-context-staleness.ps1"
$stalenessScript = (Resolve-Path -Path $stalenessScript -ErrorAction SilentlyContinue)
if ($stalenessScript -and (Test-Path $stalenessScript))
{
    # Capture stderr — the linter writes warnings there and always exits 0.
    $stalenessWarnings = & pwsh -NoProfile -File $stalenessScript 2>&1 | Where-Object { $_ -match '^\[staleness\]' }
    if ($stalenessWarnings)
    {
        foreach ($w in $stalenessWarnings) {
            Write-Host "⚠  $w" -ForegroundColor Yellow
        }
        Write-Host "   (warning only — commit not blocked)" -ForegroundColor DarkGray
    }
    else
    {
        Write-Host "✓ Context artifacts in sync" -ForegroundColor Green
    }
}
else
{
    # Script missing — silent, this is opt-in surface.
}

# ============================================
# THEME.TS SYNC CHECK (DESIGN.md Phase 2)
# ============================================
# Only run when a DESIGN.md or theme.ts change is staged, to avoid the
# ~1s node startup cost on every commit. Reads the canonical tokens
# from /DESIGN.md and verifies src/theme.ts matches; if out of sync,
# the developer needs to `npm run gen:theme` and re-stage.
$themeRelevantStaged = git diff --cached --name-only 2>$null |
    Where-Object { $_ -match '^DESIGN\.md$' -or $_ -match 'ReactComponents/ga-react-components/(src/theme\.ts|scripts/gen-theme-from-design\.mjs)$' }
if ($themeRelevantStaged) {
    Write-Host "`n▶ Checking src/theme.ts is in sync with DESIGN.md..." -ForegroundColor Blue
    Push-Location (Join-Path $PSScriptRoot '..' 'ReactComponents' 'ga-react-components')
    & npm run gen:theme:check --silent 2>&1 | Where-Object { $_ -match '^[✓✗]' } | ForEach-Object { Write-Host "  $_" }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ src/theme.ts is out of sync with DESIGN.md" -ForegroundColor Red
        Write-Host "  Run: cd ReactComponents/ga-react-components && npm run gen:theme && git add src/theme.ts" -ForegroundColor Yellow
        $script:HasErrors = $true
    }
    Pop-Location
}

# ============================================
# RESULT
# ============================================
Write-Host "`n========================================" -ForegroundColor Cyan

if ($script:HasErrors) {
    Write-Host "✗ Pre-commit checks failed!" -ForegroundColor Red
    Write-Host "  Fix the issues above before committing.`n" -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "✓ All pre-commit checks passed!`n" -ForegroundColor Green
    exit 0
}
