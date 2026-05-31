#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local-runnable post-merge smoke check. Mirrors .github/workflows/post-merge-smoke.yml.

.DESCRIPTION
    Curls the three core demo URLs the user depends on, asserts HTTP 200 +
    content-marker, and optionally writes the same JSON artifact format the
    GitHub Action produces.

    Run it before pushing to verify the live demos respond. Safe to wire
    into a pre-commit hook later (out of scope for the v1 PR that added it
    — see docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md
    item #4).

.PARAMETER WriteArtifact
    Write state/quality/e2e/<timestamp>.json. Default: off (CI does this).

.PARAMETER ArtifactDir
    Where to put the JSON. Default: state/quality/e2e relative to repo root.

.PARAMETER Quiet
    Suppress per-URL output; only print a one-line summary.

.EXAMPLE
    pwsh Scripts/post-merge-smoke.ps1

.EXAMPLE
    pwsh Scripts/post-merge-smoke.ps1 -WriteArtifact

.NOTES
    Tunnel-down heuristic: if >=2 of 3 URLs come back unreachable, the
    local Cloudflare tunnel is probably down — script exits 0 with a
    warning rather than treating it as a regression.

    Exit codes:
        0  all checks passed (or tunnel-down detected)
        1  >=1 URL failed AND it's not a tunnel-down case
        2  artifact write requested but could not be created
#>

[CmdletBinding()]
param(
    [switch] $WriteArtifact,
    [string] $ArtifactDir,
    [switch] $Quiet
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Repo root resolution: this script lives in Scripts/ so root is one up.
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Resolve-Path (Join-Path $scriptDir '..')
if (-not $ArtifactDir) {
    $ArtifactDir = Join-Path $repoRoot 'state/quality/e2e'
}

# Keep this list in sync with .github/workflows/post-merge-smoke.yml.
$checks = @(
    @{ Url = 'https://demos.guitaralchemist.com/test';               Marker = 'Guitar Alchemist' }
    @{ Url = 'https://demos.guitaralchemist.com/chatbot/';           Marker = 'GA Chatbot' }
    @{ Url = 'https://demos.guitaralchemist.com/dev-data/manifest';  Marker = 'schema_version' }
)

function Invoke-SmokeCheck {
    param(
        [Parameter(Mandatory)] [string] $Url,
        [Parameter(Mandatory)] [string] $Marker
    )
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = 0
    $sizeBytes = 0
    $markerFound = $false
    $unreachable = $false
    $body = $null

    try {
        # -SkipHttpErrorCheck so we capture 4xx/5xx as a status rather than throwing.
        $resp = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 15 -SkipHttpErrorCheck -UseBasicParsing
        $status = [int] $resp.StatusCode
        $body = $resp.Content
        if ($null -ne $body) {
            $sizeBytes = [System.Text.Encoding]::UTF8.GetByteCount($body)
            if ($body.Contains($Marker)) { $markerFound = $true }
        }
    }
    catch {
        # Connection refused / timeout / DNS — treat as unreachable.
        $unreachable = $true
    }
    finally {
        $stopwatch.Stop()
    }

    $ok = ($status -eq 200) -and ($sizeBytes -gt 0) -and $markerFound

    [PSCustomObject] @{
        url                  = $Url
        status               = $status
        ok                   = $ok
        latency_ms           = [int] $stopwatch.Elapsed.TotalMilliseconds
        size_bytes           = $sizeBytes
        content_marker       = $Marker
        content_marker_found = $markerFound
        unreachable          = $unreachable
    }
}

$results = foreach ($c in $checks) {
    $r = Invoke-SmokeCheck -Url $c.Url -Marker $c.Marker
    if (-not $Quiet) {
        $tag = if ($r.ok) { 'OK' } elseif ($r.unreachable) { 'UNREACHABLE' } else { 'FAIL' }
        Write-Host ("[{0,-11}] {1}  status={2}  marker_found={3}  {4}ms" -f $tag, $r.url, $r.status, $r.content_marker_found, $r.latency_ms)
    }
    $r
}

$unreachableCount = @($results | Where-Object { $_.unreachable }).Count
$failedCount      = @($results | Where-Object { -not $_.ok }).Count
$tunnelDown       = $unreachableCount -ge 2
$allPassed        = $failedCount -eq 0

# Try to capture head SHA for the artifact; not fatal if git is missing.
$headSha = ''
try {
    $headSha = (& git -C $repoRoot rev-parse HEAD 2>$null).Trim()
} catch { $headSha = '' }

$doc = [ordered] @{
    schema            = 'post-merge-smoke-v1'
    run_at            = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    head_sha          = $headSha
    all_passed        = $allPassed
    tunnel_down       = $tunnelDown
    unreachable_count = $unreachableCount
    checks            = @($results)
}

if ($WriteArtifact) {
    try {
        New-Item -ItemType Directory -Path $ArtifactDir -Force | Out-Null
        $ts = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssZ")
        $outFile = Join-Path $ArtifactDir "$ts.json"
        $doc | ConvertTo-Json -Depth 8 | Set-Content -Path $outFile -Encoding utf8
        if (-not $Quiet) { Write-Host "wrote $outFile" }
    }
    catch {
        Write-Error "Failed to write artifact: $_"
        exit 2
    }
}

# Summary line for piped-in callers.
$summary = if ($tunnelDown) {
    "tunnel-down ({0}/{1} unreachable) - exit 0 (transient infra)" -f $unreachableCount, $results.Count
} elseif ($allPassed) {
    "all {0} URLs OK" -f $results.Count
} else {
    "regression: {0}/{1} URLs failed" -f $failedCount, $results.Count
}
Write-Host "post-merge-smoke: $summary"

if ($tunnelDown) {
    Write-Warning "Tunnel-down detected; not flagging as regression."
    exit 0
}

if (-not $allPassed) {
    exit 1
}

exit 0
