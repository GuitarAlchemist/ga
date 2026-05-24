# PostToolUse hook (matcher: Edit|Write|Bash) — increments mutation counter.
# /digest skill resets the counter when invoked. Karpathy R4: counter feeds the
# staleness nudge so the reminder carries concrete data ("$N mutations since last
# digest"), not vague platitudes.
#
# Enhancement 1 (Cherny periodic mid-session digest): when activity threshold OR
# time threshold is hit, write a mid-session metadata digest so we survive
# crashes/network drops without waiting for Stop or PreCompact.

$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

$digestDir      = Join-Path $repoRoot 'state\digests'
$archDir        = Join-Path $digestDir 'archive'
$counterPath    = Join-Path $digestDir '.activity-counter'
$midCounterPath = Join-Path $digestDir '.activity-count'
$latest         = Join-Path $digestDir 'latest.md'

if (-not (Test-Path $digestDir)) { New-Item -ItemType Directory -Path $digestDir -Force | Out-Null }
if (-not (Test-Path $archDir))   { New-Item -ItemType Directory -Path $archDir -Force | Out-Null }

# Existing mutation counter (drives staleness nudge)
$count = 0
if (Test-Path $counterPath) {
    $raw = (Get-Content $counterPath -Raw).Trim()
    if ($raw -match '^\d+$') { $count = [int]$raw }
}
$count++
Set-Content -Path $counterPath -Value $count -Encoding UTF8

# Enhancement 1: independent counter for mid-session digest gating
$midCount = 0
if (Test-Path $midCounterPath) {
    $raw = (Get-Content $midCounterPath -Raw).Trim()
    if ($raw -match '^\d+$') { $midCount = [int]$raw }
}
$midCount++
Set-Content -Path $midCounterPath -Value $midCount -Encoding UTF8

# Thresholds: N=20 mutations OR M=30 minutes since last digest
$thresholdCount = 20
$thresholdMin   = 30
if ($env:GA_DIGEST_MID_COUNT -and $env:GA_DIGEST_MID_COUNT -match '^\d+$') { $thresholdCount = [int]$env:GA_DIGEST_MID_COUNT }
if ($env:GA_DIGEST_MID_MIN   -and $env:GA_DIGEST_MID_MIN   -match '^\d+$') { $thresholdMin   = [int]$env:GA_DIGEST_MID_MIN }

$ageMin = 99999
if (Test-Path $latest) {
    $ageMin = [int]((Get-Date) - (Get-Item $latest).LastWriteTime).TotalMinutes
}

$shouldWrite = $false
if ($midCount -ge $thresholdCount) { $shouldWrite = $true }
elseif ($ageMin -ge $thresholdMin -and $midCount -ge 3) { $shouldWrite = $true }

if (-not $shouldWrite) { exit 0 }

# Reset mid-session counter
Set-Content -Path $midCounterPath -Value 0 -Encoding UTF8

$tsIso  = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
$tsFile = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH-mm-ssZ')

$branch   = & git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null
$headSha  = & git -C $repoRoot rev-parse --short HEAD 2>$null
$headSubj = & git -C $repoRoot log -1 --format='%s' 2>$null

# Rotate existing latest into archive
if (Test-Path $latest) {
    Copy-Item $latest (Join-Path $archDir "$tsFile-pre-mid.md") -Force
}

$midPath = Join-Path $digestDir "mid-$tsFile.md"
$md = @"
---
schema_version: 1
session_id: mid-session-auto
written_at: $tsIso
trigger: activity-tracker-mid-session
branch: $branch
head_sha: $headSha
head_subject: $headSubj
mutations_since_last: $midCount
---

# Session digest (mid-session auto — activity threshold reached)

**Branch:** $branch @ $headSha — $headSubj

## Model-driven sections

_Auto-written by digest-activity-tracker after $midCount mutations / ${ageMin}m since last digest.
Invoke ``/digest`` at your next natural breakpoint to populate **Next action**,
**In-flight**, **Live hypotheses**, **Open questions**, **Do NOT carry forward**,
and **Success criteria**._
"@

Set-Content -Path $midPath -Value $md -Encoding UTF8
Copy-Item $midPath $latest -Force
exit 0
