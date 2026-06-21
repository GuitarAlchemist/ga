#!/usr/bin/env pwsh
# verify-loop-decide.ps1 — proves Scripts/loop-decide.ps1 returns the right
# governance decision for each run shape, using the shared fixture.
#
#   pwsh state/quality/_fixtures/verify-loop-decide.ps1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo    = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$decide  = Join-Path $repo 'Scripts/loop-decide.ps1'
$fixture = Join-Path $PSScriptRoot 'loop-iterations.sample.jsonl'

# PlateauWindow=3 because the fixture's plateaued run has 3 cycles.
$expected = @{
    'test-improving'   = 'continue'
    'test-oscillating' = 'halt-oscillating'
    'test-misfiring'   = 'halt-misfire'
    'test-plateaued'   = 'stop-plateau'
}

$fail = $false
foreach ($id in $expected.Keys) {
    $out = & $decide -Domain '__test__' -LoopId $id -LedgerPath $fixture -PlateauWindow 3 | ConvertFrom-Json
    $want = $expected[$id]
    if ($out.decision -eq $want) {
        Write-Host "  PASS  $id -> $($out.decision)" -ForegroundColor Green
    } else {
        Write-Host "  FAIL  $id -> expected '$want', got '$($out.decision)' ($($out.reason))" -ForegroundColor Red
        $fail = $true
    }
}
if ($fail) { throw 'loop-decide decision mismatch' }
Write-Host 'OK: loop-decide governs all four run shapes correctly.' -ForegroundColor Green
