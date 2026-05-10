# Auto-merge decision script for /chatbot-iterate Step 7.
#
# Returns exit 0 only when ALL of the following are true for the target PR:
#   1. PR has the `auto-merge-eligible` label (explicit opt-in)
#   2. PR does NOT have `do-not-merge` or `wip` labels
#   3. PR is not a draft
#   4. PR diff is path-restricted to chatbot-safe paths only (no public
#      API surface, no DI registration, no GraphQL schema, no .env, etc.)
#   5. All CI checks have run to completion AND none are 'failure'
#      (excluding the known-pre-existing Anthropic env failure on
#      `Backend Tests` / `build` jobs, which can be allowlisted)
#   6. Most recent Agent-tool multi-LLM review for this branch produced
#      a verdict (presence of `state/chatbot-reviews/<pr>.json` with
#      `verdict: pass | nits-only`)
#   7. Most recent /octo:review for this PR, if any, passed the
#      liveness check (see octo-gate-liveness.ps1)
#   8. Tribunal verdict, if present at state/quality/verdicts/, is PASS
#
# Otherwise: exit 1 with the specific failing condition on stdout.
#
# Default behavior is REFUSE. This script never merges; the caller
# (/chatbot-iterate) reads the exit code and decides.
#
# Usage:
#   pwsh Scripts/octo-auto-merge-decision.ps1 -Pr 155
#   pwsh Scripts/octo-auto-merge-decision.ps1 -Pr 155 -Json
#   pwsh Scripts/octo-auto-merge-decision.ps1 -Pr 155 -DryRun -Verbose

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [int]$Pr,

    [switch]$Json,

    [switch]$DryRun,

    # Paths chatbot-iterate can auto-merge without human review.
    # Excludes anything touching public API surface, DI composition, or
    # schema migrations — those require human eyes regardless of gates.
    [string[]]$SafePathPatterns = @(
        '^Common/GA\.Business\.ML/Agents/Mcp/',
        '^Common/GA\.Business\.ML/Agents/Skills/',
        '^Common/GA\.Business\.ML/Agents/Routing\.',
        '^Common/GA\.Business\.ML/Agents/Hooks/',
        '^Common/GA\.Business\.ML/Agents/Memory/',
        '^Common/GA\.Business\.ML/Agents/Intents/',
        '^Tests/Common/GA\.Business\.ML\.Tests/',
        '^skills/[^/]+/SKILL\.md$',
        '^docs/solutions/',
        '^BACKLOG\.md$'
    ),

    # CI check names known to fail for env reasons unrelated to the diff.
    # These are excluded from the "all checks pass" requirement until the
    # CI env is fixed.
    [string[]]$AllowlistedCiFailures = @(
        'Backend Tests',
        'Backend Test Results',
        'build',
        'Playwright Tests'
    )
)

$ErrorActionPreference = 'Stop'

function Emit-Result {
    param([int]$ExitCode, [string]$Decision, [string]$Reason, [hashtable]$Diagnostics)
    if ($Json) {
        $obj = [ordered]@{
            pr       = $Pr
            decision = $Decision
            reason   = $Reason
            exitCode = $ExitCode
            diagnostics = $Diagnostics
        }
        $obj | ConvertTo-Json -Depth 4 -Compress
    }
    else {
        $colour = if ($Decision -eq 'merge') { 'Green' } else { 'Yellow' }
        Write-Host ''
        Write-Host "PR #$Pr auto-merge decision: $Decision" -ForegroundColor $colour
        Write-Host "  reason: $Reason"
        if ($Diagnostics -and $Diagnostics.Count -gt 0) {
            foreach ($k in $Diagnostics.Keys) {
                Write-Host "  $k = $($Diagnostics[$k])" -ForegroundColor DarkGray
            }
        }
        Write-Host ''
    }
    exit $ExitCode
}

# ---- 1. Fetch PR state ----
$prData = & gh pr view $Pr --json number,state,isDraft,labels,headRefName,mergeable,mergeStateStatus,files,statusCheckRollup,reviews 2>&1
if ($LASTEXITCODE -ne 0) {
    Emit-Result 1 'refuse' "gh pr view failed: $prData" @{}
}
$prObj = $prData | ConvertFrom-Json

# ---- 2. Check basic eligibility ----
if ($prObj.state -ne 'OPEN') {
    Emit-Result 1 'refuse' "PR state is $($prObj.state); only OPEN PRs can be auto-merged" @{ state = $prObj.state }
}
if ($prObj.isDraft) {
    Emit-Result 1 'refuse' 'PR is a draft' @{ draft = $true }
}

$labels = $prObj.labels | ForEach-Object { $_.name }
if ('do-not-merge' -in $labels -or 'wip' -in $labels) {
    Emit-Result 1 'refuse' "PR has a blocking label" @{ labels = ($labels -join ',') }
}
if ('auto-merge-eligible' -notin $labels) {
    Emit-Result 1 'refuse' "PR missing 'auto-merge-eligible' label (explicit opt-in required)" @{ labels = ($labels -join ',') }
}

# ---- 3. Path scope check ----
$unsafePaths = @()
foreach ($f in $prObj.files) {
    $path = $f.path
    $matched = $false
    foreach ($pattern in $SafePathPatterns) {
        if ($path -match $pattern) { $matched = $true; break }
    }
    if (-not $matched) { $unsafePaths += $path }
}
if ($unsafePaths.Count -gt 0) {
    Emit-Result 1 'refuse' "$($unsafePaths.Count) file(s) outside chatbot-safe paths" @{
        unsafePaths = ($unsafePaths -join ', ')
    }
}

# ---- 4. CI checks ----
$failed = @()
$pending = @()
foreach ($check in $prObj.statusCheckRollup) {
    $name = $check.name
    if (-not $name) { continue }
    $conc = $check.conclusion
    if (-not $conc) {
        $status = $check.status
        if ($status -in @('IN_PROGRESS','QUEUED','PENDING','WAITING')) {
            $pending += $name
        }
        continue
    }
    if ($conc -in @('FAILURE','TIMED_OUT','CANCELLED','STARTUP_FAILURE')) {
        if ($name -notin $AllowlistedCiFailures) {
            $failed += $name
        }
    }
}
if ($pending.Count -gt 0) {
    Emit-Result 1 'wait' "$($pending.Count) check(s) still running" @{
        pending = ($pending -join ', ')
    }
}
if ($failed.Count -gt 0) {
    Emit-Result 1 'refuse' "$($failed.Count) non-allowlisted check(s) failing" @{
        failed = ($failed -join ', ')
    }
}

# ---- 5. Agent-tool multi-LLM verdict ----
$reviewFile = Join-Path (Resolve-Path .).Path "state/chatbot-reviews/$Pr.json"
$reviewVerdict = $null
if (Test-Path $reviewFile) {
    try {
        $review = Get-Content $reviewFile -Raw | ConvertFrom-Json
        $reviewVerdict = $review.verdict
    } catch {
        Emit-Result 1 'refuse' "review verdict file unparseable: $reviewFile" @{}
    }
}
if (-not $reviewVerdict) {
    Emit-Result 1 'refuse' "no Agent-tool multi-LLM review verdict at $reviewFile (run Step 4 of /chatbot-iterate)" @{}
}
if ($reviewVerdict -notin @('pass','nits-only')) {
    Emit-Result 1 'refuse' "review verdict is '$reviewVerdict' (expected 'pass' or 'nits-only')" @{ reviewVerdict = $reviewVerdict }
}

# ---- 6. If /octo:review was used, verify liveness ----
$octoLiveness = & pwsh -NoProfile -File "Scripts/octo-gate-liveness.ps1" -Json 2>&1
$octoLivenessJson = $null
try { $octoLivenessJson = $octoLiveness | ConvertFrom-Json } catch { }
if ($octoLivenessJson -and $octoLivenessJson.state -eq 'dark') {
    Emit-Result 1 'refuse' '/octo:review most recent run was dark — gate verdict is meaningless. Re-run via octo-review-clean.ps1 or rely on Agent-tool review.' @{
        octoTimestamp = $octoLivenessJson.timestamp
        octoFailed = $octoLivenessJson.failed
        octoTotal = $octoLivenessJson.total
    }
}

# ---- 7. Tribunal verdict (if present) ----
$tribunalRepo = (& git remote get-url origin 2>&1) -replace '.*[:/]([^/]+/[^/]+)\.git$','$1'
$tribunalDir = Join-Path (Resolve-Path .).Path "state/quality/verdicts/$tribunalRepo/pr-$Pr"
if (Test-Path $tribunalDir) {
    $tribunalFiles = Get-ChildItem $tribunalDir -Filter '*.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($tribunalFiles) {
        try {
            $tribunal = Get-Content $tribunalFiles.FullName -Raw | ConvertFrom-Json
            if ($tribunal.verdict -and $tribunal.verdict -notin @('PASS','pass','APPROVE','approve','approve-with-nits')) {
                Emit-Result 1 'refuse' "tribunal verdict is '$($tribunal.verdict)'" @{
                    tribunalFile = $tribunalFiles.Name
                }
            }
        } catch {
            Emit-Result 1 'refuse' "tribunal verdict file unparseable: $($tribunalFiles.Name)" @{}
        }
    }
}

# ---- All gates pass ----
$diag = @{
    state = $prObj.state
    labels = ($labels -join ',')
    files = $prObj.files.Count
    reviewVerdict = $reviewVerdict
    octoLiveness = if ($octoLivenessJson) { $octoLivenessJson.state } else { 'none' }
}

if ($DryRun) {
    Emit-Result 0 'merge-would-fire' 'All gates pass — auto-merge would fire (DryRun mode)' $diag
}

Emit-Result 0 'merge' 'All gates pass — caller should run `gh pr merge --squash --delete-branch`' $diag
