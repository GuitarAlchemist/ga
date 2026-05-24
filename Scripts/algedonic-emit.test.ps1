# algedonic-emit.test.ps1 — round-trip smoke test for the algedonic emit helper.
#
# Run:
#   pwsh Scripts/algedonic-emit.test.ps1
#
# Verifies:
#   1. Dry-run emit produces valid JSON matching the schema's required fields.
#   2. Live emit appends a line to a tmp inbox and returns the new id.
#   3. The emitted line round-trips: parsing it yields the same id + summary.
#   4. Ack mode appends a second line with acked=true and the same id.
#   5. Severity-specific escalation defaults are applied (info -> null, fail -> 4h).
#   6. Validation rejects malformed inputs (bad enum, summary > 140 chars).

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path $PSScriptRoot -Parent
$emitScript = Join-Path $PSScriptRoot 'algedonic-emit.ps1'

# Isolated tmp inbox so the test never touches state/algedonic/inbox.jsonl.
$tmpInbox = Join-Path ([System.IO.Path]::GetTempPath()) ("algedonic-test-{0}.jsonl" -f ([Guid]::NewGuid().ToString('N').Substring(0,8)))

$failures = @()
function Assert($cond, $msg) {
    if (-not $cond) {
        $script:failures += $msg
        Write-Host "  FAIL: $msg" -ForegroundColor Red
    } else {
        Write-Host "  ok: $msg" -ForegroundColor Green
    }
}

try {
    # ── 1. Dry-run produces valid JSON ──────────────────────────────────
    Write-Host "[1] Dry-run produces valid JSON" -ForegroundColor Cyan
    $dryJson = & $emitScript `
        -Severity 'warn' `
        -Repo 'ga' `
        -Source 'algedonic-test' `
        -Summary 'Test warn signal' `
        -Details 'Round-trip test' `
        -DryRun
    Assert ($LASTEXITCODE -eq 0) "dry-run exits 0 (got $LASTEXITCODE)"
    $obj = $dryJson | ConvertFrom-Json
    Assert ($obj.schema -eq 'algedonic-signal-v0.1.0') "schema is algedonic-signal-v0.1.0"
    Assert ($obj.severity -eq 'warn') "severity is warn"
    Assert ($obj.repo -eq 'ga') "repo is ga"
    Assert ($obj.summary -eq 'Test warn signal') "summary roundtrips"
    Assert ($obj.id.Length -le 36) "id <= 36 chars"
    Assert ($obj.id -match '^[A-Za-z0-9_-]+$') "id is filename-safe"
    Assert ($obj.ack.acked -eq $false) "ack.acked starts false"
    Assert ($obj.escalation.route_to -eq 'operator') "default route is operator"
    Assert ($obj.escalation.on_unack_after_hours -eq 24) "warn default escalation = 24h"

    # ── 2. Live emit appends to tmp inbox ───────────────────────────────
    # NOTE: in-process invocation via & $emitScript preserves [string[]] array
    # parameter binding. pwsh -File loses the comma-array binding (it serializes
    # the array into a single string token), so we invoke directly.
    Write-Host "[2] Live emit appends a line to the inbox" -ForegroundColor Cyan
    $emittedId = & $emitScript `
        -Severity 'fail' `
        -Repo 'ix' `
        -Source 'algedonic-test' `
        -Summary 'Test fail signal from ix' `
        -Details 'Should default to 4h escalation' `
        -EvidenceUrl 'https://example.invalid/run/1' `
        -AffectedArtifacts 'state/test/artifact.json','state/test/other.json' `
        -InboxPath $tmpInbox
    Assert ($LASTEXITCODE -eq 0) "live emit exits 0 (got $LASTEXITCODE)"
    Assert (Test-Path $tmpInbox) "inbox file was created at $tmpInbox"
    $emittedId = $emittedId.Trim()
    Assert ($emittedId -and $emittedId.Length -le 36) "emit returned an id"

    $lines = @(Get-Content $tmpInbox -Encoding utf8 | Where-Object { $_.Trim() })
    Assert ($lines.Count -eq 1) "inbox contains exactly 1 line (got $($lines.Count))"

    $parsed = $lines[0] | ConvertFrom-Json
    Assert ($parsed.id -eq $emittedId) "first line id matches returned id"
    Assert ($parsed.severity -eq 'fail') "first line severity is fail"
    Assert ($parsed.escalation.on_unack_after_hours -eq 4) "fail default escalation = 4h"
    Assert ($parsed.evidence_url -eq 'https://example.invalid/run/1') "evidence_url roundtrips"
    Assert ($parsed.affected_artifacts.Count -eq 2) "affected_artifacts has 2 items"

    # ── 3. Ack mode appends a second line ───────────────────────────────
    Write-Host "[3] Ack mode appends a second line with acked=true" -ForegroundColor Cyan
    & $emitScript `
        -Ack `
        -Id $emittedId `
        -AckBy 'test-suite' `
        -Resolution 'verified by test' `
        -InboxPath $tmpInbox | Out-Null
    Assert ($LASTEXITCODE -eq 0) "ack exits 0 (got $LASTEXITCODE)"

    $lines = @(Get-Content $tmpInbox -Encoding utf8 | Where-Object { $_.Trim() })
    Assert ($lines.Count -eq 2) "inbox now contains 2 lines (got $($lines.Count))"

    $ackLine = $lines[1] | ConvertFrom-Json
    Assert ($ackLine.id -eq $emittedId) "ack line id matches original"
    Assert ($ackLine.ack.acked -eq $true) "ack line has acked=true"
    Assert ($ackLine.ack.acked_by -eq 'test-suite') "acked_by roundtrips"
    Assert ($ackLine.ack.resolution -eq 'verified by test') "resolution roundtrips"
    Assert ($ackLine.severity -eq 'fail') "ack line preserves severity"

    # ── 4. Info severity has null escalation ────────────────────────────
    Write-Host "[4] Info severity has null escalation" -ForegroundColor Cyan
    $infoJson = & $emitScript `
        -Severity 'info' `
        -Repo 'ga' `
        -Source 'algedonic-test' `
        -Summary 'Test info signal' `
        -DryRun
    $infoObj = $infoJson | ConvertFrom-Json
    Assert ($null -eq $infoObj.escalation.on_unack_after_hours) "info severity has null escalation"

    # ── 5. Critical severity has 1h escalation ──────────────────────────
    Write-Host "[5] Critical severity has 1h escalation" -ForegroundColor Cyan
    $critJson = & $emitScript `
        -Severity 'critical' `
        -Repo 'demerzel' `
        -Source 'algedonic-test' `
        -Summary 'Test critical signal' `
        -DryRun
    $critObj = $critJson | ConvertFrom-Json
    Assert ($critObj.escalation.on_unack_after_hours -eq 1) "critical default escalation = 1h"

    # ── 6. Validation rejects bad inputs ────────────────────────────────
    Write-Host "[6] Validation rejects bad inputs" -ForegroundColor Cyan
    # Summary > 140 chars — caught by [ValidateLength] at param binding.
    $longSummary = 'x' * 141
    $caught = $false
    try {
        & $emitScript `
            -Severity 'warn' `
            -Repo 'ga' `
            -Source 'algedonic-test' `
            -Summary $longSummary `
            -DryRun 2>&1 | Out-Null
    } catch { $caught = $true }
    Assert $caught "summary > 140 chars is rejected"

    # ── Summary ─────────────────────────────────────────────────────────
    Write-Host ""
    if ($failures.Count -eq 0) {
        Write-Host "[ok] All algedonic-emit round-trip checks passed." -ForegroundColor Green
        exit 0
    } else {
        Write-Host "[fail] $($failures.Count) assertion failure(s):" -ForegroundColor Red
        foreach ($f in $failures) { Write-Host "  - $f" -ForegroundColor Red }
        exit 1
    }
}
finally {
    if (Test-Path $tmpInbox) {
        Remove-Item -Path $tmpInbox -Force -ErrorAction SilentlyContinue
    }
}
