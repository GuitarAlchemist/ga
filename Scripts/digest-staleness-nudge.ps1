# UserPromptSubmit hook — emits a one-line additionalContext nudge when state has
# drifted from the last /digest. Karpathy R1 (think before acting) + R4 (goal-driven
# feedback). Silent when digest is fresh.

$ErrorActionPreference = 'SilentlyContinue'

$repoRoot = & git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) { exit 0 }

$latest  = Join-Path $repoRoot 'state\digests\latest.md'
$counter = Join-Path $repoRoot 'state\digests\.activity-counter'

$digestAgeMin = $null
if (Test-Path $latest) {
    $digestAgeMin = [int]((Get-Date) - (Get-Item $latest).LastWriteTime).TotalMinutes
}

$mutationCount = 0
if (Test-Path $counter) {
    $raw = (Get-Content $counter -Raw).Trim()
    if ($raw -match '^\d+$') { $mutationCount = [int]$raw }
}

$shouldNudge = $false
$reason = ''
if ($null -eq $digestAgeMin) {
    $shouldNudge = $true
    $reason = "No session digest exists yet. Invoke /digest at your next natural breakpoint to seed it."
} elseif ($digestAgeMin -gt 30 -and $mutationCount -gt 10) {
    $shouldNudge = $true
    $reason = "Last digest $digestAgeMin min ago; $mutationCount mutations since. Karpathy R4: task complete != goal achieved — consider /digest to mark success criteria status before continuing."
} elseif ($digestAgeMin -gt 90) {
    $shouldNudge = $true
    $reason = "Last digest $digestAgeMin min ago. Session drifting — invoke /digest before the next compaction or session boundary."
}

if (-not $shouldNudge) { exit 0 }

$payload = @{ additionalContext = "[digest-nudge] $reason" } | ConvertTo-Json -Compress
Write-Output $payload
exit 0
