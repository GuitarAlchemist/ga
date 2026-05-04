#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Measure Phase 0 QA Architect baseline runtimes.

.DESCRIPTION
    Runs a small set of local probes and writes an additive JSON snapshot to
    state/quality/qa-architect/baseline.json. Probes are intentionally recorded
    independently: a failed or timed-out probe is baseline data, not a reason to
    discard the whole run.

.PARAMETER OutputPath
    JSON baseline output path.

.PARAMETER SkipOllama
    Skip local Ollama HTTP probes.

.PARAMETER OllamaUrl
    Base URL for local Ollama.

.PARAMETER OllamaModel
    Model to use for the guarded generation probe. If omitted, the script
    prefers qwen2.5-coder:7b when available, then the first local model.

.PARAMETER OllamaTimeoutSeconds
    Timeout for each Ollama HTTP request.
#>

param(
    [string]$OutputPath,
    [switch]$SkipOllama,
    [string]$OllamaUrl = "http://localhost:11434",
    [string]$OllamaModel,
    [int]$OllamaTimeoutSeconds = 60
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "state/quality/qa-architect/baseline.json"
}

function ConvertTo-OutputTail {
    param([string]$Text, [int]$MaxLength = 6000)

    if ([string]::IsNullOrEmpty($Text)) {
        return ""
    }

    if ($Text.Length -le $MaxLength) {
        return $Text.Trim()
    }

    return $Text.Substring($Text.Length - $MaxLength).Trim()
}

function Invoke-BaselineCommand {
    param(
        [string]$Name,
        [string]$Category,
        [string]$Command,
        [scriptblock]$Script
    )

    Write-Host ""
    Write-Host "== $Name ==" -ForegroundColor Cyan

    $started = (Get-Date).ToUniversalTime()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $exitCode = 0
    $status = "passed"
    $output = ""
    $errorMessage = $null

    try {
        $global:LASTEXITCODE = 0
        $output = (& $Script 2>&1 | Out-String)
        $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { [int]$LASTEXITCODE }
        if ($exitCode -ne 0) {
            $status = "failed"
        }
    }
    catch {
        $status = "failed"
        $exitCode = 1
        $errorMessage = $_.Exception.Message
        $output = $_ | Out-String
    }
    finally {
        $stopwatch.Stop()
    }

    Write-Host "$status in $($stopwatch.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor $(if ($status -eq "passed") { "Green" } else { "Yellow" })

    [ordered]@{
        name = $Name
        category = $Category
        command = $Command
        status = $status
        exit_code = $exitCode
        started_at = $started.ToString("O")
        elapsed_seconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
        error = $errorMessage
        output_tail = ConvertTo-OutputTail $output
    }
}

function Invoke-OllamaHttpProbe {
    param(
        [string]$Name,
        [string]$Category,
        [string]$Command,
        [scriptblock]$Script
    )

    Write-Host ""
    Write-Host "== $Name ==" -ForegroundColor Cyan

    $started = (Get-Date).ToUniversalTime()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "passed"
    $errorMessage = $null
    $payload = $null

    try {
        $payload = & $Script
    }
    catch {
        $status = if ($_.Exception.Message -match "timed out|timeout|operation was canceled") { "timed_out" } else { "failed" }
        $errorMessage = $_.Exception.Message
    }
    finally {
        $stopwatch.Stop()
    }

    Write-Host "$status in $($stopwatch.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor $(if ($status -eq "passed") { "Green" } else { "Yellow" })

    [ordered]@{
        name = $Name
        category = $Category
        command = $Command
        status = $status
        exit_code = if ($status -eq "passed") { 0 } else { 1 }
        started_at = $started.ToString("O")
        elapsed_seconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
        error = $errorMessage
        output_tail = if ($null -eq $payload) { "" } else { ConvertTo-OutputTail (($payload | ConvertTo-Json -Depth 12 -Compress)) }
    }
}

function Get-GitValue {
    param([string[]]$GitArgs)

    try {
        return ((& git @GitArgs 2>$null) | Out-String).Trim()
    }
    catch {
        return $null
    }
}

Set-Location $repoRoot
$env:GA_REPO_ROOT = $repoRoot
$runStarted = (Get-Date).ToUniversalTime()
$runStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$probes = @()
$runId = $runStarted.ToString("yyyyMMddTHHmmssZ")
$outRoot = Join-Path ([System.IO.Path]::GetTempPath()) "ga-qa-architect-baseline-$runId"
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null
$contractOut = Join-Path $outRoot "qa-verdict-contract"
$backendSchemaOut = Join-Path $outRoot "backend-schema"
$opticKOut = Join-Path $outRoot "optick-search"

$probes += Invoke-BaselineCommand `
    -Name "qa_verdict_contract_tests" `
    -Category "contract" `
    -Command 'dotnet test Tests\Common\GA.Business.ML.Tests\GA.Business.ML.Tests.csproj --filter "FullyQualifiedName~QaArchitectAgentTests" -p:OutDir=<temp>\qa-verdict-contract\' `
    -Script {
        dotnet test Tests\Common\GA.Business.ML.Tests\GA.Business.ML.Tests.csproj --filter "FullyQualifiedName~QaArchitectAgentTests" --logger "console;verbosity=minimal" -p:OutDir="$contractOut\"
    }

$probes += Invoke-BaselineCommand `
    -Name "backend_schema_smoke_tests" `
    -Category "backend_tests" `
    -Command 'dotnet test Tests\Common\GA.Business.Core.Tests\GA.Business.Core.Tests.csproj --filter "FullyQualifiedName~SchemaContractTests" -p:OutDir=<temp>\backend-schema\' `
    -Script {
        dotnet test Tests\Common\GA.Business.Core.Tests\GA.Business.Core.Tests.csproj --filter "FullyQualifiedName~SchemaContractTests" --logger "console;verbosity=minimal" -p:OutDir="$backendSchemaOut\"
    }

$probes += Invoke-BaselineCommand `
    -Name "optick_search_smoke_tests" `
    -Category "optic_k" `
    -Command 'dotnet test Tests\Common\GA.Business.ML.Tests\GA.Business.ML.Tests.csproj --filter "FullyQualifiedName~MusicalQueryEncoderTests" -p:OutDir=<temp>\optick-search\' `
    -Script {
        dotnet test Tests\Common\GA.Business.ML.Tests\GA.Business.ML.Tests.csproj --filter "FullyQualifiedName~MusicalQueryEncoderTests" --logger "console;verbosity=minimal" -p:OutDir="$opticKOut\"
    }

if ($SkipOllama) {
    $probes += [ordered]@{
        name = "ollama_tags_probe"
        category = "semantic_judge"
        command = "GET $OllamaUrl/api/tags"
        status = "skipped"
        exit_code = 0
        started_at = (Get-Date).ToUniversalTime().ToString("O")
        elapsed_seconds = 0
        error = $null
        output_tail = "Skipped by -SkipOllama."
    }
}
else {
    $tagsPayload = $null
    $tagsProbe = Invoke-OllamaHttpProbe `
        -Name "ollama_tags_probe" `
        -Category "semantic_judge" `
        -Command "GET $OllamaUrl/api/tags" `
        -Script {
            $script:tagsPayload = Invoke-RestMethod -Method Get -Uri "$OllamaUrl/api/tags" -TimeoutSec $OllamaTimeoutSeconds
            $script:tagsPayload
        }
    $probes += $tagsProbe

    if ($tagsProbe.status -eq "passed") {
        $modelNames = @($script:tagsPayload.models | ForEach-Object { $_.name })
        if ([string]::IsNullOrWhiteSpace($OllamaModel)) {
            $preferred = $modelNames | Where-Object { $_ -eq "qwen2.5-coder:7b" } | Select-Object -First 1
            $OllamaModel = if ($preferred) { $preferred } else { $modelNames | Select-Object -First 1 }
        }

        if ([string]::IsNullOrWhiteSpace($OllamaModel)) {
            $probes += [ordered]@{
                name = "ollama_generation_probe"
                category = "semantic_judge"
                command = "POST $OllamaUrl/api/generate"
                status = "skipped"
                exit_code = 0
                started_at = (Get-Date).ToUniversalTime().ToString("O")
                elapsed_seconds = 0
                error = $null
                output_tail = "No local Ollama models were listed."
            }
        }
        else {
            $body = @{
                model = $OllamaModel
                stream = $false
                prompt = "Return exactly one JSON object: {`"verdict`":`"informational`",`"risk_tier`":`"P3`"}. No prose."
                options = @{
                    num_predict = 64
                    temperature = 0
                }
            } | ConvertTo-Json -Depth 8

            $probes += Invoke-OllamaHttpProbe `
                -Name "ollama_generation_probe" `
                -Category "semantic_judge" `
                -Command "POST $OllamaUrl/api/generate model=$OllamaModel timeout=${OllamaTimeoutSeconds}s" `
                -Script {
                    Invoke-RestMethod -Method Post -Uri "$OllamaUrl/api/generate" -Body $body -ContentType "application/json" -TimeoutSec $OllamaTimeoutSeconds
                }
        }
    }
}

$runStopwatch.Stop()
$passed = @($probes | Where-Object { $_.status -eq "passed" }).Count
$failed = @($probes | Where-Object { $_.status -eq "failed" }).Count
$timedOut = @($probes | Where-Object { $_.status -eq "timed_out" }).Count
$skipped = @($probes | Where-Object { $_.status -eq "skipped" }).Count
$ollamaGeneration = $probes | Where-Object { $_.name -eq "ollama_generation_probe" } | Select-Object -First 1
$latencyFeasibility = if ($null -eq $ollamaGeneration) {
    "unknown"
}
elseif ($ollamaGeneration.status -ne "passed") {
    "not_established"
}
elseif ($ollamaGeneration.elapsed_seconds -le 480) {
    "needs_more_samples"
}
else {
    "not_established"
}

$baseline = [ordered]@{
    schema_version = 1
    produced_at = (Get-Date).ToUniversalTime().ToString("O")
    repo_root = $repoRoot
    temp_output_root = $outRoot
    git = [ordered]@{
        branch = Get-GitValue -GitArgs @("branch", "--show-current")
        sha = Get-GitValue -GitArgs @("rev-parse", "HEAD")
        dirty = -not [string]::IsNullOrWhiteSpace((Get-GitValue -GitArgs @("status", "--porcelain")))
    }
    thresholds = [ordered]@{
        provisional_local_tribunal_p95_seconds = 480
        hard_stop_seconds = 900
        ollama_probe_timeout_seconds = $OllamaTimeoutSeconds
    }
    summary = [ordered]@{
        started_at = $runStarted.ToString("O")
        elapsed_seconds = [Math]::Round($runStopwatch.Elapsed.TotalSeconds, 3)
        probe_count = $probes.Count
        passed = $passed
        failed = $failed
        timed_out = $timedOut
        skipped = $skipped
        local_tribunal_p95_feasibility = $latencyFeasibility
        notes = @(
            "This is a Phase 0 local baseline, not a statistical P95.",
            "Failed or timed-out probes are preserved as evidence."
        )
    }
    probes = $probes
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$baseline | ConvertTo-Json -Depth 16 | Set-Content -Path $OutputPath -Encoding utf8

Write-Host ""
Write-Host "Baseline written to $OutputPath" -ForegroundColor Green
Write-Host "Summary: passed=$passed failed=$failed timed_out=$timedOut skipped=$skipped elapsed=$($runStopwatch.Elapsed.TotalSeconds.ToString('F1'))s"
