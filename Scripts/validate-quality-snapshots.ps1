#requires -Version 7
<#
.SYNOPSIS
    Validate dashboard-envelope snapshots against the canonical schema — OPT-IN by the
    snapshot registry, not opt-out by filename. Reuses Test-Contract from Governance.psm1.

.DESCRIPTION
    The previous validator (PR #370) walked EVERY *.json under state/quality/ and checked
    it against the envelope schema, skipping only `_`-prefixed names — so 134 of 165 files
    false-failed because they were baselines, SCHEMA.json, SAE artifacts, and lens sidecars
    that were never dashboard envelopes. This validator inverts the selection: it reads
    state/quality/.snapshot-registry.json and validates ONLY the dated snapshot files in the
    registered envelope domains. Everything else is excluded by design, so a `FAIL` here is a
    REAL conformance gap (a registered producer that isn't emitting the envelope yet), not noise.

    Self-contained in ga: uses the vendored docs/contracts/quality-snapshot.schema.json — no
    cross-repo rust build (the old workflow checked out ix and built ix-quality-validate).

.PARAMETER Advisory
    Exit 0 even when registered snapshots fail (report-only). Default: exit 1 on any failure
    so the check can be a real merge gate once producers conform.
#>
[CmdletBinding()]
param(
    [string] $RepoRoot = (Resolve-Path .).Path,
    [switch] $Advisory
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Import-Module (Join-Path $PSScriptRoot 'Governance.psm1') -Force

$registryPath = Join-Path $RepoRoot 'state/quality/.snapshot-registry.json'
if (-not (Test-Path $registryPath)) {
    Write-Host "SNAPSHOT-VALIDATE: no registry at $registryPath" -ForegroundColor Red; exit 2
}
$registry = Get-Content $registryPath -Raw | ConvertFrom-Json
$schemaPath = Join-Path $RepoRoot $registry.envelope_schema
if (-not (Test-Path $schemaPath)) {
    Write-Host "SNAPSHOT-VALIDATE: envelope schema missing at $schemaPath" -ForegroundColor Red; exit 2
}

$pass = 0; $failures = @(); $domainsChecked = 0; $filesChecked = 0
foreach ($d in $registry.domains) {
    $dir = Join-Path $RepoRoot $d.dir
    if (-not (Test-Path $dir)) { continue }
    $domainsChecked++
    $files = @(Get-ChildItem -Path $dir -Filter $d.snapshot_glob -File -ErrorAction SilentlyContinue)
    foreach ($f in $files) {
        $filesChecked++
        $res = Test-Contract -SchemaPath $schemaPath -Data (Get-Content $f.FullName -Raw)
        if ($res.Valid) { $pass++ }
        else {
            $rel = [System.IO.Path]::GetRelativePath($RepoRoot, $f.FullName) -replace '\\', '/'
            $failures += [pscustomobject]@{ domain = $d.domain; file = $rel; error = ($res.Errors -join '; ') }
        }
    }
}

Write-Host ""
Write-Host "Dashboard-envelope snapshots (opt-in via .snapshot-registry.json)" -ForegroundColor Cyan
Write-Host "  domains registered : $($registry.domains.Count)  (checked: $domainsChecked)"
Write-Host "  snapshots validated: $filesChecked   pass: $pass   fail: $($failures.Count)"
if ($failures.Count -gt 0) {
    Write-Host "  Non-conforming (REAL gap — producer not emitting the envelope yet):" -ForegroundColor Yellow
    foreach ($x in $failures) {
        Write-Host ("    FAIL {0,-18} {1}" -f $x.domain, $x.file) -ForegroundColor Yellow
        Write-Host ("         {0}" -f $x.error) -ForegroundColor DarkGray
    }
}

if ($failures.Count -gt 0 -and -not $Advisory) {
    Write-Host "`nSNAPSHOT-VALIDATE: $($failures.Count) registered snapshot(s) fail the envelope schema." -ForegroundColor Red
    exit 1
}
Write-Host "`nSNAPSHOT-VALIDATE: ok ($filesChecked checked, $($failures.Count) non-conforming$(if($Advisory){' — advisory'}))." -ForegroundColor Green
exit 0
