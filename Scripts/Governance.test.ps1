# Governance.test.ps1 — pins the contract-validation + governance-gate semantics.
#
# Run:
#   pwsh Scripts/Governance.test.ps1
#
# Covers the load-bearing, fail-closed behaviours that were previously copy-pasted
# (and untestable) inside SKILL.md prose:
#   - Test-Contract: valid passes, bad const fails, missing-required fails.
#   - Test-GovernanceGate: clean → allowed; local kill switch → halted; valid marker →
#     halted; expired marker → falls through; exempt agent → not marker-halted;
#     UNKNOWN schema_version → fail-closed halt; unparseable marker → falls through.

[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'Governance.psm1') -Force
$schema = Join-Path $PSScriptRoot '../docs/contracts/overseer-halt-marker.schema.json'

$failures = @()
function Assert($cond, $msg) {
    if (-not $cond) { $script:failures += $msg; Write-Host "  FAIL: $msg" -ForegroundColor Red }
    else { Write-Host "  ok: $msg" -ForegroundColor Green }
}

# Isolated sandbox so the test never touches the real ~/.demerzel or state/.
$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("gov-test-{0}" -f ([Guid]::NewGuid().ToString('N').Substring(0,8)))
$repoRoot = Join-Path $sandbox 'repo'
New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot 'state') | Out-Null
$marker = Join-Path $sandbox 'HALT-ALL'
$loopHalted = Join-Path $repoRoot 'state/.loop-halted'

function Gate([string]$agent = 'auto-optimize') {
    Test-GovernanceGate -AgentId $agent -RepoRoot $repoRoot -MarkerPath $marker -LoopHaltedPath $loopHalted -SchemaPath $schema
}
function Set-Marker($obj) { $obj | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $marker -Encoding utf8 }
function Clear-State { Remove-Item $marker, $loopHalted -ErrorAction SilentlyContinue }

try {
    Write-Host "Test-Contract:" -ForegroundColor Cyan
    $valid = @{ schema_version = 1; halted_at = '2026-05-16T16:30:00Z'; reason = 'x' }
    Assert (Test-Contract -SchemaPath $schema -Data $valid).Valid 'valid marker passes'
    Assert (-not (Test-Contract -SchemaPath $schema -Data @{ schema_version = 2; halted_at = '2026-05-16T16:30:00Z'; reason = 'x' }).Valid) 'bad const (version 2) fails'
    Assert (-not (Test-Contract -SchemaPath $schema -Data @{ schema_version = 1; reason = 'no ts' }).Valid) 'missing required (halted_at) fails'
    Assert ((Test-Contract -SchemaPath $schema -Data $valid).Version -eq 1) 'Version extracted from body'

    Write-Host "Test-GovernanceGate:" -ForegroundColor Cyan
    Clear-State
    Assert (Gate).Allowed 'clean (no marker, no kill switch) → allowed'

    Clear-State
    'cost spike' | Set-Content -LiteralPath $loopHalted -Encoding utf8
    $v = Gate
    Assert ((-not $v.Allowed) -and $v.Source -eq 'loop-halted') 'local .loop-halted → halted (loop-halted)'

    Clear-State
    Set-Marker @{ schema_version = 1; halted_at = '2026-05-16T16:30:00Z'; reason = 'freeze'; scope = 'loops-only'; halted_by = 'operator:test' }
    $v = Gate
    Assert ((-not $v.Allowed) -and $v.Source -eq 'halt-all' -and $v.Reason -eq 'freeze') 'valid HALT-ALL → halted (halt-all) with reason'

    Clear-State
    Set-Marker @{ schema_version = 1; halted_at = '2026-05-16T16:30:00Z'; reason = 'old'; expires_at = '2020-01-01T00:00:00Z' }
    Assert (Gate).Allowed 'expired marker → falls through → allowed'

    Clear-State
    Set-Marker @{ schema_version = 1; halted_at = '2026-05-16T16:30:00Z'; reason = 'all but me'; exempt_agents = @('auto-optimize') }
    $v = Gate 'auto-optimize'
    Assert ($v.Allowed -and $v.Exempt) 'exempt agent → allowed + Exempt flag'
    $v = Gate 'some-other-agent'
    Assert (-not $v.Allowed) 'non-exempt agent still halted by same marker'

    Clear-State
    Set-Marker @{ schema_version = 99; halted_at = '2026-05-16T16:30:00Z'; reason = 'future' }
    $v = Gate
    Assert ((-not $v.Allowed) -and $v.Source -eq 'unknown-version') 'UNKNOWN schema_version → fail-closed halt'

    Clear-State
    'not json {{{' | Set-Content -LiteralPath $marker -Encoding utf8
    Assert (Gate).Allowed 'unparseable marker, no kill switch → falls through → allowed'
    'kill' | Set-Content -LiteralPath $loopHalted -Encoding utf8
    Assert ((Gate).Source -eq 'loop-halted') 'unparseable marker falls through TO the local kill switch'
}
finally {
    Remove-Item -Recurse -Force $sandbox -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0) {
    Write-Host "`n$($failures.Count) failure(s)." -ForegroundColor Red
    exit 1
}
Write-Host "`nAll governance-gate assertions passed." -ForegroundColor Green
