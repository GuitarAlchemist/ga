# Stop hook — appends a metadata-only finalize stub to state/digests/latest.md at
# session end IF the last /digest invocation was >10 min ago. Karpathy R4: every
# session boundary is a goal-driven checkpoint. Idempotent — safe to run alongside
# the existing session-pr-check Stop hook.

$ErrorActionPreference = 'SilentlyContinue'

# Sanitizers — same as precompact-digest.ps1. Closes path-traversal + YAML
# injection findings from the 2026-05-15 security review.
function Get-SafeId {
    param([string]$Value, [string]$Fallback = 'unknown', [int]$MaxLen = 64)
    if (-not $Value) { return $Fallback }
    $cleaned = $Value -replace '[\r\n\t]', ''
    if ($cleaned.Length -gt $MaxLen) { $cleaned = $cleaned.Substring(0, $MaxLen) }
    if ($cleaned -match '^[A-Za-z0-9._\-]+$') { return $cleaned }
    return $Fallback
}
function Get-SafeYaml {
    param([string]$Value, [int]$MaxLen = 200)
    if ($null -eq $Value -or $Value -eq '') { return 'null' }
    $cleaned = ($Value -replace '[\r\n]', ' ')
    if ($cleaned.Length -gt $MaxLen) { $cleaned = $cleaned.Substring(0, $MaxLen) + '...' }
    $escaped = $cleaned -replace "'", "''"
    return "'$escaped'"
}

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

$digestDir = Join-Path $repoRoot 'state\digests'
$archDir   = Join-Path $digestDir 'archive'
$latest    = Join-Path $digestDir 'latest.md'

if (Test-Path $latest) {
    $age = (Get-Date) - (Get-Item $latest).LastWriteTime
    if ($age.TotalMinutes -lt 10) { exit 0 }
}

New-Item -ItemType Directory -Path $digestDir, $archDir -Force | Out-Null

$tsFile = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH-mm-ssZ')
$tsIso  = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

if (Test-Path $latest) {
    Copy-Item $latest (Join-Path $archDir "$tsFile-stop.md") -Force
}

$branch   = & git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null
$headSha  = & git -C $repoRoot rev-parse --short HEAD 2>$null
$headSubj = & git -C $repoRoot log -1 --format='%s' 2>$null

$openPr = $null
if (Get-Command gh -ErrorAction SilentlyContinue) {
    $prJson = & gh pr view --json number 2>$null
    if ($prJson) {
        try { $openPr = "#$(($prJson | ConvertFrom-Json).number)" } catch {}
    }
}
$prLine = if ($openPr) { "**Open PR:** $openPr`n" } else { '' }

$digest = @"
---
schema_version: 1
session_id: stop-finalize
written_at: $tsIso
trigger: stop-hook-finalize
branch: $(Get-SafeYaml $branch)
head_sha: $(Get-SafeYaml $headSha)
head_subject: $(Get-SafeYaml $headSubj)
open_pr: $(Get-SafeYaml $openPr)
---

# Session digest (Stop-hook finalize — /digest not invoked in last 10 min)

**Branch:** $branch @ $headSha — $headSubj
$prLine
## Model-driven sections

_Session ended without a recent ``/digest``. Next session: re-orient from
``git log`` + open PR + ``docs/solutions/`` recent entries. Prior digests
(if any) are in ``state/digests/archive/``._
"@

Set-Content -Path $latest -Value $digest -Encoding UTF8

# Karpathy R11: validate our own output before exiting.
& (Join-Path $repoRoot 'Scripts\digest-validate.ps1') -DigestPath $latest 2>$null

exit 0
