#!/usr/bin/env pwsh
# verify-loop-record.ps1 — asserts Scripts/loop-record.ps1 (the per-cycle loop
# ledger WRITER, Contract A for ix-duck's loop lens) emits rows that match the
# pinned fixture schema and honour the fail-closed + sentinel rules.
#
# No external deps; writes to a temp ledger and cleans up. Exit 0 = all pass.
#   pwsh state/quality/_fixtures/verify-loop-record.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')
$writer = Join-Path $root 'Scripts/loop-record.ps1'
$fixture = Join-Path $PSScriptRoot 'loop-iterations.sample.jsonl'
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("verify-loop-record-" + [guid]::NewGuid().ToString() + ".iterations.jsonl")
$fail = 0
function Check($name, $cond) {
    if ($cond) { Write-Host "  PASS  $name" -ForegroundColor Green }
    else { Write-Host "  FAIL  $name" -ForegroundColor Red; $script:fail++ }
}

try {
    # 1. Normal row — delta derived, roundtrip honoured.
    & $writer -LoopId run-A -Domain chatbot-qa -Iteration 1 -MetricName pass_pct `
        -MetricBefore 0.88 -MetricAfter 0.91 -Verdict improved -WorstItem p-12 `
        -ArtifactEdited Common/X.cs -CommitSha abc1234 -RoundtripPassed -LedgerPath $tmp | Out-Null
    $r1 = Get-Content $tmp | Select-Object -First 1 | ConvertFrom-Json
    Check "delta derived (after-before = 0.03)" ([math]::Abs($r1.metric_delta - 0.03) -lt 1e-6)
    Check "roundtrip_passed = true" ($r1.roundtrip_passed -eq $true)
    Check "real domain preserved" ($r1.domain -eq 'chatbot-qa')

    # 2. Field set + order matches the pinned fixture exactly.
    $fixKeys = ((Get-Content $fixture | Select-Object -First 1 | ConvertFrom-Json).PSObject.Properties.Name) -join ','
    $rowKeys = ($r1.PSObject.Properties.Name) -join ','
    Check "field set+order matches fixture" ($fixKeys -eq $rowKeys)

    # 3. couldnt_run is fail-closed — never reads as progress.
    & $writer -LoopId run-A -Domain chatbot-qa -Iteration 2 -MetricName pass_pct `
        -MetricBefore 0.91 -MetricAfter 0.99 -Verdict improved -OracleStatus couldnt_run `
        -LedgerPath $tmp | Out-Null
    $r2 = Get-Content $tmp | Select-Object -Last 1 | ConvertFrom-Json
    Check "couldnt_run nulls metric_after" ($null -eq $r2.metric_after)
    Check "couldnt_run nulls metric_delta" ($null -eq $r2.metric_delta)
    Check "couldnt_run forces verdict=couldnt_run" ($r2.verdict -eq 'couldnt_run')

    # 4. Sentinel domains are refused as production rows.
    $threw = $false
    try { & $writer -LoopId x -Domain __test__ -Iteration 1 -MetricName m -Verdict improved -LedgerPath $tmp 2>$null }
    catch { $threw = $true }
    Check "sentinel domain refused" $threw

    # 5. -AllowSentinel permits the seed row (graceful-degrade placeholder).
    & $writer -LoopId __seed__ -Domain __seed__ -Iteration 0 -MetricName none -Verdict improved `
        -AllowSentinel -LedgerPath $tmp | Out-Null
    $seed = Get-Content $tmp | Select-Object -Last 1 | ConvertFrom-Json
    Check "-AllowSentinel writes the seed" ($seed.loop_id -eq '__seed__')
}
finally {
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
}

if ($fail -gt 0) { Write-Host "loop-record verify: $fail FAILED" -ForegroundColor Red; exit 1 }
Write-Host "loop-record verify: all checks passed" -ForegroundColor Green
exit 0
