#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Estimate per-PR agent token spend from commit metadata.

.DESCRIPTION
    Walks the commit range BASE_SHA..HEAD_SHA, detects AI-agent authorship via
    Co-Authored-By: trailers, and estimates token spend per agent using the
    rate card in state/quality/ai-costs/pricing.json.

    Writes:
      - JSON record to state/quality/ai-costs/<head_sha>.json (or -OutFile)
      - Markdown comment to stdout (capturable with -CommentOutFile)

    This is a heuristic. Diff size + commit count + message length are weak
    proxies for actual API spend. Cross-check provider billing dashboards.

.PARAMETER PrNumber
    The PR number (integer). Recorded in the JSON output.

.PARAMETER BaseSha
    Merge-base commit SHA. Required.

.PARAMETER HeadSha
    PR head commit SHA. Required.

.PARAMETER RepoRoot
    Path to repo root. Defaults to current directory.

.PARAMETER PricingPath
    Path to pricing.json. Defaults to <repo>/state/quality/ai-costs/pricing.json.

.PARAMETER OutFile
    Where to write the per-PR JSON. Default: state/quality/ai-costs/<head_sha>.json.

.PARAMETER CommentOutFile
    Where to write the rendered Markdown comment body. Default: stdout only.

.PARAMETER ThresholdAlertUsd
    If set and total exceeds this, set $env:GITHUB_OUTPUT pr_spend_alert=true.
    Default: 0 (disabled).

.EXAMPLE
    pwsh Scripts/pr-token-tally.ps1 -PrNumber 213 -BaseSha abc1234 -HeadSha def5678

.EXAMPLE
    # Local dry-run against an open PR
    $base = git merge-base origin/main HEAD
    $head = git rev-parse HEAD
    pwsh Scripts/pr-token-tally.ps1 -PrNumber 0 -BaseSha $base -HeadSha $head -CommentOutFile comment.md
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [int]$PrNumber,

    [Parameter(Mandatory = $true)]
    [string]$BaseSha,

    [Parameter(Mandatory = $true)]
    [string]$HeadSha,

    [string]$RepoRoot = (Resolve-Path .).Path,
    [string]$PricingPath,
    [string]$OutFile,
    [string]$CommentOutFile,
    [double]$ThresholdAlertUsd = 0
)

$ErrorActionPreference = 'Stop'

# ---- Resolve paths ----
if (-not $PricingPath) {
    $PricingPath = Join-Path $RepoRoot 'state/quality/ai-costs/pricing.json'
}
if (-not (Test-Path $PricingPath)) {
    throw "Pricing file not found: $PricingPath"
}
$pricing = Get-Content $PricingPath -Raw | ConvertFrom-Json

$outDir = Join-Path $RepoRoot 'state/quality/ai-costs'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
if (-not $OutFile) {
    $OutFile = Join-Path $outDir "$HeadSha.json"
}

# ---- Pricing staleness check ----
$asOf = $pricing._as_of
$staleWarning = $null
try {
    $asOfDate = [datetime]::Parse($asOf)
    $ageDays = [int]((Get-Date) - $asOfDate).TotalDays
    $thresholdDays = if ($pricing._staleness_warn_days) { [int]$pricing._staleness_warn_days } else { 90 }
    if ($ageDays -gt $thresholdDays) {
        $staleWarning = "Pricing snapshot is $ageDays days old (threshold: $thresholdDays). Refresh state/quality/ai-costs/pricing.json against current provider pricing pages."
        Write-Warning $staleWarning
    }
} catch {
    Write-Warning "Could not parse pricing._as_of '$asOf'; staleness check skipped."
}

# ---- Canonicalize an agent string to {id, provider} ----
function Get-AgentInfo {
    param([string]$Raw)
    $norm = $Raw.ToLowerInvariant().Trim().Trim('"', "'")
    $id = $null
    # Order matters: longest/most-specific match first.
    if ($norm -match 'mercury') { $id = 'mercury' }
    elseif ($norm -match 'antigravity') { $id = 'antigravity' }
    elseif ($norm -match 'junie') { $id = 'junie' }
    elseif ($norm -match 'codex' -or $norm -match 'chatgpt' -or $norm -match 'openai') { $id = 'codex' }
    elseif ($norm -match 'claude') { $id = 'claude' }
    elseif ($norm -match 'gemini') { $id = 'gemini' }
    elseif ($norm -match 'demerzel') { $id = 'demerzel' }
    if (-not $id) { return $null }  # Skip human authors and unknown bots.
    $providerKey = $pricing.agent_to_provider.$id
    if (-not $providerKey) { $providerKey = 'unknown' }
    return @{ id = $id; provider = $providerKey }
}

# ---- Collect per-commit metadata in range ----
$commits = git -C $RepoRoot log --pretty=format:'%H%x09%s' "$BaseSha..$HeadSha" 2>$null
if (-not $commits) {
    Write-Host "No commits between $BaseSha..$HeadSha; emitting empty report." -ForegroundColor Yellow
}

# Map: agent-id -> aggregate stats
$agentStats = @{}

foreach ($line in $commits) {
    if (-not $line) { continue }
    $parts = $line -split "`t", 2
    $sha = $parts[0]
    $subject = if ($parts.Length -gt 1) { $parts[1] } else { '' }

    # Get full body so we can find Co-Authored-By trailers.
    $body = git -C $RepoRoot log -1 --pretty=format:'%B' $sha 2>$null
    $coauthors = @()
    foreach ($bl in ($body -split "`r?`n")) {
        if ($bl -match '^Co-Authored-By:\s*(.+?)\s*<.*>$') {
            $info = Get-AgentInfo $matches[1]
            if ($info) { $coauthors += , $info }
        }
    }
    if ($coauthors.Count -eq 0) { continue }  # Human-only commit.

    # Per-commit diff stats (insertions + deletions).
    $shortstat = git -C $RepoRoot show --shortstat --format= $sha 2>$null
    $diffLines = 0
    if ($shortstat -match '(\d+)\s+insertions?\(\+\)') { $diffLines += [int]$matches[1] }
    if ($shortstat -match '(\d+)\s+deletions?\(-\)') { $diffLines += [int]$matches[1] }

    $msgChars = ($subject.Length + $body.Length)

    # Distribute this commit's stats across all detected coauthors.
    # If multiple agents pair on one commit, we split tokens evenly (best-effort attribution).
    $share = 1.0 / $coauthors.Count
    foreach ($ca in $coauthors) {
        $key = $ca.id
        if (-not $agentStats.ContainsKey($key)) {
            $agentStats[$key] = @{
                name        = $key
                provider    = $ca.provider
                commits     = 0
                diff_lines  = 0
                msg_chars   = 0
                _commit_shares = 0.0
            }
        }
        $agentStats[$key].commits      += 1   # Unique commit attribution count (rounded up by participation).
        $agentStats[$key].diff_lines   += [int]([Math]::Round($diffLines * $share))
        $agentStats[$key].msg_chars    += [int]([Math]::Round($msgChars * $share))
        $agentStats[$key]._commit_shares += $share
    }
}

# ---- Compute estimated tokens + cost per agent ----
$em = $pricing.estimation_model
$tokensPerLine = [double]$em.tokens_per_diff_line
$tokensPerCommit = [double]$em.tokens_per_commit_overhead
$tokensPerMsgChar = [double]$em.tokens_per_message_char

$agentArr = @()
$totalTokens = 0
$totalCost = 0.0

foreach ($key in ($agentStats.Keys | Sort-Object)) {
    $s = $agentStats[$key]
    $estTokens = [int]([Math]::Round(
        ($s.diff_lines * $tokensPerLine) +
        ($s._commit_shares * $tokensPerCommit) +
        ($s.msg_chars * $tokensPerMsgChar)
    ))
    $providerEntry = $pricing.providers.($s.provider)
    if (-not $providerEntry) { $providerEntry = $pricing.providers.unknown }
    $ratePer1k = [double]$providerEntry.rate_per_1k_usd
    $estCost = [Math]::Round(($estTokens / 1000.0) * $ratePer1k, 4)

    $agentArr += [pscustomobject]@{
        name         = $s.name
        provider     = $s.provider
        commits      = [int]$s.commits
        diff_lines   = [int]$s.diff_lines
        est_tokens   = $estTokens
        est_cost_usd = $estCost
    }
    $totalTokens += $estTokens
    $totalCost += $estCost
}
$totalCost = [Math]::Round($totalCost, 4)

# ---- Build output record ----
$thresholdValue = $null
if ($ThresholdAlertUsd -gt 0) { $thresholdValue = $ThresholdAlertUsd }

$record = [ordered]@{
    schema                = 'pr-token-spend-v1'
    pr_number             = $PrNumber
    head_sha              = $HeadSha
    base_sha              = $BaseSha
    run_at                = (Get-Date).ToUniversalTime().ToString('o')
    agents                = $agentArr
    total_est_tokens      = $totalTokens
    total_est_cost_usd    = $totalCost
    threshold_alert_usd   = $thresholdValue
    pricing_source        = "state/quality/ai-costs/pricing.json (as of $asOf)"
    pricing_stale_warning = $staleWarning
}

$json = $record | ConvertTo-Json -Depth 5
Set-Content -Path $OutFile -Value $json -Encoding utf8
Write-Host "Wrote $OutFile" -ForegroundColor Green

# ---- Build Markdown comment ----
function Format-Tokens {
    param([int]$N)
    if ($N -ge 1000000) { return ('{0:N1}M' -f ($N / 1000000.0)) }
    if ($N -ge 1000)    { return ('{0:N1}K' -f ($N / 1000.0)) }
    return "$N"
}
function Format-Cost {
    param([double]$Usd)
    if ($Usd -lt 0.01) { return ('${0:N4}' -f $Usd) }
    return ('${0:N2}' -f $Usd)
}

$lines = @()
$lines += "<!-- pr-token-spend -->"
$lines += "**Agent token spend (estimate)**"
$lines += ""
if ($agentArr.Count -eq 0) {
    $lines += "_No AI-agent commits detected in this PR's range (no `Co-Authored-By:` trailers matching registered agents)._"
} else {
    $lines += "| Agent | Provider | Commits | Diff lines | Est. tokens | Est. cost |"
    $lines += "|---|---|---:|---:|---:|---:|"
    foreach ($a in $agentArr) {
        $lines += "| ``$($a.name)`` | $($a.provider) | $($a.commits) | $($a.diff_lines) | ~$(Format-Tokens $a.est_tokens) | $(Format-Cost $a.est_cost_usd) |"
    }
    $totalCommits = ($agentArr | Measure-Object -Property commits -Sum).Sum
    $totalDiff    = ($agentArr | Measure-Object -Property diff_lines -Sum).Sum
    $lines += "| **Total** | | **$totalCommits** | **$totalDiff** | **~$(Format-Tokens $totalTokens)** | **$(Format-Cost $totalCost)** |"
}
$lines += ""
$lines += "<sub>Estimate based on diff size + commit metadata; **not a replacement for provider billing dashboards**. A 1-line diff can be the tail end of a 200-tool-use exploration. Values prefixed ``est_`` in the JSON record. Pricing snapshot: ``$asOf``."
if ($staleWarning) {
    $lines += "<br>:warning: $staleWarning"
}
if ($thresholdValue -and $totalCost -gt $thresholdValue) {
    $lines += "<br>:rotating_light: **Total exceeds threshold of $(Format-Cost $thresholdValue)**."
}
$lines += "<br>Source: ``.github/workflows/pr-token-spend.yml`` -> ``Scripts/pr-token-tally.ps1``. Per-PR JSON: ``state/quality/ai-costs/$HeadSha.json``.</sub>"

$comment = ($lines -join "`n")
if ($CommentOutFile) {
    Set-Content -Path $CommentOutFile -Value $comment -Encoding utf8
    Write-Host "Wrote $CommentOutFile" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host $comment
}

# Emit alert signal for the workflow to consume.
if ($thresholdValue -and $totalCost -gt $thresholdValue -and $env:GITHUB_OUTPUT) {
    Add-Content -Path $env:GITHUB_OUTPUT -Value "spend_alert=true"
    Add-Content -Path $env:GITHUB_OUTPUT -Value "total_cost_usd=$totalCost"
}

# Surface key numbers as job summary if available.
if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $comment
}
