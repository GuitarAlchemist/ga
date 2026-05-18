# SessionStart hook — emits state/digests/latest.md to stdout so Claude Code
# injects it as additionalContext for the model at session start. Skips silently
# if the digest is missing or >24h stale (prefer git log over stale digest).

$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

$latest = Join-Path $repoRoot 'state\digests\latest.md'
if (-not (Test-Path $latest)) { exit 0 }

$age = (Get-Date) - (Get-Item $latest).LastWriteTime
if ($age.TotalHours -gt 24) {
    Write-Host ''
    Write-Host "Session digest exists but is >24h old — skipping injection. Run /digest to refresh." -ForegroundColor DarkGray
    Write-Host ''
    exit 0
}

$ageStr = if ($age.TotalMinutes -lt 60) {
    "$([int]$age.TotalMinutes) min"
} else {
    "$([int]$age.TotalHours)h $([int]$age.Minutes)m"
}

Write-Host ''
Write-Host "=== Session digest (last written $ageStr ago) ===" -ForegroundColor Cyan
Get-Content $latest -Raw | Write-Host
Write-Host '=== End digest ===' -ForegroundColor Cyan
Write-Host ''
exit 0
