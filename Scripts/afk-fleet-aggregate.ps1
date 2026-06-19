# afk-fleet-aggregate.ps1 — collect the latest QA-loop signal from every repo in
# the GuitarAlchemist fleet into ONE feed the unified dashboard reads.
#
# Why an aggregator (not cross-repo fetch in the browser): a static dashboard
# can only fetch under one served root, but the feeds live in sibling repos
# (../ix, ../Demerzel). This script has filesystem access, so it reads all of
# them and writes a single ga/state/quality/ecosystem/afk-fleet.json. Mirrors
# the existing fleet-status.yml / ecosystem-health.yml aggregation pattern.
#
# Run from the ga repo:  pwsh Scripts/afk-fleet-aggregate.ps1
# Then the unified dashboard (Tools/afk-fleet-dashboard/) shows all repos.

[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
$gaRoot   = Split-Path $PSScriptRoot -Parent
$parent   = Split-Path $gaRoot -Parent
$ixRoot   = Join-Path $parent "ix"
$demRoot  = Join-Path $parent "Demerzel"
$outDir   = Join-Path $gaRoot "state/quality/ecosystem"
$outPath  = Join-Path $outDir "afk-fleet.json"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

function Read-JsonSafe([string]$path) {
    if ($path -and (Test-Path $path)) {
        try { return Get-Content -Raw $path | ConvertFrom-Json } catch { return $null }
    }
    return $null
}
function Latest([string]$dir, [string]$filter) {
    if (-not (Test-Path $dir)) { return $null }
    Get-ChildItem -Path $dir -Filter $filter -File -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
}

$repos = @()

# ── ga — chatbot AFK harness (live develop+QA loop) ───────────────────────────
$gaStatus = Read-JsonSafe (Join-Path $gaRoot "state/quality/chatbot-qa/afk-runs/latest-status.json")
$gaTargets = Latest (Join-Path $gaRoot "state/quality/chatbot-qa/afk-runs") "*.targets.json"
$gaTargetCount = if ($gaTargets) { (Read-JsonSafe $gaTargets).queued_targets.Count } else { 0 }
if ($gaStatus) {
    $repos += [ordered]@{
        repo = "ga"; domain = "chatbot-qa"; kind = "afk-harness"
        state = $gaStatus.state; updated_at = $gaStatus.updated_at
        primary_label = "deterministic"; primary_pct = $gaStatus.current.deterministic_pass_pct
        secondary_label = "semantic";    secondary_pct = $gaStatus.current.semantic_pass_pct
        target = $gaStatus.target_metric; queued_targets = $gaTargetCount
        detail = $gaStatus.last_event; source = "ga/state/quality/chatbot-qa/afk-runs/latest-status.json"
    }
} else {
    $repos += [ordered]@{ repo = "ga"; domain = "chatbot-qa"; kind = "afk-harness"; state = "no-data"; detail = "no harness run yet" }
}

# ── ix — adversarial LLM judge panel ──────────────────────────────────────────
$ixPanel = Latest (Join-Path $ixRoot "state/adversarial") "llm-panel-*.json"
$ixObj = if ($ixPanel) { Read-JsonSafe $ixPanel } else { Read-JsonSafe (Join-Path $ixRoot "state/adversarial/summary.json") }
if ($ixObj) {
    $pct = if ($null -ne $ixObj.agreement_pct) { [double]$ixObj.agreement_pct / 100.0 } `
           elseif ($null -ne $ixObj.match_rate) { [double]$ixObj.match_rate } else { $null }
    $repos += [ordered]@{
        repo = "ix"; domain = "adversarial-qa"; kind = "llm-panel"
        state = if ($ixObj.degraded) { "degraded" } elseif ($ixPanel) { "done" } else { "baseline" }
        updated_at = $ixObj.timestamp
        primary_label = "agreement"; primary_pct = $pct
        secondary_label = $null; secondary_pct = $null
        verdicts = $ixObj.by_truth_value; graded = $ixObj.graded
        detail = if ($ixPanel) { "llm-panel: $($ixObj.graded) graded" } else { "deterministic baseline ($($ixObj.pipeline_version)); LLM panel not yet run on schedule" }
        source = if ($ixPanel) { "ix/$(Split-Path $ixPanel -Leaf)" } else { "ix/state/adversarial/summary.json" }
    }
} else {
    $repos += [ordered]@{ repo = "ix"; domain = "adversarial-qa"; kind = "llm-panel"; state = "no-data"; detail = "no adversarial feed" }
}

# ── Demerzel — QA tribunal verdicts ───────────────────────────────────────────
$demVerdict = $null
$vdir = Join-Path $demRoot "state/quality/verdicts"
if (Test-Path $vdir) {
    $demVerdict = Get-ChildItem -Path $vdir -Recurse -Filter "*.json" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if ($demVerdict) {
    $v = Read-JsonSafe $demVerdict.FullName
    $repos += [ordered]@{
        repo = "Demerzel"; domain = "qa-tribunal"; kind = "verdict"
        state = if ($v.verdict -eq "block") { "blocked" } elseif ($v.verdict -eq "pass") { "done" } else { "running" }
        updated_at = $v.produced_at
        primary_label = "verdict"; primary_text = "$($v.verdict) ($($v.risk_tier))"
        detail = $v.narrative; source = "Demerzel/state/quality/verdicts/$($demVerdict.Name)"
    }
} else {
    $repos += [ordered]@{ repo = "Demerzel"; domain = "qa-tribunal"; kind = "verdict"; state = "no-data"; detail = "no verdicts emitted yet — run /demerzel-tribunal-panel on a PR" }
}

$fleet = [ordered]@{
    schema_version = 1
    generated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    repos = $repos
}
($fleet | ConvertTo-Json -Depth 8) | Set-Content -Path $outPath -Encoding utf8
Write-Host "afk-fleet: aggregated $($repos.Count) repos -> $outPath"
