# replay-chatbot-goldens.ps1 — semantic-regression diff for chatbot goldens
#
# Replays each prompt under state/quality/chatbot-qa/golden-traces/ that has a
# stored reference answer (run-1.json with response.naturalLanguageAnswer)
# against a running chatbot, embeds both answers via OpenAI text-embedding-3-
# small, computes cosine similarity, and writes:
#
#   1. A structured JSON artifact at <OutputDir>/<HeadSha>.json conforming to
#      semantic-regression-v1 (see state/quality/chatbot-qa/semantic-
#      regression/SCHEMA.json).
#   2. A markdown PR-comment table at <EmitMarkdown> (optional).
#
# Designed to be CI-callable AND locally-runnable. Locally:
#
#   pwsh Scripts/replay-chatbot-goldens.ps1                          # all prompts, default threshold
#   pwsh Scripts/replay-chatbot-goldens.ps1 -SampleSize 5             # random 5
#   pwsh Scripts/replay-chatbot-goldens.ps1 -Threshold 0.90           # stricter
#   pwsh Scripts/replay-chatbot-goldens.ps1 -ChatbotUrl http://localhost:5252
#
# Requires: $env:OPENAI_API_KEY for embedding calls. Without it the script
# fails fast — "we'll add semantic diff later" is the failure mode the
# harness is designed to prevent.
#
# Why text-embedding-3-small and not ga_generate_voicing_embedding:
#   The GA MCP voicing embedder produces 228-dim vectors from raw pitch-
#   class geometry — perfect for voicings, useless for prose. Chatbot
#   answers are markdown English. text-embedding-3-small is already the
#   project's prose embedder (used by VectorSearchService.cs and
#   GaDataCLI). One encoder per run, recorded in the artifact, prevents
#   apples-to-oranges drift charts.
#
# Provider-replay discipline:
#   The user's harness contract is explicit: replay against the SAME
#   provider the original golden used. We surface response.agentId (e.g.
#   skill.circleoffifths) and compare it against the new agentId. If they
#   diverge we tag the prompt "provider_mismatch" and skip the cosine
#   verdict — that's a routing test, not a semantic regression.

[CmdletBinding()]
param(
    [string]$ChatbotUrl     = "http://localhost:5252",
    [int]$TimeoutSeconds    = 120,
    [double]$Threshold      = 0.85,
    [int]$SampleSize        = 0,   # 0 == all
    [string]$HeadSha        = "",
    [string]$GoldensDir     = "state/quality/chatbot-qa/golden-traces",
    [string]$OutputDir      = "state/quality/chatbot-qa/semantic-regression",
    [string]$EmbeddingModel = "text-embedding-3-small",
    [int]$TokenWarnBudget   = 50000,
    [string]$EmitMarkdown   = ""
)

$ErrorActionPreference = "Stop"

# Resolve repo root (script lives in <repo>/Scripts/)
$repoRoot   = Split-Path $PSScriptRoot -Parent
$goldensAbs = Join-Path $repoRoot $GoldensDir
$outputAbs  = Join-Path $repoRoot $OutputDir

if (-not (Test-Path $goldensAbs)) {
    throw "Goldens directory not found: $goldensAbs"
}
if (-not (Test-Path $outputAbs)) {
    New-Item -ItemType Directory -Path $outputAbs -Force | Out-Null
}

# ── OpenAI guard ───────────────────────────────────────────────────────────
$openAiKey = $env:OPENAI_API_KEY
if ([string]::IsNullOrWhiteSpace($openAiKey)) {
    throw "OPENAI_API_KEY not set. Cannot embed answers."
}

# ── Resolve HeadSha if not passed in (local runs) ──────────────────────────
if ([string]::IsNullOrWhiteSpace($HeadSha)) {
    try {
        $HeadSha = (git -C $repoRoot rev-parse HEAD).Trim()
    } catch {
        $HeadSha = "unknown-$(Get-Date -Format yyyyMMddHHmmss)"
    }
}

Write-Host "─── Semantic regression: chatbot goldens ───" -ForegroundColor Cyan
Write-Host "Chatbot URL    : $ChatbotUrl"
Write-Host "Goldens dir    : $goldensAbs"
Write-Host "Output dir     : $outputAbs"
Write-Host "Encoder        : $EmbeddingModel"
Write-Host "Threshold      : $Threshold"
Write-Host "Sample size    : $(if ($SampleSize -gt 0) { $SampleSize } else { 'all' })"
Write-Host "Head SHA       : $HeadSha"

# ── Discover prompts that have a reference answer ──────────────────────────
$candidates = @()
foreach ($dir in Get-ChildItem -Directory -Path $goldensAbs) {
    $metaPath = Join-Path $dir.FullName "_meta.json"
    $run1Path = Join-Path $dir.FullName "run-1.json"
    if (-not (Test-Path $run1Path)) { continue }

    try {
        $run1 = Get-Content -LiteralPath $run1Path -Raw | ConvertFrom-Json
    } catch {
        Write-Host "  ! skip $($dir.Name): run-1.json unparseable ($($_.Exception.Message))" -ForegroundColor DarkYellow
        continue
    }

    $refAnswer = $null
    if ($run1.response -and $run1.response.naturalLanguageAnswer) {
        $refAnswer = [string]$run1.response.naturalLanguageAnswer
    }
    if ([string]::IsNullOrWhiteSpace($refAnswer)) { continue }

    $prompt   = if ($run1.prompt) { [string]$run1.prompt } else { $dir.Name }
    $category = if ($run1.category) { [string]$run1.category } else { 'uncategorized' }
    $refAgent = if ($run1.response.agentId) { [string]$run1.response.agentId } else { $null }

    if (Test-Path $metaPath) {
        try {
            $meta = Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json
            if ($meta.prompt)   { $prompt   = [string]$meta.prompt }
            if ($meta.category) { $category = [string]$meta.category }
        } catch { }
    }

    $candidates += [pscustomobject]@{
        Slug       = $dir.Name
        Prompt     = $prompt
        Category   = $category
        RefAgent   = $refAgent
        RefAnswer  = $refAnswer
    }
}

if ($candidates.Count -eq 0) {
    Write-Host "No prompts have a stored reference answer yet (need run-1.json with response.naturalLanguageAnswer)." -ForegroundColor Yellow
    Write-Host "Writing an empty artifact so the dashboard sees the run happened." -ForegroundColor Yellow
}

# ── Optional sampling ──────────────────────────────────────────────────────
if ($SampleSize -gt 0 -and $candidates.Count -gt $SampleSize) {
    $candidates = $candidates | Get-Random -Count $SampleSize
    Write-Host "Sampled $SampleSize of $($candidates.Count) prompts (cost guardrail)." -ForegroundColor Yellow
}

Write-Host "Replaying $($candidates.Count) prompt(s)."

# ── OpenAI embedding helper ────────────────────────────────────────────────
function Get-OpenAiEmbedding {
    param([string]$Text, [string]$Model = $EmbeddingModel)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        throw "Refusing to embed empty text."
    }

    $body = @{ input = $Text; model = $Model } | ConvertTo-Json -Compress
    $headers = @{
        "Authorization" = "Bearer $openAiKey"
        "Content-Type"  = "application/json"
    }
    $resp = Invoke-RestMethod `
        -Method Post `
        -Uri "https://api.openai.com/v1/embeddings" `
        -Headers $headers `
        -Body $body `
        -TimeoutSec 30

    $vec = $resp.data[0].embedding
    $tokens = if ($resp.usage -and $resp.usage.total_tokens) { [int]$resp.usage.total_tokens } else { 0 }
    return [pscustomobject]@{ Vector = $vec; Tokens = $tokens }
}

function Get-CosineSimilarity {
    param($A, $B)
    if ($A.Count -ne $B.Count) {
        throw "Embedding dimension mismatch: $($A.Count) vs $($B.Count)."
    }
    $dot = 0.0; $na = 0.0; $nb = 0.0
    for ($i = 0; $i -lt $A.Count; $i++) {
        $av = [double]$A[$i]
        $bv = [double]$B[$i]
        $dot += $av * $bv
        $na  += $av * $av
        $nb  += $bv * $bv
    }
    if ($na -eq 0 -or $nb -eq 0) { return 0.0 }
    return $dot / ([math]::Sqrt($na) * [math]::Sqrt($nb))
}

# ── Replay loop ────────────────────────────────────────────────────────────
$results       = New-Object System.Collections.Generic.List[object]
$totalTokens   = 0
$okCount       = 0
$driftCount    = 0
$erroredCount  = 0
$mismatchCount = 0
$noBaselineCount = 0
$sw = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($c in $candidates) {
    $sessionId = "semreg-$($c.Slug)-$(Get-Date -Format yyyyMMddHHmmss)"
    $body = @{ message = $c.Prompt; sessionId = $sessionId } | ConvertTo-Json -Compress

    $entry = [ordered]@{
        slug             = $c.Slug
        prompt           = $c.Prompt
        category         = $c.Category
        provider_ref     = $c.RefAgent
        provider_new     = $null
        ref_cosine_self  = 1.0
        new_cosine       = $null
        delta            = $null
        verdict          = $null
        tokens           = 0
        error            = $null
    }

    # Replay against live chatbot
    try {
        $resp = Invoke-RestMethod `
            -Method Post `
            -Uri "$ChatbotUrl/api/chatbot/chat" `
            -ContentType "application/json" `
            -Body $body `
            -TimeoutSec $TimeoutSeconds
    } catch {
        $entry.verdict = "errored"
        $entry.error   = "replay_http: $($_.Exception.Message)"
        $erroredCount++
        Write-Host "  x [$($c.Category)] $($c.Prompt): $($_.Exception.Message)" -ForegroundColor Red
        $results.Add([pscustomobject]$entry) | Out-Null
        continue
    }

    $newAnswer = if ($resp -and $resp.naturalLanguageAnswer) { [string]$resp.naturalLanguageAnswer } else { $null }
    $newAgent  = if ($resp -and $resp.agentId) { [string]$resp.agentId } else { $null }
    $entry.provider_new = $newAgent

    if ([string]::IsNullOrWhiteSpace($newAnswer)) {
        $entry.verdict = "errored"
        $entry.error   = "replay_empty_answer"
        $erroredCount++
        Write-Host "  x [$($c.Category)] $($c.Prompt): empty answer in response" -ForegroundColor Red
        $results.Add([pscustomobject]$entry) | Out-Null
        continue
    }

    # Provider-replay discipline
    if ($c.RefAgent -and $newAgent -and ($c.RefAgent -ne $newAgent)) {
        $entry.verdict = "provider_mismatch"
        $entry.error   = "ref agent '$($c.RefAgent)' != new '$newAgent' (routing change, not semantic regression)"
        $mismatchCount++
        Write-Host "  ~ [$($c.Category)] $($c.Prompt): provider drift $($c.RefAgent) -> $newAgent" -ForegroundColor DarkYellow
        $results.Add([pscustomobject]$entry) | Out-Null
        continue
    }

    # Embed both sides
    try {
        $eRef = Get-OpenAiEmbedding -Text $c.RefAnswer
        $eNew = Get-OpenAiEmbedding -Text $newAnswer
    } catch {
        $entry.verdict = "errored"
        $entry.error   = "embedding: $($_.Exception.Message)"
        $erroredCount++
        Write-Host "  x [$($c.Category)] $($c.Prompt): embedding failed - $($_.Exception.Message)" -ForegroundColor Red
        $results.Add([pscustomobject]$entry) | Out-Null
        continue
    }

    $tokensUsed = $eRef.Tokens + $eNew.Tokens
    $totalTokens += $tokensUsed
    $entry.tokens = $tokensUsed

    $cos = Get-CosineSimilarity -A $eRef.Vector -B $eNew.Vector
    $entry.new_cosine = [math]::Round($cos, 4)
    $entry.delta      = [math]::Round($cos - 1.0, 4)

    if ($cos -lt $Threshold) {
        $entry.verdict = "drift"
        $driftCount++
        Write-Host "  ! [$($c.Category)] $($c.Prompt): cosine=$([math]::Round($cos,3)) (< $Threshold) DRIFT" -ForegroundColor Yellow
    } else {
        $entry.verdict = "ok"
        $okCount++
        Write-Host "  o [$($c.Category)] $($c.Prompt): cosine=$([math]::Round($cos,3)) ok"
    }

    $results.Add([pscustomobject]$entry) | Out-Null
}

$sw.Stop()

# ── Cost guardrail warning ────────────────────────────────────────────────
$costWarning = $null
if ($totalTokens -gt $TokenWarnBudget) {
    $costWarning = "Token usage $totalTokens exceeded warn budget $TokenWarnBudget. " +
                   "Consider -SampleSize or moving the workflow from per-PR to weekly."
    Write-Host ""
    Write-Host "WARN: $costWarning" -ForegroundColor Yellow
}

# ── Write artifact ────────────────────────────────────────────────────────
$artifact = [ordered]@{
    schema           = "semantic-regression-v1"
    head_sha         = $HeadSha
    run_at           = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC)
    chatbot_url      = $ChatbotUrl
    encoder          = $EmbeddingModel
    threshold_cosine = $Threshold
    sample_size      = $SampleSize
    elapsed_seconds  = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    cost_warning     = $costWarning
    prompts          = $results.ToArray()
    summary          = [ordered]@{
        total              = $results.Count
        ok                 = $okCount
        drift              = $driftCount
        errored            = $erroredCount
        provider_mismatch  = $mismatchCount
        no_baseline        = $noBaselineCount
        total_tokens       = $totalTokens
    }
}

$outPath = Join-Path $outputAbs ("{0}.json" -f $HeadSha)
$artifact | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outPath -Encoding UTF8
Write-Host ""
Write-Host "Wrote artifact: $outPath"

# ── Markdown PR comment ───────────────────────────────────────────────────
if (-not [string]::IsNullOrWhiteSpace($EmitMarkdown)) {
    $emitAbs = if ([System.IO.Path]::IsPathRooted($EmitMarkdown)) {
        $EmitMarkdown
    } else {
        Join-Path $repoRoot $EmitMarkdown
    }
    $emitDir = Split-Path $emitAbs -Parent
    if ($emitDir -and -not (Test-Path $emitDir)) {
        New-Item -ItemType Directory -Path $emitDir -Force | Out-Null
    }

    $lines = @()
    $lines += "### Semantic regression: chatbot goldens"
    $lines += ""
    $lines += "- Encoder: ``$EmbeddingModel``"
    $lines += "- Threshold: cosine < ``$Threshold`` is flagged"
    $lines += "- Head SHA: ``$HeadSha``"
    $lines += "- Summary: $okCount ok / $driftCount drift / $mismatchCount provider-mismatch / $erroredCount errored (of $($results.Count))"
    $lines += "- Tokens: $totalTokens (warn budget $TokenWarnBudget)"
    if ($costWarning) { $lines += "- ⚠ $costWarning" }
    $lines += ""
    $lines += "| Prompt | Provider | Ref cosine→self | New cosine | Δ | Verdict |"
    $lines += "|---|---|---|---|---|---|"

    foreach ($r in $results) {
        $provider = if ($r.provider_new) { $r.provider_new } elseif ($r.provider_ref) { $r.provider_ref } else { '-' }
        $cosRef   = '1.0'
        $cosNew   = if ($null -ne $r.new_cosine) { ('{0:N3}' -f $r.new_cosine) } else { '-' }
        $delta    = if ($null -ne $r.delta)      { ('{0:+0.000;-0.000;0.000}' -f $r.delta) } else { '-' }
        $verdict  = switch ($r.verdict) {
            'ok'                { 'OK' }
            'drift'             { '⚠ DRIFT' }
            'provider_mismatch' { 'provider-mismatch' }
            'errored'           { 'errored' }
            default             { '-' }
        }
        $shortPrompt = if ($r.prompt.Length -gt 60) { $r.prompt.Substring(0,57) + '...' } else { $r.prompt }
        # Escape pipes inside prompt cells
        $shortPrompt = $shortPrompt -replace '\|','\|'
        $lines += "| $shortPrompt | $provider | $cosRef | $cosNew | $delta | $verdict |"
    }

    $lines += ""
    $lines += "Artifact: ``$(Resolve-Path -Relative $outPath)`` (also uploaded as a workflow artifact)."
    $lines += ""
    $lines += "_Drift flags are advisory — they do not fail the build. Review the diff and merge if the new answer is intentional._"

    ($lines -join "`n") | Set-Content -LiteralPath $emitAbs -Encoding UTF8
    Write-Host "Wrote PR comment markdown: $emitAbs"
}

Write-Host ""
Write-Host "─── Done in $($sw.Elapsed.TotalSeconds.ToString('0.0'))s ───" -ForegroundColor Cyan
Write-Host "  ok: $okCount  drift: $driftCount  mismatch: $mismatchCount  errored: $erroredCount  tokens: $totalTokens"

# Exit code: 0 on completion regardless of drift count (drift is reported,
# not fatal). Non-zero only if the run itself couldn't produce data.
if ($results.Count -eq 0 -and $candidates.Count -gt 0) {
    Write-Host "All replay attempts errored — surfacing as workflow failure." -ForegroundColor Red
    exit 2
}
exit 0
