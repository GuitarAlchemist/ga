# Stop-hook helper. When a Claude Code session ends, surface a quick PR
# status summary if the current branch has an open PR. Catches the
# "shipped on local green, red on CI" gap mentioned in the
# feedback_check_ci_before_next_chunk memory.
#
# Output goes to stdout and is shown inline in the Claude Code session
# transcript. Returns 0 on success even when CI is red — surfacing the
# state is enough; we don't want to block the user from closing the
# session.

$ErrorActionPreference = 'SilentlyContinue'
$WarningPreference     = 'SilentlyContinue'

# Bail quietly when not in a git repo (or `gh` is missing) — keeps the
# hook silent on hosts that don't have the tooling installed.
$branch = & git branch --show-current 2>$null
if (-not $branch) { exit 0 }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { exit 0 }

# Probe the open PR for this branch. `gh pr view` exits non-zero when
# there's no PR — that's fine, we just exit quietly.
$prJson = & gh pr view --json number,statusCheckRollup,mergeable,isDraft 2>$null
if (-not $prJson) { exit 0 }

try {
    $pr = $prJson | ConvertFrom-Json
} catch {
    exit 0
}

$num    = $pr.number
$checks = $pr.statusCheckRollup
$draft  = $pr.isDraft

# Group checks by conclusion (Success / Failure / Pending / Skipped /
# Cancelled / etc). Many GitHub workflows report null `conclusion` while
# `status` is `IN_PROGRESS`.
$success = 0
$failed  = 0
$pending = 0
$other   = 0
foreach ($c in $checks) {
    $conc = if ($c.conclusion) { $c.conclusion } else { $c.status }
    switch ($conc) {
        'SUCCESS'      { $success++ }
        'NEUTRAL'      { $success++ }
        'FAILURE'      { $failed++ }
        'TIMED_OUT'    { $failed++ }
        'CANCELLED'    { $failed++ }
        'STARTUP_FAILURE' { $failed++ }
        'IN_PROGRESS'  { $pending++ }
        'QUEUED'       { $pending++ }
        'PENDING'      { $pending++ }
        'WAITING'      { $pending++ }
        default        { $other++ }
    }
}

$total = $checks.Count
if ($total -eq 0) {
    Write-Host ''
    Write-Host "PR #$num on $branch — no CI checks reported yet." -ForegroundColor Gray
    Write-Host ''
    exit 0
}

# Pick the right tone for the summary.
$colour = 'Yellow'
$state  = 'mixed'
if ($failed -gt 0)        { $colour = 'Red';   $state = 'red'   }
elseif ($pending -gt 0)   { $colour = 'Yellow';$state = 'pending' }
elseif ($success -eq $total) { $colour = 'Green'; $state = 'green' }

Write-Host ''
Write-Host ("PR #$num on $branch — CI is $state" + $(if ($draft) { ' (draft)' } else { '' })) -ForegroundColor $colour
Write-Host ("  ${success} green / ${failed} red / ${pending} pending / ${other} other (of ${total})") -ForegroundColor Gray

# When red, list the failing check names so the user knows what to look at.
if ($failed -gt 0) {
    $failingNames = $checks | Where-Object {
        $_.conclusion -in @('FAILURE','TIMED_OUT','CANCELLED','STARTUP_FAILURE')
    } | ForEach-Object { $_.name } | Select-Object -Unique
    foreach ($n in $failingNames) {
        Write-Host "  ✗ $n" -ForegroundColor Red
    }
    Write-Host "  → gh pr view $num --web   for the full report" -ForegroundColor DarkGray
}
Write-Host ''
exit 0
