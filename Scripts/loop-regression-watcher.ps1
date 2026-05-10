# Closes the gate-ROI feedback loop using git-inferred regression signal.
#
# Without production telemetry (L4 prereq, not yet shipped), we don't know
# which merged PRs caused real user-facing regressions. But every regression
# eventually surfaces as either a `Revert "<original>"` commit or a
# follow-up `fix(...)` PR that references the original. This script:
#
#   1. Scans `git log --since=<window>` for `^Revert "..."` commits
#      AND merged PRs whose title starts with "Revert " or "revert: ".
#   2. For each revert, finds the original PR (via the reverted commit's
#      associated PR, or via the title that comes after "Revert ").
#   3. Cross-references state/quality/gate-ledger.jsonl. If an entry
#      exists for the reverted PR with decision="merged-clean", that
#      gate said the PR was safe to merge but reality disagreed —
#      mark it `merged-with-revert` with a pointer to the revert PR.
#   4. Optionally (with -Apply) rewrites the affected ledger rows.
#      Default mode is dry-run report.
#
# This is the L3-promotion empirical signal in a form that does NOT
# require production telemetry. As soon as a regression is reverted,
# the gate-ledger updates and uniqueFindingsCount-per-gate analysis
# tells us which gates missed it.
#
# Usage:
#   pwsh Scripts/loop-regression-watcher.ps1                  # report, last 30 days
#   pwsh Scripts/loop-regression-watcher.ps1 -Days 90         # wider window
#   pwsh Scripts/loop-regression-watcher.ps1 -Apply           # rewrite ledger rows
#   pwsh Scripts/loop-regression-watcher.ps1 -Json            # machine-readable

[CmdletBinding()]
param(
    [int]$Days = 30,
    [switch]$Apply,
    [switch]$Json,
    [string]$RepoRoot = (Resolve-Path .).Path
)

$ErrorActionPreference = 'Stop'

$ledger = Join-Path $RepoRoot 'state/quality/gate-ledger.jsonl'
$hasLedger = Test-Path $ledger

# ---- Find revert commits in the window ----
$since = "$Days days ago"
$revertLines = & git log --since=$since --pretty=format:'%H|%s' 2>$null |
    Where-Object { $_ -match '^[0-9a-f]+\|(Revert "|revert[:(])' }

$regressions = @()
foreach ($line in $revertLines) {
    if (-not $line) { continue }
    $parts = $line -split '\|', 2
    if ($parts.Count -ne 2) { continue }
    $revertSha = $parts[0]
    $subject = $parts[1]

    # Try to find the original commit this revert undoes.
    # `git log --grep` is unreliable because commit messages can match
    # incidentally. Better path: look at the body of the revert commit,
    # which contains `This reverts commit <sha>.`
    $body = & git show --no-patch --pretty=format:%B $revertSha 2>$null
    $originalSha = $null
    if ($body -match 'This reverts commit ([0-9a-f]{7,40})') {
        $originalSha = $Matches[1]
    }

    # Find the PR# this revert is associated with (the revert PR), and
    # the PR# of the original commit (the regression PR).
    # NB: `gh pr list --search <sha>` does fuzzy text matching and returns
    # false positives. Use the commits/<sha>/pulls API for precise
    # commit-to-PR mapping (returns empty array if commit wasn't part of
    # any PR, which is common for direct-to-main reverts).
    $revertPr = $null
    $originalPr = $null
    $originalTitle = ''
    $originalMergedAt = $null
    try {
        $revertPrJson = & gh api "repos/{owner}/{repo}/commits/$revertSha/pulls" 2>$null
        if ($revertPrJson) {
            $arr = $revertPrJson | ConvertFrom-Json
            if ($arr -and $arr.Count -gt 0) { $revertPr = $arr[0].number }
        }
    } catch {}
    if ($originalSha) {
        try {
            $origPrJson = & gh api "repos/{owner}/{repo}/commits/$originalSha/pulls" 2>$null
            if ($origPrJson) {
                $arr = $origPrJson | ConvertFrom-Json
                if ($arr -and $arr.Count -gt 0) {
                    $originalPr = $arr[0].number
                    $originalTitle = $arr[0].title
                    $originalMergedAt = $arr[0].merged_at
                }
            }
        } catch {}
        # If no PR association, the commit was pushed direct-to-main —
        # still record the title from `git show`.
        if (-not $originalTitle) {
            $origSubject = & git show --no-patch --pretty=format:%s $originalSha 2>$null
            if ($origSubject) { $originalTitle = $origSubject }
        }
    }

    $regressions += [pscustomobject]@{
        revertSha     = $revertSha
        revertSubject = $subject
        originalSha   = $originalSha
        originalPr    = $originalPr
        originalTitle = $originalTitle
        originalMergedAt = $originalMergedAt
        revertPr      = $revertPr
    }
}

# ---- Cross-reference with ledger ----
$ledgerRows = @()
if ($hasLedger) {
    $ledgerRows = @(Get-Content $ledger | ForEach-Object {
        try { $_ | ConvertFrom-Json } catch { }
    } | Where-Object { $_ })
}

$mismatches = @()    # Gate said clean, reality reverted
$confirmed = @()     # Ledger already reflects the revert
$untracked = @()     # Regression not in ledger (PR didn't go through /chatbot-iterate)
foreach ($r in $regressions) {
    if (-not $r.originalPr) { continue }
    $existing = $ledgerRows | Where-Object { $_.pr -eq $r.originalPr } | Select-Object -First 1
    if (-not $existing) {
        $untracked += $r
        continue
    }
    if ($existing.decision -eq 'merged-with-revert') {
        $confirmed += $r
        continue
    }
    if ($existing.decision -eq 'merged-clean') {
        $mismatches += [pscustomobject]@{
            pr             = $r.originalPr
            originalTitle  = $r.originalTitle
            revertSha      = $r.revertSha
            revertPr       = $r.revertPr
            existingRow    = $existing
        }
    }
}

# ---- Report ----
if ($Json) {
    $out = [ordered]@{
        windowDays = $Days
        revertsFound = $regressions.Count
        ledgerRows = $ledgerRows.Count
        gateMismatches = $mismatches.Count
        confirmed = $confirmed.Count
        untracked = $untracked.Count
        mismatches = $mismatches | Select-Object pr,originalTitle,revertSha,revertPr
        untrackedReverts = $untracked | Select-Object originalPr,originalTitle,revertSha
        applied = $Apply.IsPresent
    }
    $out | ConvertTo-Json -Depth 6 -Compress
}
else {
    Write-Host ''
    Write-Host "Loop regression watcher (window: last $Days days)" -ForegroundColor Cyan
    Write-Host ("  revert commits found:  {0}" -f $regressions.Count) -ForegroundColor Gray
    Write-Host ("  ledger rows:           {0}" -f $ledgerRows.Count) -ForegroundColor Gray
    Write-Host ("  gate ROI mismatches:   {0}" -f $mismatches.Count) -ForegroundColor $(if ($mismatches.Count) { 'Yellow' } else { 'Green' })
    Write-Host ("  already confirmed:     {0}" -f $confirmed.Count) -ForegroundColor Gray
    Write-Host ("  untracked reverts:     {0}" -f $untracked.Count) -ForegroundColor Gray
    Write-Host ''
    if ($mismatches.Count -gt 0) {
        Write-Host "Mismatches (gate said clean, reality reverted):" -ForegroundColor Yellow
        foreach ($m in $mismatches) {
            Write-Host ("  PR #{0,-5} '{1}'" -f $m.pr, ($m.originalTitle -replace '\s+',' ').Substring(0, [Math]::Min(80, $m.originalTitle.Length))) -ForegroundColor Yellow
            Write-Host ("    reverted by {0}{1}" -f $m.revertSha.Substring(0,8), $(if ($m.revertPr) { " (PR #$($m.revertPr))" } else { "" })) -ForegroundColor DarkGray
        }
        Write-Host ''
    }
}

# ---- Apply ----
if ($Apply -and $mismatches.Count -gt 0) {
    if (-not $hasLedger) {
        Write-Host "Cannot apply — gate ledger doesn't exist." -ForegroundColor Red
        exit 1
    }
    # Rewrite the ledger: each mismatching row gets decision flipped + rollbackOf set
    $updated = @()
    foreach ($row in $ledgerRows) {
        $hit = $mismatches | Where-Object { $_.pr -eq $row.pr } | Select-Object -First 1
        if ($hit) {
            $row | Add-Member -NotePropertyName decision -NotePropertyValue 'merged-with-revert' -Force
            if ($hit.revertPr) {
                $row | Add-Member -NotePropertyName rollbackOf -NotePropertyValue $hit.revertPr -Force
            }
            $existing = if ($row.notes) { "$($row.notes); " } else { "" }
            $row | Add-Member -NotePropertyName notes -NotePropertyValue ("$existing(auto-flagged by loop-regression-watcher.ps1 on $(Get-Date -Format o); revert $($hit.revertSha))") -Force
        }
        $updated += $row
    }
    # Write back
    $tmpPath = "$ledger.tmp"
    $updated | ForEach-Object {
        Add-Content -Path $tmpPath -Value ($_ | ConvertTo-Json -Depth 6 -Compress) -Encoding UTF8
    }
    Move-Item -Path $tmpPath -Destination $ledger -Force
    Write-Host "Applied $($mismatches.Count) flip(s) to gate-ledger.jsonl." -ForegroundColor Red
    Write-Host ''
}

exit 0
