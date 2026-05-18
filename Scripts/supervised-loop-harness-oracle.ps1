# Supervised-loop harness oracle (ga-harness domain)
#
# Lightweight readiness check that emits the contract-shape last.json
# expected by Scripts/dev-process-overseer.ps1 + Scripts/supervised-loop-preflight.ps1.
#
# This is intentionally NOT the full oracle (`dotnet build AllProjects.slnx
# -c Debug && dotnet test AllProjects.slnx`) — that command runs inside
# Step 5 of the supervised-loop skill itself before any commit. The
# harness oracle only verifies that the kit files exist and parse cleanly.
#
# Usage:
#   pwsh Scripts/supervised-loop-harness-oracle.ps1
#   pwsh Scripts/supervised-loop-harness-oracle.ps1 -OutPath custom/last.json

[CmdletBinding()]
param(
    [string]$OutPath = 'state/quality/ga-harness/last.json'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

$problems = @()

# Required kit artifacts.
$required = @(
    'ga.loop-policy.json',
    'agent-blackbox.policy.json',
    'Scripts/dev-process-overseer.ps1',
    'Scripts/supervised-loop-preflight.ps1',
    '.claude/skills/supervised-loop/SKILL.md',
    'state/quality/ga-harness/baseline.json'
)
foreach ($p in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $p))) {
        $problems += "missing_$($p -replace '[/\\.]', '_')"
    }
}

# Loop policy parses.
$loopPolicy = $null
try {
    $loopPolicy = Get-Content -LiteralPath (Join-Path $root 'ga.loop-policy.json') -Raw | ConvertFrom-Json
} catch {
    $problems += 'loop_policy_unparseable'
}
if ($loopPolicy) {
    if (-not $loopPolicy.allow_edit -or $loopPolicy.allow_edit.Count -eq 0) { $problems += 'loop_policy_missing_allow_edit' }
    if (-not $loopPolicy.protected_paths -or $loopPolicy.protected_paths.Count -eq 0) { $problems += 'loop_policy_missing_protected_paths' }
}

$status = if ($problems.Count -eq 0) { 'ok' } else { 'fail' }
$metric = if ($problems.Count -eq 0) { 1.0 } else { 0.0 }

$last = [ordered]@{
    domain          = 'ga-harness'
    emitted_at      = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ' -AsUTC)
    metric_name     = 'harness_ready'
    metric_value    = $metric
    oracle_status   = $status
    oracle_command  = 'pwsh Scripts/supervised-loop-harness-oracle.ps1'
    summary         = if ($status -eq 'ok') {
        'Supervised-loop kit artifacts present and parseable.'
    } else {
        "Kit readiness check failed: $($problems -join ', ')"
    }
    problems        = @($problems)
}

$artifactPath = if ([System.IO.Path]::IsPathRooted($OutPath)) { $OutPath } else { Join-Path $root $OutPath }
$artifactDirectory = Split-Path -Parent $artifactPath
if ($artifactDirectory -and -not (Test-Path -LiteralPath $artifactDirectory)) {
    New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
}

$artifactJson = $last | ConvertTo-Json -Depth 6
$tempPath = "$artifactPath.tmp"
Set-Content -LiteralPath $tempPath -Value $artifactJson -Encoding UTF8
Move-Item -LiteralPath $tempPath -Destination $artifactPath -Force

Write-Host "[harness-oracle] status=$status metric=$metric out=$([System.IO.Path]::GetRelativePath($root, $artifactPath))"
if ($status -ne 'ok') { exit 1 }
exit 0
