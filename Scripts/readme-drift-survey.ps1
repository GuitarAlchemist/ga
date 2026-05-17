<#
.SYNOPSIS
    Surveys README staleness across the GuitarAlchemist sibling repos and
    emits a structured drift report.

.DESCRIPTION
    For each known sibling repo, computes:
      - README mtime  (the README's filesystem mtime)
      - README touch  (date of last commit that modified README.md)
      - HEAD date     (date of the most recent commit on the default branch)
      - drift_days    = HEAD_date - README_touch
      - status        = ok | borderline | stale | very-stale

    Drift thresholds are read from state/quality/readme-drift/baseline.json.

    Emits the report to stdout as JSON, and (if -OutPath is provided) writes a
    YYYY-MM-DD.json snapshot for ix-quality-trend consumption.

    Failure semantics: missing sibling directories are reported as "absent"
    (status="absent") rather than failing the survey. This lets the workflow
    run on a partial-checkout (e.g., CI runner without every sibling cloned)
    and surface what it can see.

.PARAMETER OutPath
    If provided, writes the JSON report to this path. Otherwise just prints.

.PARAMETER SiblingsRoot
    Parent directory containing all sibling repos. Defaults to one level above
    this repo (matches the standard local layout C:\Users\spare\source\repos).

.PARAMETER Baseline
    Path to baseline.json with the drift thresholds. Defaults to
    state/quality/readme-drift/baseline.json relative to this repo.

.EXAMPLE
    pwsh Scripts/readme-drift-survey.ps1
    pwsh Scripts/readme-drift-survey.ps1 -OutPath state/quality/readme-drift/2026-05-17.json
#>
[CmdletBinding()]
param(
    [string]$OutPath,
    [string]$SiblingsRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$Baseline = (Join-Path $PSScriptRoot '..\state\quality\readme-drift\baseline.json')
)

$ErrorActionPreference = 'Stop'

# ── Repos to survey. Keep this list in sync with the baseline.json
#    `tracked_repos` field — the workflow asserts they match.
$Repos = @(
    'ga',
    'ix',
    'Demerzel',
    'tars',
    'guitaralchemist.github.io',
    'mergerisk',
    'agent-blackbox',
    'demerzel-bot',
    'devto-mcp',
    'fin',
    'ga-godot',
    'hari'
)

if (-not (Test-Path $Baseline)) {
    throw "Baseline contract not found at $Baseline — run from the ga repo root or pass -Baseline."
}
$thresholds = (Get-Content $Baseline -Raw | ConvertFrom-Json).thresholds_days
$borderlineDays = [int]$thresholds.borderline
$staleDays      = [int]$thresholds.stale
$veryStaleDays  = [int]$thresholds.very_stale

function Get-DriftStatus([int]$days) {
    if ($days -ge $veryStaleDays)  { return 'very-stale' }
    if ($days -ge $staleDays)      { return 'stale' }
    if ($days -ge $borderlineDays) { return 'borderline' }
    return 'ok'
}

$now = Get-Date
$report = [pscustomobject]@{
    schema_version = 1
    domain         = 'readme-drift'
    snapshot_date  = $now.ToUniversalTime().ToString('yyyy-MM-dd')
    snapshot_at    = $now.ToUniversalTime().ToString('o')
    thresholds_days = $thresholds
    repos          = @()
    summary        = [pscustomobject]@{
        total       = 0
        ok          = 0
        borderline  = 0
        stale       = 0
        very_stale  = 0
        absent      = 0
    }
}

foreach ($repo in $Repos) {
    $repoPath = Join-Path $SiblingsRoot $repo
    $readme   = Join-Path $repoPath 'README.md'

    if (-not (Test-Path $repoPath)) {
        $entry = [pscustomobject]@{
            repo            = $repo
            status          = 'absent'
            reason          = "Repo directory not found at $repoPath"
        }
        $report.repos += $entry
        $report.summary.absent++
        continue
    }
    if (-not (Test-Path $readme)) {
        $entry = [pscustomobject]@{
            repo            = $repo
            status          = 'absent'
            reason          = 'README.md not found in repo'
        }
        $report.repos += $entry
        $report.summary.absent++
        continue
    }

    # README mtime — filesystem signal (changes on local edits even before commit).
    $mtime = (Get-Item $readme).LastWriteTimeUtc.ToString('yyyy-MM-dd')

    # README touch — last commit that modified the README. This is the
    # authoritative "when was the README intentionally updated" signal.
    $lastCommit = git -C $repoPath log -1 --format='%cs' -- README.md 2>$null
    if (-not $lastCommit) { $lastCommit = $mtime }

    # HEAD date — most recent commit on the branch.
    $headDate = git -C $repoPath log -1 --format='%cs' 2>$null
    if (-not $headDate) { $headDate = $mtime }

    $driftDays = [int]([datetime]::Parse($headDate) - [datetime]::Parse($lastCommit)).TotalDays
    $status = Get-DriftStatus $driftDays

    $report.repos += [pscustomobject]@{
        repo            = $repo
        status          = $status
        readme_mtime    = $mtime
        readme_touch    = $lastCommit
        head_date       = $headDate
        drift_days      = $driftDays
    }
    $report.summary.total++
    switch ($status) {
        'ok'         { $report.summary.ok++ }
        'borderline' { $report.summary.borderline++ }
        'stale'      { $report.summary.stale++ }
        'very-stale' { $report.summary.very_stale++ }
    }
}

$json = $report | ConvertTo-Json -Depth 5
$json

if ($OutPath) {
    $outDir = Split-Path $OutPath -Parent
    if ($outDir -and -not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    }
    $json | Set-Content -Path $OutPath -Encoding UTF8
    Write-Host "Snapshot written to $OutPath" -ForegroundColor Cyan
}
