#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Sync AGENTS.md from CLAUDE.md so codex/OpenAI tooling and Claude read identical instructions.

.DESCRIPTION
    CLAUDE.md is canonical. This script regenerates AGENTS.md as a literal copy
    with the title swapped (# CLAUDE.md -> # AGENTS.md) and a "do not edit" warning
    appended at the top so humans don't accidentally edit AGENTS.md directly.

    Called by .githooks/pre-commit before each commit; can also be run manually.
    Idempotent — exits 0 if AGENTS.md already matches.

.EXAMPLE
    pwsh Scripts/sync-agents-md.ps1
    Sync AGENTS.md from CLAUDE.md.

.EXAMPLE
    pwsh Scripts/sync-agents-md.ps1 -CheckOnly
    Exit 1 if AGENTS.md is out of sync (for CI).
#>
param(
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$claude = Join-Path $repoRoot 'CLAUDE.md'
$agents = Join-Path $repoRoot 'AGENTS.md'

if (-not (Test-Path $claude)) {
    Write-Host "CLAUDE.md not found at $claude" -ForegroundColor Red
    exit 1
}

$claudeContent = Get-Content $claude -Raw

# Generate the AGENTS.md body: replace the title and the self-referential note.
$agentsContent = $claudeContent `
    -replace '^# CLAUDE\.md', '# AGENTS.md' `
    -replace 'Edit `CLAUDE\.md`; never edit `AGENTS\.md` directly\.', 'Source of truth is `CLAUDE.md`. This file is auto-generated — do not edit directly.'

$existing = if (Test-Path $agents) { Get-Content $agents -Raw } else { '' }

if ($existing -eq $agentsContent) {
    if (-not $CheckOnly) {
        Write-Host "✓ AGENTS.md already in sync with CLAUDE.md" -ForegroundColor Green
    }
    exit 0
}

if ($CheckOnly) {
    Write-Host "✗ AGENTS.md is out of sync with CLAUDE.md" -ForegroundColor Red
    Write-Host "  Run: pwsh Scripts/sync-agents-md.ps1" -ForegroundColor Yellow
    exit 1
}

Set-Content -Path $agents -Value $agentsContent -NoNewline
Write-Host "✓ AGENTS.md synced from CLAUDE.md ($($agentsContent.Length) chars)" -ForegroundColor Green
