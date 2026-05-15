# PostToolUse hook (matcher: Edit|Write|Bash) — increments mutation counter.
# /digest skill resets the counter when invoked. Karpathy R4: counter feeds the
# staleness nudge so the reminder carries concrete data ("$N mutations since last
# digest"), not vague platitudes.

$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

$digestDir   = Join-Path $repoRoot 'state\digests'
$counterPath = Join-Path $digestDir '.activity-counter'

if (-not (Test-Path $digestDir)) {
    New-Item -ItemType Directory -Path $digestDir -Force | Out-Null
}

$count = 0
if (Test-Path $counterPath) {
    $raw = (Get-Content $counterPath -Raw).Trim()
    if ($raw -match '^\d+$') { $count = [int]$raw }
}
$count++

Set-Content -Path $counterPath -Value $count -Encoding UTF8
exit 0
