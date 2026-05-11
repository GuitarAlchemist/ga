# Tier 1 Demerzel integration — read-only loop-status emitter.
#
# Aggregates GA's /chatbot-iterate state into a single
# state/governance/ga-loop-status.json that Demerzel governance can
# poll. Schema: docs/schemas/ga-loop-status.schema.json.
#
# Sources:
#   - BACKLOG.md (Chatbot Track section)
#   - gh pr list --search "head:chatbot/" --state open
#   - state/chatbot-reviews/<pr>.json (Agent-tool review verdicts)
#   - state/quality/gate-ledger.jsonl (post-merge cohort data)
#   - state/quality/verdicts/<repo>/pr-<n>/*.json (Demerzel tribunal verdicts when present)
#   - state/.loop-halted (kill switch state)
#
# Output: state/governance/ga-loop-status.json (atomically overwritten).
#
# This is the Tier 1 integration: GA writes, Demerzel reads. Tier 2 adds
# the reverse direction (Demerzel writes governance directives that
# /chatbot-iterate Step 0 reads).
#
# Usage:
#   pwsh Scripts/project-sync.ps1                # emit snapshot
#   pwsh Scripts/project-sync.ps1 -Verbose       # with progress
#   pwsh Scripts/project-sync.ps1 -DryRun        # print to stdout, don't write
#   pwsh Scripts/project-sync.ps1 -LastN 20      # include more ledger rows
#
# Safe to run on a cron: every minute is fine, the script is read-only
# against the repo and atomic on the output.

[CmdletBinding()]
param(
    [int]$LastN = 10,
    [switch]$DryRun,
    [string]$RepoRoot = (Resolve-Path .).Path
)

$ErrorActionPreference = 'Stop'

function Read-JsonOrNull {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    try { return Get-Content $Path -Raw | ConvertFrom-Json } catch { return $null }
}

function Parse-ChatbotTrack {
    param([string]$BacklogPath)
    if (-not (Test-Path $BacklogPath)) { return @() }
    $content = Get-Content $BacklogPath -Raw

    # Find the "Chatbot Track" section between its heading and the next H2.
    $match = [regex]::Match($content,
        '(?s)## Chatbot Track.*?(?=\n## (?!#)|\z)')
    if (-not $match.Success) { return @() }
    $section = $match.Value

    # Match each item: - [ ] **`slug`** [status] — summary
    $items = @()
    foreach ($lineMatch in [regex]::Matches($section,
        '(?m)^- \[(?<done>[ x])\] \*\*`(?<slug>[^`]+)`\*\* \[(?<status>[^\]]+)\][^—]*— (?<summary>[^\r\n]+)')) {
        $statusRaw = $lineMatch.Groups['status'].Value.Trim()
        # Normalise: "blocked: ..." → "blocked"
        $status = ($statusRaw -split ':')[0].Trim().ToLowerInvariant()
        if ($lineMatch.Groups['done'].Value -eq 'x') { $status = 'shipped' }
        # Determine priority from the surrounding H3 (P0 / P1 / P2 / Scheduled / Parked)
        $beforeText = $section.Substring(0, $lineMatch.Index)
        $priority = if ($beforeText -match '###\s*P0[^#]*$') { 'P0' }
                    elseif ($beforeText -match '###\s*P1[^#]*$') { 'P1' }
                    elseif ($beforeText -match '###\s*P2[^#]*$') { 'P2' }
                    else { 'P2' }  # scheduled / parked default to P2 bucket
        # Tribunal-required if summary mentions "Tribunal: REQUIRED"
        $tribunalRequired = ($lineMatch.Value -match 'Tribunal:\s*REQUIRED')
        $items += [ordered]@{
            slug             = $lineMatch.Groups['slug'].Value
            priority         = $priority
            status           = $status
            summary          = ($lineMatch.Groups['summary'].Value.Trim() -replace '\s+', ' ')
            tribunalRequired = $tribunalRequired
        }
    }
    return $items
}

function Get-OpenChatbotPRs {
    param([string]$RepoRoot)
    try {
        $raw = & gh pr list --state open --json number,title,headRefName,createdAt,labels,mergeable,statusCheckRollup --limit 50 2>$null
        if (-not $raw) { return @() }
        $all = $raw | ConvertFrom-Json
    } catch { return @() }

    $result = @()
    foreach ($pr in $all) {
        $labels = @($pr.labels | ForEach-Object { $_.name })
        # Heuristic: include if branch is chatbot/* OR has auto-merge-eligible label
        $isChatbot = $pr.headRefName -like 'chatbot/*' -or ($labels -contains 'auto-merge-eligible')
        if (-not $isChatbot) { continue }

        # CI tally
        $passed = 0; $failed = 0; $pending = 0
        $failedNames = @()
        foreach ($c in $pr.statusCheckRollup) {
            $conc = if ($c.conclusion) { $c.conclusion } else { $c.status }
            switch ($conc) {
                'SUCCESS'         { $passed++ }
                'NEUTRAL'         { $passed++ }
                'FAILURE'         { $failed++; if ($c.name) { $failedNames += $c.name } }
                'TIMED_OUT'       { $failed++ }
                'CANCELLED'       { $failed++ }
                'STARTUP_FAILURE' { $failed++ }
                'IN_PROGRESS'     { $pending++ }
                'QUEUED'          { $pending++ }
                'PENDING'         { $pending++ }
                'WAITING'         { $pending++ }
            }
        }

        # Review verdict (if produced by chatbot-review-write.ps1)
        $verdictPath = Join-Path $RepoRoot "state/chatbot-reviews/$($pr.number).json"
        $verdict = $null
        if (Test-Path $verdictPath) {
            try {
                $v = Get-Content $verdictPath -Raw | ConvertFrom-Json
                $verdict = [ordered]@{
                    verdict    = $v.verdict
                    mechanism  = $v.mechanism
                    reviewedAt = $v.reviewedAt
                }
            } catch { }
        }

        $result += [ordered]@{
            number        = $pr.number
            title         = $pr.title
            branch        = $pr.headRefName
            createdAt     = $pr.createdAt
            labels        = $labels
            mergeable     = $pr.mergeable
            ci            = [ordered]@{ passed=$passed; failed=$failed; pending=$pending; failedNames=@($failedNames | Select-Object -Unique) }
            reviewVerdict = $verdict
        }
    }
    return $result
}

function Read-LedgerSummary {
    param([string]$RepoRoot, [int]$LastN)
    $ledger = Join-Path $RepoRoot 'state/quality/gate-ledger.jsonl'
    if (-not (Test-Path $ledger)) {
        return [ordered]@{
            totalRows = 0
            lastNRows = @()
            decisionCounts = [ordered]@{
                'merged-clean' = 0
                'merged-with-followup' = 0
                'merged-with-revert' = 0
                'open' = 0
                'abandoned' = 0
            }
        }
    }
    $rows = @(Get-Content $ledger | ForEach-Object { try { $_ | ConvertFrom-Json } catch { } } | Where-Object { $_ })
    $counts = @{}
    foreach ($d in @('merged-clean','merged-with-followup','merged-with-revert','open','abandoned')) { $counts[$d] = 0 }
    foreach ($r in $rows) { if ($r.decision -and $counts.ContainsKey($r.decision)) { $counts[$r.decision]++ } }
    return [ordered]@{
        totalRows = $rows.Count
        lastNRows = @($rows | Select-Object -Last $LastN)
        decisionCounts = [ordered]@{
            'merged-clean'         = [int]$counts['merged-clean']
            'merged-with-followup' = [int]$counts['merged-with-followup']
            'merged-with-revert'   = [int]$counts['merged-with-revert']
            'open'                 = [int]$counts['open']
            'abandoned'            = [int]$counts['abandoned']
        }
    }
}

function Read-TribunalVerdicts {
    param([string]$RepoRoot, $OpenPRs, $TribunalRequiredSlugs)
    $owner = ''; $repo = ''
    try {
        $remote = & git remote get-url origin 2>$null
        if ($remote -match '[:/]([^/]+)/([^/]+?)(?:\.git)?$') {
            $owner = $Matches[1]; $repo = $Matches[2]
        }
    } catch { }
    $verdictsDir = Join-Path $RepoRoot "state/quality/verdicts/$owner/$repo"

    $completed = @()
    if (Test-Path $verdictsDir) {
        Get-ChildItem -Path $verdictsDir -Directory -Filter 'pr-*' -ErrorAction SilentlyContinue | ForEach-Object {
            $prNum = ($_.Name -replace '^pr-','')
            $latest = Get-ChildItem $_.FullName -Filter '*.json' -ErrorAction SilentlyContinue |
                      Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if ($latest) {
                $v = Read-JsonOrNull $latest.FullName
                if ($v) {
                    $completed += [ordered]@{
                        pr          = [int]$prNum
                        verdict     = ($v.verdict ?? 'unknown')
                        verdictId   = ($v.verdictId ?? '')
                        completedAt = ($v.completedAt ?? $latest.LastWriteTimeUtc.ToString('o'))
                    }
                }
            }
        }
    }

    $completedPrs = @($completed | ForEach-Object { $_.pr })
    $pending = @()
    foreach ($pr in $OpenPRs) {
        # Heuristic: a PR is "pending tribunal" if its branch matches a
        # tribunal-required slug from the Chatbot Track AND no verdict
        # has been written yet.
        $matchesTrack = $false
        foreach ($slug in $TribunalRequiredSlugs) {
            if ($pr.branch -like "*$slug*") { $matchesTrack = $true; break }
        }
        if ($matchesTrack -and ($pr.number -notin $completedPrs)) {
            $ageDays = [Math]::Round(((Get-Date) - [datetime]$pr.createdAt).TotalDays, 2)
            $pending += [ordered]@{
                pr      = [int]$pr.number
                branch  = $pr.branch
                ageDays = $ageDays
            }
        }
    }

    return [ordered]@{ pendingTribunal = $pending; completedTribunal = $completed }
}

# ---- Gather state ----

$repoInfo = [ordered]@{
    owner   = ''
    name    = ''
    branch  = ($null)
    headSha = ($null)
}
try {
    $remote = & git remote get-url origin 2>$null
    if ($remote -match '[:/]([^/]+)/([^/]+?)(?:\.git)?$') {
        $repoInfo.owner = $Matches[1]
        $repoInfo.name  = $Matches[2]
    }
    $repoInfo.branch  = (& git branch --show-current 2>$null)
    $repoInfo.headSha = (& git rev-parse HEAD 2>$null)
} catch { }

$loopState = [ordered]@{
    halted            = $false
    killswitchReason  = $null
}
$sentinel = Join-Path $RepoRoot 'state/.loop-halted'
if (Test-Path $sentinel) {
    $loopState.halted = $true
    $reasonLine = Get-Content $sentinel | Where-Object { $_ -like 'REASON:*' } | Select-Object -First 1
    if ($reasonLine) { $loopState.killswitchReason = ($reasonLine -replace '^REASON:\s*','') }
}

$track = @(Parse-ChatbotTrack -BacklogPath (Join-Path $RepoRoot 'BACKLOG.md'))
$openPRs = @(Get-OpenChatbotPRs -RepoRoot $RepoRoot)
$ledgerSummary = Read-LedgerSummary -RepoRoot $RepoRoot -LastN $LastN
$tribunalRequiredSlugs = @($track | Where-Object { $_.tribunalRequired } | ForEach-Object { $_.slug })
$tribunal = Read-TribunalVerdicts -RepoRoot $RepoRoot -OpenPRs $openPRs -TribunalRequiredSlugs $tribunalRequiredSlugs

# ---- Assemble ----

$snapshot = [ordered]@{
    schemaVersion = '0.1'
    emittedAt     = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ' -AsUTC)
    repo          = $repoInfo
    loop          = $loopState
    backlog       = [ordered]@{ chatbotTrack = $track }
    openPRs       = $openPRs
    ledger        = $ledgerSummary
    verdicts      = $tribunal
}

$json = $snapshot | ConvertTo-Json -Depth 8

if ($DryRun) {
    Write-Output $json
    exit 0
}

$outDir  = Join-Path $RepoRoot 'state/governance'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$outPath = Join-Path $outDir 'ga-loop-status.json'
$tmpPath = "$outPath.tmp"
Set-Content -Path $tmpPath -Value $json -Encoding UTF8
Move-Item -Path $tmpPath -Destination $outPath -Force

Write-Host ("Loop status emitted: {0}" -f $outPath) -ForegroundColor Green
Write-Host ("  track items: {0}; open PRs: {1}; ledger rows: {2}; pending tribunal: {3}; completed tribunal: {4}; halted: {5}" -f `
    $track.Count, $openPRs.Count, $ledgerSummary.totalRows, $tribunal.pendingTribunal.Count, $tribunal.completedTribunal.Count, $loopState.halted) -ForegroundColor DarkGray
