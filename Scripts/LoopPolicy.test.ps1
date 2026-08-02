# LoopPolicy.test.ps1 — pins the GA AFK policy/readiness contract.
#
# Run:
#   pwsh Scripts/LoopPolicy.test.ps1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$modulePath = Join-Path $PSScriptRoot 'LoopPolicy.psm1'
Import-Module $modulePath -Force

$policyPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'ga.loop-policy.json'
$policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
$failures = @()

function Assert {
    param([bool]$Condition, [string]$Message)

    if ($Condition) {
        Write-Host "  ok: $Message" -ForegroundColor Green
        return
    }

    $script:failures += $Message
    Write-Host "  FAIL: $Message" -ForegroundColor Red
}

function Copy-Policy {
    param([object]$Value)

    return $Value | ConvertTo-Json -Depth 20 | ConvertFrom-Json
}

Write-Host 'Canonical policy:' -ForegroundColor Cyan
$result = Test-GaLoopPolicy -Policy $policy
Assert $result.Valid 'checked-in policy satisfies the AFK readiness contract'
Assert ($policy.allow_edit -contains 'Common/GA.Business.ML/Agents/Skills/**') 'skills source seam is editable'
Assert ($policy.allow_edit -contains 'Common/GA.Business.ML/Agents/Mcp/**') 'MCP source seam is editable'
Assert ($policy.allow_edit -contains 'Tests/Common/GA.Business.ML.Tests/Unit/*SkillTests.cs') 'skill tests can accompany skill changes'
Assert ($policy.allow_edit -contains 'Tests/Common/GA.Business.ML.Tests/Unit/*McpToolsTests.cs') 'MCP tests can accompany MCP changes'
$cloudValidateBytes = [System.IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'cloud-validate.sh'))
Assert (-not ($cloudValidateBytes -contains 13)) 'L2 cloud validator is LF-only for local bash portability'
Assert ($policy.verification_levels.L2.platform_commands.windows -contains 'pwsh Scripts/cloud-validate.ps1') 'L2 declares its Windows entry point'

Write-Host 'Fail-closed variants:' -ForegroundColor Cyan
$broad = Copy-Policy $policy
$broad.allow_edit = @($broad.allow_edit) + 'Tests/**'
$result = Test-GaLoopPolicy -Policy $broad
Assert ((-not $result.Valid) -and ($result.Problems -contains 'allow_edit_too_broad_Tests/**')) 'broad Tests/** permission is rejected'

$missingL2 = Copy-Policy $policy
$missingL2.verification_levels.PSObject.Properties.Remove('L2')
$result = Test-GaLoopPolicy -Policy $missingL2
Assert ((-not $result.Valid) -and ($result.Problems -contains 'verification_level_missing_L2')) 'all L0-L3 levels are mandatory'

$wrongCloudCommand = Copy-Policy $policy
$wrongCloudCommand.verification_levels.L2.commands = @('echo smoke')
$result = Test-GaLoopPolicy -Policy $wrongCloudCommand
Assert ((-not $result.Valid) -and ($result.Problems -contains 'verification_L2_missing_cloud_validate')) 'L2 is pinned to cloud-validate.sh'

$selfReview = Copy-Policy $policy
$selfReview.independent_review.author_comment_satisfies = $true
$result = Test-GaLoopPolicy -Policy $selfReview
Assert ((-not $result.Valid) -and ($result.Problems -contains 'independent_review_allows_author_comment')) 'an author comment cannot satisfy independent review'

$localPostflight = Copy-Policy $policy
$localPostflight.postflight.provider = 'ga-local-copy'
$result = Test-GaLoopPolicy -Policy $localPostflight
Assert ((-not $result.Valid) -and ($result.Problems -contains 'postflight_provider_not_agent_blackbox')) 'GA must consume Agent Blackbox postflight'

if ($failures.Count -gt 0) {
    Write-Host "`n$($failures.Count) failure(s)." -ForegroundColor Red
    exit 1
}

Write-Host "`nAll loop-policy assertions passed." -ForegroundColor Green
