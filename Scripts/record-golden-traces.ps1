# record-golden-traces.ps1 — capture successful chatbot runs as golden traces
#
# Hits the chatbot HTTP endpoint with each prompt from the corpus N times,
# saves successful traces (no orchestration.fallback step) to disk under
# state/quality/chatbot-qa/golden-traces/<prompt-slug>/run-<N>.json.
#
# Downstream consumers (next session): dominator extractor + CI diff against
# canonical trace shape. v1 is the recorder only.
#
# Usage:
#   pwsh Scripts/record-golden-traces.ps1                          # 1 run/prompt against localhost:5252
#   pwsh Scripts/record-golden-traces.ps1 -RunsPerPrompt 3
#   pwsh Scripts/record-golden-traces.ps1 -ChatbotUrl https://demos.guitaralchemist.com
#   pwsh Scripts/record-golden-traces.ps1 -Filter "modes"          # only categories containing "modes"
#
# Output structure:
#   state/quality/chatbot-qa/golden-traces/
#     <prompt-slug>/
#       run-1.json
#       run-2.json
#       _meta.json   ← prompt + category + recording metadata

[CmdletBinding()]
param(
    [string]$ChatbotUrl     = "http://localhost:5252",
    [int]$RunsPerPrompt     = 1,
    [int]$TimeoutSeconds    = 60,
    [string]$Filter         = "",
    [string]$CorpusPath     = "Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml",
    [string]$OutputDir      = "state/quality/chatbot-qa/golden-traces",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$corpus   = Join-Path $repoRoot $CorpusPath
$outRoot  = Join-Path $repoRoot $OutputDir

if (-not (Test-Path $corpus)) {
    Write-Error "Corpus not found at $corpus"
    exit 1
}

# ── Parse prompts.yaml without a YAML library ──────────────────────────
# Corpus format is strictly line-based; we only need (prompt, category, skip).
# This is lossy by design — when we need the full schema we'll port the
# C# parser in PromptCorpusTests.cs. For trace recording the prompt string
# and category bucket are sufficient.

function Parse-Corpus {
    param([string]$Path)

    $entries = @()
    $current = $null
    foreach ($line in Get-Content -LiteralPath $Path) {
        # Strip comments
        $stripped = $line -replace '#.*$', ''

        # New prompt entry
        if ($stripped -match '^\s*-\s+prompt:\s*"(.+)"\s*$') {
            if ($null -ne $current -and -not [string]::IsNullOrWhiteSpace($current.prompt)) {
                $entries += [pscustomobject]$current
            }
            $current = @{
                prompt   = $matches[1]
                category = "uncategorized"
                skip     = $false
            }
            continue
        }

        if ($null -eq $current) { continue }

        if ($stripped -match '^\s+category:\s*"?([^"\r\n]+?)"?\s*$') {
            $current.category = $matches[1].Trim()
        }
        elseif ($stripped -match '^\s+skip:\s*(true|false)\s*$') {
            $current.skip = $matches[1] -eq 'true'
        }
    }

    if ($null -ne $current -and -not [string]::IsNullOrWhiteSpace($current.prompt)) {
        $entries += [pscustomobject]$current
    }

    return $entries
}

function ConvertTo-Slug {
    param([string]$Text)
    $s = $Text.ToLowerInvariant() -replace "[^a-z0-9]+", "-"
    $s = $s.Trim('-')
    if ($s.Length -gt 64) { $s = $s.Substring(0, 64).TrimEnd('-') }
    return $s
}

function Trace-HasFallback {
    param($Trace)
    if ($null -eq $Trace -or $null -eq $Trace.steps) { return $false }
    foreach ($step in $Trace.steps) {
        if ($step.name -eq "orchestration.fallback" -or $step.name -eq "gen_ai.chat.fallback") {
            return $true
        }
    }
    return $false
}

# ── Load corpus and filter ─────────────────────────────────────────────
$all = Parse-Corpus -Path $corpus
$active = @($all | Where-Object { -not $_.skip })
if ($Filter) {
    $active = @($active | Where-Object {
        $_.category -like "*$Filter*" -or $_.prompt -like "*$Filter*"
    })
}

Write-Host "─── Golden trace recorder ───" -ForegroundColor Cyan
Write-Host "Corpus       : $corpus"
Write-Host "Chatbot URL  : $ChatbotUrl"
Write-Host "Output dir   : $outRoot"
Write-Host "Total prompts: $($all.Count) ($($all.Count - $active.Count) skipped/filtered)"
Write-Host "Active       : $($active.Count) × $RunsPerPrompt run(s) = $($active.Count * $RunsPerPrompt) HTTP calls"

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run — first 5 active prompts:" -ForegroundColor Yellow
    $active | Select-Object -First 5 | ForEach-Object {
        Write-Host "  [$($_.category)] $($_.prompt)"
    }
    return
}

if (-not (Test-Path $outRoot)) {
    New-Item -ItemType Directory -Path $outRoot -Force | Out-Null
}

# ── Record loop ────────────────────────────────────────────────────────
$recorded = 0
$skipped  = 0
$errored  = 0
$sw       = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($entry in $active) {
    $slug    = ConvertTo-Slug -Text $entry.prompt
    $promptDir = Join-Path $outRoot $slug

    for ($run = 1; $run -le $RunsPerPrompt; $run++) {
        $sessionId = "golden-trace-$slug-run$run-$(Get-Date -Format yyyyMMddHHmmss)"
        $body = @{
            message   = $entry.prompt
            sessionId = $sessionId
        } | ConvertTo-Json -Compress

        try {
            $resp = Invoke-RestMethod `
                -Method Post `
                -Uri "$ChatbotUrl/api/chatbot/chat" `
                -ContentType "application/json" `
                -Body $body `
                -TimeoutSec $TimeoutSeconds
        }
        catch {
            $errored++
            Write-Host "  ✗ [$($entry.category)] $($entry.prompt) (run $run): $($_.Exception.Message)" -ForegroundColor Red
            continue
        }

        if (Trace-HasFallback -Trace $resp.trace) {
            $skipped++
            Write-Host "  ↩ [$($entry.category)] $($entry.prompt) (run $run): fell back, not golden" -ForegroundColor DarkYellow
            continue
        }

        if (-not (Test-Path $promptDir)) {
            New-Item -ItemType Directory -Path $promptDir -Force | Out-Null
        }

        $artifact = [ordered]@{
            schemaVersion = 1
            recordedAt    = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC)
            promptId      = $slug
            prompt        = $entry.prompt
            category      = $entry.category
            runIndex      = $run
            response      = $resp
        }

        $runPath  = Join-Path $promptDir "run-$run.json"
        $artifact | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $runPath -Encoding UTF8

        # Write or refresh _meta.json with prompt + category once per dir
        $metaPath = Join-Path $promptDir "_meta.json"
        if (-not (Test-Path $metaPath)) {
            ([ordered]@{
                promptId         = $slug
                prompt           = $entry.prompt
                category         = $entry.category
                schemaVersion    = 1
                firstRecordedAt  = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC)
            } | ConvertTo-Json) | Set-Content -LiteralPath $metaPath -Encoding UTF8
        }

        $recorded++
        Write-Host "  ✓ [$($entry.category)] $($entry.prompt) (run $run) → $slug/run-$run.json"
    }
}

$sw.Stop()

Write-Host ""
Write-Host "─── Done in $($sw.Elapsed.TotalSeconds.ToString('0.0'))s ───" -ForegroundColor Cyan
Write-Host "  Recorded : $recorded"
Write-Host "  Skipped  : $skipped (fell back)"
Write-Host "  Errored  : $errored"
Write-Host ""
Write-Host "Artifacts in: $outRoot"
