# compute-pr-eta-baseline.ps1 — write state/quality/pr-eta-baseline.json
#
# Computes the median and p90 time-to-merge per PR type (chore / feat / fix
# / docs / other) over the last N days. The Vite /dev-data/in-flight
# middleware reads this file to compute an ETA for each open PR:
#
#   eta_minutes  = max(median_for_type - age_minutes, 0)
#   p90_minutes  = (used only as a long-tail tooltip)
#
# Schema (pr-eta-baseline-v1):
#   {
#     "_schema":      "pr-eta-baseline-v1",
#     "computed_at":  "<ISO-8601>",
#     "window_days":  30,
#     "by_type": {
#       "chore": { "sample_n": 14, "median_minutes": 18, "p90_minutes": 32 },
#       "feat":  { "sample_n": 23, "median_minutes": 42, "p90_minutes": 87 },
#       "fix":   { "sample_n":  9, "median_minutes": 25, "p90_minutes": 41 },
#       "docs":  { "sample_n":  5, "median_minutes": 12, "p90_minutes": 18 },
#       "other": { "sample_n":  2, "median_minutes": 30, "p90_minutes": 45 }
#     }
#   }
#
# PR type comes from the conventional-commit prefix on the title:
# `chore(quality): ...` → chore; `feat(harness): ...` → feat; etc.
#
# Run weekly (cron) or on-demand. Idempotent — same window = same output.
# Falls back gracefully when gh is missing or the repo has no merged PRs
# in the window (writes a stub with empty by_type{} so the dashboard
# still gets a parsable file).

[CmdletBinding()]
param(
    [int]$WindowDays = 30,
    [string]$OutputPath = "state/quality/pr-eta-baseline.json"
)

$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    $r = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $r) {
        throw "Not in a git repo (or git missing). Run from inside the ga checkout."
    }
    return $r.Trim()
}

function Get-PrType([string]$title) {
    if (-not $title) { return 'other' }
    # Conventional-commit prefix: word + optional (scope) + colon
    if ($title -match '^(?<type>[a-z]+)(\([^)]*\))?\s*:') {
        $t = $Matches['type'].ToLower()
        switch ($t) {
            'chore'   { return 'chore' }
            'feat'    { return 'feat' }
            'feature' { return 'feat' }
            'fix'     { return 'fix' }
            'bugfix'  { return 'fix' }
            'docs'    { return 'docs' }
            'doc'     { return 'docs' }
            default   { return 'other' }
        }
    }
    return 'other'
}

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$P
    )
    if (-not $Values -or $Values.Count -eq 0) { return 0 }
    $sorted = $Values | Sort-Object
    if ($sorted.Count -eq 1) { return [int]$sorted[0] }
    # Linear interpolation between closest ranks
    $rank = $P * ($sorted.Count - 1)
    $lo = [Math]::Floor($rank)
    $hi = [Math]::Ceiling($rank)
    if ($lo -eq $hi) { return [int]$sorted[$lo] }
    $frac = $rank - $lo
    return [int]([Math]::Round($sorted[$lo] * (1 - $frac) + $sorted[$hi] * $frac))
}

$repoRoot = Get-RepoRoot
$outFull  = Join-Path $repoRoot $OutputPath
$outDir   = Split-Path -Parent $outFull
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

# Default empty payload — written when gh is missing or 0 PRs in window.
$nowIso = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
$payload = [ordered]@{
    '_schema'     = 'pr-eta-baseline-v1'
    computed_at   = $nowIso
    window_days   = $WindowDays
    by_type       = [ordered]@{}
}

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    Write-Warning "gh CLI not found — writing stub baseline with empty by_type."
    $payload | ConvertTo-Json -Depth 5 | Set-Content -Path $outFull -Encoding UTF8
    Write-Host "Wrote stub: $outFull"
    return
}

# Pull a generous sample so the per-type buckets have enough data points.
# 250 is below gh's default cap and well above what we expect to need.
$rawJson = & gh pr list `
    --state merged `
    --limit 250 `
    --search "merged:>=$(((Get-Date).AddDays(-$WindowDays)).ToString('yyyy-MM-dd'))" `
    --json number,title,createdAt,mergedAt 2>$null

if ($LASTEXITCODE -ne 0 -or -not $rawJson) {
    Write-Warning "gh pr list returned nothing — writing stub baseline."
    $payload | ConvertTo-Json -Depth 5 | Set-Content -Path $outFull -Encoding UTF8
    Write-Host "Wrote stub: $outFull"
    return
}

$prs = $rawJson | ConvertFrom-Json
if (-not $prs -or $prs.Count -eq 0) {
    Write-Warning "No merged PRs in last $WindowDays days — writing stub baseline."
    $payload | ConvertTo-Json -Depth 5 | Set-Content -Path $outFull -Encoding UTF8
    Write-Host "Wrote stub: $outFull"
    return
}

# Bucket by PR type
$buckets = @{}
foreach ($pr in $prs) {
    if (-not $pr.createdAt -or -not $pr.mergedAt) { continue }
    try {
        $created = [datetime]::Parse($pr.createdAt)
        $merged  = [datetime]::Parse($pr.mergedAt)
    } catch { continue }
    $delta = ($merged - $created).TotalMinutes
    if ($delta -le 0) { continue }
    $type = Get-PrType $pr.title
    if (-not $buckets.ContainsKey($type)) { $buckets[$type] = @() }
    $buckets[$type] += [double]$delta
}

# Compute median + p90 per bucket; emit in stable key order
foreach ($type in @('chore', 'feat', 'fix', 'docs', 'other')) {
    if (-not $buckets.ContainsKey($type)) { continue }
    $samples = $buckets[$type]
    if ($samples.Count -eq 0) { continue }
    $payload.by_type[$type] = [ordered]@{
        sample_n       = $samples.Count
        median_minutes = Get-Percentile -Values $samples -P 0.5
        p90_minutes    = Get-Percentile -Values $samples -P 0.9
    }
}

$json = $payload | ConvertTo-Json -Depth 5
Set-Content -Path $outFull -Value $json -Encoding UTF8

$totalSamples = 0
foreach ($k in $payload.by_type.Keys) { $totalSamples += $payload.by_type[$k].sample_n }
Write-Host "Wrote $outFull"
Write-Host "  Window: last $WindowDays days, $totalSamples PRs across $($payload.by_type.Count) type bucket(s)"
foreach ($k in $payload.by_type.Keys) {
    $b = $payload.by_type[$k]
    Write-Host ("  {0,-6}  n={1,-3}  median={2,4} min  p90={3,4} min" -f $k, $b.sample_n, $b.median_minutes, $b.p90_minutes)
}
