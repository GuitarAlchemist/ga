#!/usr/bin/env pwsh
# verify-loop-convergence.ps1 — proves build-views.sql's loop_convergence view
# classifies improving / oscillating / misfiring / plateaued runs correctly,
# WITHOUT running a real multi-hour loop.
#
# Method: drop the fixture into the production glob as loops/__test__.iterations.jsonl,
# run the REAL analytics/build-views.sql (no duplicated SQL), query loop_convergence
# for the __test__ domain, assert the four shapes, then clean up.
#
#   pwsh state/quality/_fixtures/verify-loop-convergence.ps1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$qualityDir = Split-Path $PSScriptRoot -Parent          # state/quality
$fixture    = Join-Path $PSScriptRoot 'loop-iterations.sample.jsonl'
$testFile   = Join-Path $qualityDir 'loops/__test__.iterations.jsonl'
$expected   = @{
    'test-improving'   = 'improving'
    'test-oscillating' = 'oscillating'
    'test-misfiring'   = 'misfiring'
    'test-plateaued'   = 'plateaued'
}

Copy-Item $fixture $testFile -Force
try {
    Push-Location $qualityDir
    try {
        $sql = (Get-Content 'analytics/build-views.sql' -Raw) +
               "`nSELECT loop_id || '=' || shape FROM loop_convergence WHERE domain = '__test__' ORDER BY loop_id;`n"
        $out = $sql | duckdb -noheader -list
    } finally { Pop-Location }

    $got = @{}
    foreach ($line in ($out -split "`r?`n" | Where-Object { $_ -match '=' })) {
        $k, $v = $line.Trim() -split '=', 2
        $got[$k] = $v
    }

    $fail = $false
    foreach ($id in $expected.Keys) {
        $want = $expected[$id]; $have = $got[$id]
        if ($have -eq $want) {
            Write-Host "  PASS  $id -> $have" -ForegroundColor Green
        } else {
            Write-Host "  FAIL  $id -> expected '$want', got '$have'" -ForegroundColor Red
            $fail = $true
        }
    }
    if ($fail) { throw 'loop_convergence classification mismatch' }
    Write-Host 'OK: loop_convergence classifies all four shapes correctly.' -ForegroundColor Green
}
finally {
    Remove-Item $testFile -ErrorAction SilentlyContinue
}
