#!/usr/bin/env pwsh

<#
.SYNOPSIS
    One-shot stamper that adds `last_verified` to every .agent/skills/SKILL.md
    that doesn't already have it. Idempotent — re-running has no effect on
    files already stamped.

.DESCRIPTION
    For each SKILL.md:
      - If frontmatter exists and does NOT contain `last_verified:`, insert
        it just after `description:` (or at the end of frontmatter if no
        description present).
      - If frontmatter is missing entirely, the file is skipped — those
        cases need an authoring decision and should not be auto-stamped.

    The chosen date is today's date in YYYY-MM-DD form, in UTC, since the
    health-check parser expects that exact format.

.PARAMETER Date
    Date string to write. Default: today's UTC date.

.PARAMETER DryRun
    Print the files that would be modified without writing.
#>

[CmdletBinding()]
param(
    [string]$Date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd"),
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Resolve .agent/skills relative to this script.
$dir = $PSScriptRoot
$skillsDir = $null
while ($dir) {
    $cand = Join-Path $dir ".agent/skills"
    if (Test-Path $cand) { $skillsDir = (Resolve-Path $cand).Path; break }
    $parent = Split-Path $dir -Parent
    if ($parent -eq $dir) { break }
    $dir = $parent
}
if (-not $skillsDir) { throw ".agent/skills not found from $PSScriptRoot upward" }

$files = Get-ChildItem -Path $skillsDir -Recurse -Filter "SKILL.md" -File
$stamped = 0
$skippedNoFm = 0
$alreadyStamped = 0

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw

    if (-not $content.StartsWith("---")) {
        Write-Host "  skip (no frontmatter): $($file.FullName)" -ForegroundColor DarkGray
        $skippedNoFm++
        continue
    }

    # Locate end of frontmatter.
    $lines = $content -split "(?<=`n)"
    $end = -1
    for ($i = 1; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match '^---\s*\r?\n?$') { $end = $i; break }
    }
    if ($end -lt 1) {
        Write-Host "  skip (malformed frontmatter): $($file.FullName)" -ForegroundColor Yellow
        $skippedNoFm++
        continue
    }

    $fmBlock = ($lines[1..($end - 1)] -join "")

    if ($fmBlock -match '(?m)^last_verified\s*:') {
        $alreadyStamped++
        continue
    }

    # Find a sensible insertion point: just after the `description:` line if
    # present, otherwise just before the closing `---`.
    $insertAt = $end  # default: just before closing ---
    for ($i = 1; $i -lt $end; $i++) {
        if ($lines[$i] -match '^description\s*:') { $insertAt = $i + 1; break }
    }

    $newLine = "last_verified: $Date`n"
    $newLines = @()
    $newLines += $lines[0..($insertAt - 1)]
    $newLines += $newLine
    if ($insertAt -le ($lines.Length - 1)) {
        $newLines += $lines[$insertAt..($lines.Length - 1)]
    }
    $newContent = $newLines -join ""

    if ($DryRun) {
        Write-Host "  would stamp: $($file.FullName)" -ForegroundColor Cyan
    } else {
        Set-Content -Path $file.FullName -Value $newContent -NoNewline
        Write-Host "  stamped: $($file.FullName)" -ForegroundColor Green
    }
    $stamped++
}

Write-Host ""
Write-Host "Summary: stamped=$stamped, already=$alreadyStamped, skipped=$skippedNoFm"
