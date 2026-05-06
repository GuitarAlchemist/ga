#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Health-check for .agent/skills/ SKILL.md frontmatter and content.

.DESCRIPTION
    Walks every SKILL.md under .agent/skills/, validates YAML frontmatter,
    flags stale data (hardcoded ports, missing required fields, missing
    `last_verified` dates), and reports duplicates.

    Exit codes:
        0  No critical issues (warnings allowed)
        1  Critical issues found (missing required frontmatter, duplicate names)

.PARAMETER Path
    Root directory to scan. Default: .agent/skills relative to repo root.

.PARAMETER FailOnWarn
    Treat warnings (stale dates, hardcoded ports) as failures.

.PARAMETER Json
    Emit a JSON report instead of human-readable text.

.PARAMETER StaleAfterDays
    Number of days after which a `last_verified` date is treated as stale
    (warn). Default: 90.

.EXAMPLE
    pwsh Scripts/check-agent-skills.ps1
    Run with default settings; prints a markdown-style report.

.EXAMPLE
    pwsh Scripts/check-agent-skills.ps1 -FailOnWarn
    Treat stale dates and hardcoded-port references as build-breaking.
#>

[CmdletBinding()]
param(
    [string]$Path = $null,
    [switch]$FailOnWarn,
    [switch]$Json,
    [int]$StaleAfterDays = 90
)

$ErrorActionPreference = "Stop"

# Resolve the .agent/skills directory anchored at the repo root.
function Resolve-SkillsDir {
    param([string]$Override)
    if ($Override) { return (Resolve-Path $Override).Path }

    $dir = $PSScriptRoot
    while ($dir) {
        $cand = Join-Path $dir ".agent/skills"
        if (Test-Path $cand) { return (Resolve-Path $cand).Path }
        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    throw ".agent/skills directory not found from $PSScriptRoot upward"
}

function Get-Frontmatter {
    param([string]$Content)

    if (-not $Content.StartsWith("---")) { return $null }

    $lines = $Content -split "(`r`n|`n|`r)"
    $end = -1
    for ($i = 1; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -match '^---\s*$') { $end = $i; break }
    }
    if ($end -lt 1) { return $null }

    $fmLines = $lines[1..($end - 1)]
    $fm = [ordered]@{}
    $currentKey = $null
    foreach ($line in $fmLines) {
        # Skip separator-only lines (empty, or pure CR/LF — the -split with
        # capture groups keeps line endings as their own elements).
        if ([string]::IsNullOrEmpty($line) -or $line -match '^[\r\n]+$') { continue }
        # Indented continuation (list item or mapping value) — append to current key.
        if ($line -match '^\s+\S') {
            if ($currentKey) { $fm[$currentKey] += "`n" + $line.TrimEnd() }
            continue
        }
        if ($line -match '^([a-zA-Z][a-zA-Z0-9_-]*)\s*:\s*(.*)$') {
            $currentKey = $matches[1]
            $val = $matches[2].Trim()
            # Strip surrounding quotes if present.
            if ($val -match '^"(.*)"$') { $val = $matches[1] }
            elseif ($val -match "^'(.*)'$") { $val = $matches[1] }
            $fm[$currentKey] = $val
        }
    }
    return $fm
}

# Findings accumulators
$skills = @()
$nameMap = @{}
$totalCritical = 0
$totalWarn = 0
$totalInfo = 0

$skillsDir = Resolve-SkillsDir -Override $Path
$files = Get-ChildItem -Path $skillsDir -Recurse -Filter "SKILL.md" -File

foreach ($file in $files) {
    $relPath = $file.FullName.Substring((Resolve-Path (Split-Path $skillsDir -Parent)).Path.Length + 1).Replace('\', '/')
    $content = Get-Content -Path $file.FullName -Raw
    $issues = @()

    $fm = Get-Frontmatter -Content $content

    if ($null -eq $fm) {
        $issues += [pscustomobject]@{ severity = "critical"; code = "no-frontmatter"; message = "no YAML frontmatter found" }
    } else {
        if (-not $fm.Contains("name") -or [string]::IsNullOrWhiteSpace($fm["name"])) {
            $issues += [pscustomobject]@{ severity = "critical"; code = "missing-name"; message = "frontmatter missing required 'name' field" }
        }
        if (-not $fm.Contains("description") -or [string]::IsNullOrWhiteSpace($fm["description"])) {
            $issues += [pscustomobject]@{ severity = "critical"; code = "missing-description"; message = "frontmatter missing required 'description' field" }
        }
        if (-not $fm.Contains("last_verified")) {
            $issues += [pscustomobject]@{ severity = "warn"; code = "missing-last-verified"; message = "no 'last_verified' date — staleness invisible" }
        } else {
            $dateStr = $fm["last_verified"]
            [DateTime]$parsed = [DateTime]::MinValue
            if ([DateTime]::TryParseExact($dateStr, "yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsed)) {
                $age = (Get-Date) - $parsed
                if ($age.TotalDays -gt $StaleAfterDays) {
                    $issues += [pscustomobject]@{ severity = "warn"; code = "stale-last-verified"; message = "last_verified $dateStr is $([int]$age.TotalDays) days old (threshold $StaleAfterDays)" }
                }
            } else {
                $issues += [pscustomobject]@{ severity = "warn"; code = "bad-last-verified"; message = "last_verified '$dateStr' is not in YYYY-MM-DD format" }
            }
        }
        if (-not $fm.Contains("triggers")) {
            $issues += [pscustomobject]@{ severity = "info"; code = "no-triggers"; message = "no 'triggers' field — skill won't auto-dispatch by phrase match" }
        }

        # Duplicate-name detection.
        if ($fm.Contains("name") -and $fm["name"]) {
            $nm = $fm["name"]
            if ($nameMap.ContainsKey($nm)) {
                $issues += [pscustomobject]@{ severity = "critical"; code = "duplicate-name"; message = "duplicate of $($nameMap[$nm])" }
            } else {
                $nameMap[$nm] = $relPath
            }
        }
    }

    # Body-level checks: hardcoded GA.MusicTheory.Service port (7001) where
    # the GaApi port (5232) is almost always the right target.
    if ($content -match 'localhost:7001') {
        $count = ([regex]::Matches($content, 'localhost:7001')).Count
        $issues += [pscustomobject]@{ severity = "warn"; code = "stale-port-7001"; message = "$count occurrence(s) of localhost:7001 (GA.MusicTheory.Service); GaApi is on 5232" }
    }

    foreach ($iss in $issues) {
        switch ($iss.severity) {
            "critical" { $totalCritical++ }
            "warn"     { $totalWarn++ }
            "info"     { $totalInfo++ }
        }
    }

    $skills += [pscustomobject]@{
        path = $relPath
        name = if ($fm -and $fm["name"]) { $fm["name"] } else { "<unknown>" }
        last_verified = if ($fm -and $fm["last_verified"]) { $fm["last_verified"] } else { $null }
        issues = @($issues)
    }
}

# Output
if ($Json) {
    $report = [pscustomobject]@{
        scanned_root = $skillsDir
        scanned_count = $skills.Count
        critical = $totalCritical
        warn = $totalWarn
        info = $totalInfo
        skills = $skills
    }
    $report | ConvertTo-Json -Depth 6
} else {
    Write-Host ""
    Write-Host "GA Skills Health Check" -ForegroundColor Cyan
    Write-Host "Scanned: $skillsDir" -ForegroundColor Gray
    Write-Host "Found $($skills.Count) SKILL.md file(s); critical=$totalCritical warn=$totalWarn info=$totalInfo"
    Write-Host ""

    foreach ($s in ($skills | Sort-Object path)) {
        if ($s.issues.Count -eq 0) { continue }
        Write-Host "## $($s.path)" -ForegroundColor White
        if ($s.last_verified) {
            Write-Host "   last_verified: $($s.last_verified)" -ForegroundColor DarkGray
        }
        foreach ($iss in $s.issues) {
            $color = switch ($iss.severity) {
                "critical" { "Red" }
                "warn"     { "Yellow" }
                "info"     { "DarkGray" }
                default    { "White" }
            }
            Write-Host "   [$($iss.severity.ToUpper())] $($iss.code): $($iss.message)" -ForegroundColor $color
        }
        Write-Host ""
    }

    Write-Host "Summary: $($skills.Count) skill(s) scanned; $totalCritical critical, $totalWarn warning(s), $totalInfo info"
}

# Exit code policy:
# - critical issues always fail (1)
# - warnings fail only when -FailOnWarn is set
$exitCode = 0
if ($totalCritical -gt 0) { $exitCode = 1 }
elseif ($FailOnWarn -and $totalWarn -gt 0) { $exitCode = 1 }

exit $exitCode
