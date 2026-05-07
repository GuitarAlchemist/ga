<#
.SYNOPSIS
    Phase 3 soak runner for the ga_dsl_eval canary measurement.

.DESCRIPTION
    Fans out the prompt set in state/quality/dsl-eval/prompts.json against
    /api/chatbot/chat in parallel (Pattern 2 — fan-out, per philschmid's
    2026 subagent-patterns article), then writes a dated results JSON to
    state/quality/dsl-eval/<date>-soak-results.json.

    Per-prompt verdict: a prompt PASSES when every entry in its
    expected_contains list appears (case-insensitive substring match) in
    response.naturalLanguageAnswer. Anything else FAILS.

    The aggregate `success_rate` is the Phase 3 measurement gate target:
    >= 0.80 → proceed, 0.50–0.80 → pivot per-prompt to keyhole tools,
    < 0.50 → roll back per the plan's reversibility log.

    NOTE: this is a black-box test. It does not yet read tool-call
    telemetry (the chatbot response shape doesn't expose tool spans), so
    "DSL-eval invocation success" is approximated by "answer contains the
    expected musical artefacts". Real tool-span counters become a
    follow-up once /api/chatbot/chat surfaces them.

.PARAMETER ChatbotUrl
    Base URL of the chatbot under test. Defaults to the local dev
    server (http://localhost:5232). Pass https://demos.guitaralchemist.com
    to soak the live tunnel.

.PARAMETER PromptsPath
    Path to the prompt-set JSON. Defaults to
    state/quality/dsl-eval/prompts.json.

.PARAMETER MaxConcurrency
    Number of parallel /api/chatbot/chat calls. Defaults to 5.

.PARAMETER OutDir
    Directory for the dated output JSON. Defaults to
    state/quality/dsl-eval.

.EXAMPLE
    pwsh Scripts/run-dsl-eval-soak.ps1
    pwsh Scripts/run-dsl-eval-soak.ps1 -ChatbotUrl https://demos.guitaralchemist.com -MaxConcurrency 3
#>

[CmdletBinding()]
param(
    [string]$ChatbotUrl = 'http://localhost:5232',
    [string]$PromptsPath,
    [int]$MaxConcurrency = 5,
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $PromptsPath) { $PromptsPath = Join-Path $repoRoot 'state\quality\dsl-eval\prompts.json' }
if (-not $OutDir)      { $OutDir      = Join-Path $repoRoot 'state\quality\dsl-eval' }

if (-not (Test-Path $PromptsPath)) { throw "Prompts file not found: $PromptsPath" }
if (-not (Test-Path $OutDir))      { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

$promptSet = Get-Content $PromptsPath -Raw | ConvertFrom-Json
$prompts   = $promptSet.prompts
$endpoint  = "$ChatbotUrl/api/chatbot/chat"

Write-Host "Soak: $($prompts.Count) prompts → $endpoint (parallelism=$MaxConcurrency)"
$totalStart = Get-Date

# ForEach-Object -Parallel runs each iteration in its own runspace; pass
# data in via $using:. Each iteration POSTs the prompt and emits one
# result object that the parent aggregates.
$results = $prompts | ForEach-Object -ThrottleLimit $MaxConcurrency -Parallel {
    $p = $_
    $endpoint = $using:endpoint
    $body = @{ message = $p.text } | ConvertTo-Json -Compress

    $start = Get-Date
    try {
        $resp = Invoke-RestMethod -Uri $endpoint -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 90
        $elapsed = ((Get-Date) - $start).TotalMilliseconds
        $answer = if ($resp.naturalLanguageAnswer) { [string]$resp.naturalLanguageAnswer } else { '' }
        $needles = @($p.expected_contains)
        $missing = @($needles | Where-Object { $answer.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0 })
        $skillMatch = if ($p.expected_skill) { $resp.agentId -eq $p.expected_skill } else { $true }
        [pscustomobject]@{
            id              = $p.id
            text            = $p.text
            expected_skill  = $p.expected_skill
            actual_skill    = [string]$resp.agentId
            skill_match     = [bool]$skillMatch
            routing_method  = [string]$resp.routingMethod
            confidence      = [double]$resp.confidence
            answer          = $answer
            expected_contains = $needles
            missing         = $missing
            content_match   = ($missing.Count -eq 0)
            pass            = (($missing.Count -eq 0) -and $skillMatch)
            elapsed_ms      = [math]::Round($elapsed, 1)
            elapsed_server_ms = [int]($resp.elapsedMs ?? 0)
            error           = $null
        }
    } catch {
        $elapsed = ((Get-Date) - $start).TotalMilliseconds
        [pscustomobject]@{
            id            = $p.id
            text          = $p.text
            pass          = $false
            error         = $_.Exception.Message
            elapsed_ms    = [math]::Round($elapsed, 1)
        }
    }
}

$totalElapsed = ((Get-Date) - $totalStart).TotalSeconds
$passed = @($results | Where-Object pass).Count
$total  = $results.Count
$rate   = if ($total -gt 0) { [math]::Round($passed / $total, 3) } else { 0 }
$latencies = @($results | Where-Object { $_.elapsed_ms } | ForEach-Object elapsed_ms | Sort-Object)
$p50 = if ($latencies.Count -gt 0) { $latencies[[int]([math]::Floor($latencies.Count * 0.5))] } else { 0 }
$p95 = if ($latencies.Count -gt 0) { $latencies[[int]([math]::Floor($latencies.Count * 0.95))] } else { 0 }

$gateVerdict = if ($rate -ge 0.8) { 'proceed' }
               elseif ($rate -ge 0.5) { 'pivot' }
               else { 'rollback' }

# Aggregate by expected_closure (DSL-eval) and by actual_skill (routing)
# so we can compare ga_dsl_eval vs the keyhole MCP tools head-to-head
# without re-soaking. Each bucket reports n, pass-rate, p50, p95.
function Group-Metrics {
    param([object[]]$Items, [string]$KeyName)
    $groups = $Items | Group-Object -Property $KeyName
    $rows = foreach ($g in $groups) {
        $n = $g.Count
        $pass = @($g.Group | Where-Object pass).Count
        $lats = @($g.Group | Where-Object { $_.elapsed_ms } | ForEach-Object elapsed_ms | Sort-Object)
        $p50i = if ($lats.Count -gt 0) { $lats[[int]([math]::Floor($lats.Count * 0.5))] } else { 0 }
        $p95i = if ($lats.Count -gt 0) { $lats[[int]([math]::Floor($lats.Count * 0.95))] } else { 0 }
        [pscustomobject]@{
            key            = if ($g.Name) { $g.Name } else { '<none>' }
            n              = $n
            passed         = $pass
            pass_rate      = if ($n -gt 0) { [math]::Round($pass / $n, 3) } else { 0 }
            p50_ms         = $p50i
            p95_ms         = $p95i
        }
    }
    $rows | Sort-Object -Property key
}

# Re-attach expected_closure from the prompt set onto each result for grouping
$idToClosure = @{}
foreach ($p in $prompts) { $idToClosure[$p.id] = $p.expected_closure }
$enriched = foreach ($r in $results) {
    $r | Add-Member -MemberType NoteProperty -Name expected_closure -Value $idToClosure[$r.id] -Force -PassThru
}

$byClosure = Group-Metrics -Items $enriched -KeyName 'expected_closure'
$bySkill   = Group-Metrics -Items $enriched -KeyName 'actual_skill'

$outDate = (Get-Date).ToString('yyyy-MM-dd-HHmmss')
$outPath = Join-Path $OutDir "$outDate-soak-results.json"
# `latest-soak-results.json` is the only result file checked into git
# (gitignore excludes the dated form). Per PR #151 review arch-F8: weekly
# soaks shouldn't add a fresh committed JSON — operators want one stable
# file showing current state, plus dated artefacts for local diffing.
$latestPath = Join-Path $OutDir "latest-soak-results.json"
$payload = [ordered]@{
    schema_version    = '0.1'
    prompts_version   = $promptSet.version
    chatbot_url       = $ChatbotUrl
    started_at        = $totalStart.ToUniversalTime().ToString('o')
    duration_seconds  = [math]::Round($totalElapsed, 2)
    parallelism       = $MaxConcurrency
    total_prompts     = $total
    passed            = $passed
    success_rate      = $rate
    p50_ms            = $p50
    p95_ms            = $p95
    gate              = [ordered]@{
        proceed_at  = 0.80
        pivot_at    = 0.50
        rollback_at = 0.50
        verdict     = $gateVerdict
    }
    by_closure        = @($byClosure)
    by_skill          = @($bySkill)
    results           = @($enriched)
}
$json = $payload | ConvertTo-Json -Depth 8
$json | Set-Content -Path $outPath    -Encoding utf8
$json | Set-Content -Path $latestPath -Encoding utf8

Write-Host ''
Write-Host "Soak complete in $([math]::Round($totalElapsed,1))s. Passed $passed/$total ($([math]::Round($rate*100,1))%). p50=${p50}ms, p95=${p95}ms."
Write-Host "Gate verdict: $gateVerdict"
Write-Host "Wrote: $outPath (dated, local) + $latestPath (committed)"

Write-Host ''
Write-Host '=== by closure (expected DSL-eval target) ==='
$byClosure | Format-Table key, n, passed, pass_rate, p50_ms, p95_ms -AutoSize | Out-String | Write-Host

Write-Host '=== by skill (router actual destination) ==='
$bySkill | Format-Table key, n, passed, pass_rate, p50_ms, p95_ms -AutoSize | Out-String | Write-Host

Write-Host '=== per prompt ==='
$enriched | Sort-Object id | ForEach-Object {
    $mark = if ($_.pass) { 'PASS' } elseif ($_.error) { 'ERR ' } else { 'FAIL' }
    $snippet = if ($_.answer) { $_.answer } elseif ($_.error) { $_.error } else { '' }
    if ($snippet.Length -gt 60) { $snippet = $snippet.Substring(0, 60) }
    $snippet = $snippet.Replace("`r", '').Replace("`n", ' ')
    Write-Host ("  {0} {1,-22} skill={2,-22} → {3}" -f $mark, $_.id, $_.actual_skill, $snippet)
}
