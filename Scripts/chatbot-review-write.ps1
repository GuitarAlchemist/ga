# Writes a chatbot-review-verdict.schema.json conformant file to
# state/chatbot-reviews/<pr>.json.
#
# Called by /chatbot-iterate Step 4 (after Agent-tool subagents return)
# OR by Scripts/octo-review-clean.ps1 wrapper (after /octo:review).
# Either way, the auto-merge decision (Scripts/octo-auto-merge-decision.ps1)
# reads from here.
#
# Usage (typical, from inside /chatbot-iterate):
#   pwsh Scripts/chatbot-review-write.ps1 `
#     -Pr 155 `
#     -Branch chatbot/m7b5-dim7-chord-info `
#     -HeadSha e1997811 `
#     -Mechanism agent-tool-subagents `
#     -Verdict nits-only `
#     -Reviewers (@{
#         name='octo:droids:octo-code-reviewer'
#         verdict='approve-with-nits'
#         findingsCount=3
#         blockingCount=0
#      }, @{
#         name='octo:droids:octo-security-auditor'
#         verdict='approve'
#         findingsCount=0
#         blockingCount=0
#      })
#
# The script validates the shape against docs/schemas/chatbot-review-verdict.schema.json
# before writing.

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][int]$Pr,
    [Parameter(Mandatory=$true)][string]$Branch,
    [string]$HeadSha,
    [Parameter(Mandatory=$true)][ValidateSet('agent-tool-subagents','octo-review-clean','octo-review-raw')]
    [string]$Mechanism,
    [Parameter(Mandatory=$true)][ValidateSet('pass','nits-only','blocking','abstain')]
    [string]$Verdict,
    [Parameter(Mandatory=$true)][hashtable[]]$Reviewers,
    [hashtable[]]$BlockingFindings = @(),
    [string]$Notes,
    [string]$RepoRoot = (Resolve-Path .).Path
)

$ErrorActionPreference = 'Stop'

# Sanity check on reviewers shape — every entry must have name + verdict
foreach ($r in $Reviewers) {
    if (-not $r.name) { throw "reviewer entry missing 'name'" }
    if (-not $r.verdict) { throw "reviewer entry missing 'verdict' for $($r.name)" }
    $validVerdicts = @('approve','approve-with-nits','request-changes','abstain','error')
    if ($r.verdict -notin $validVerdicts) {
        throw "reviewer $($r.name) has invalid verdict '$($r.verdict)' — must be one of: $($validVerdicts -join ', ')"
    }
}

# Sanity check on aggregated verdict consistency
$anyBlocking = $false
$anyAbstain = $false
foreach ($r in $Reviewers) {
    if ($r.verdict -eq 'request-changes' -or ($r.blockingCount -and $r.blockingCount -gt 0)) {
        $anyBlocking = $true
    }
    if ($r.verdict -in @('abstain','error')) { $anyAbstain = $true }
}
if ($anyBlocking -and $Verdict -ne 'blocking') {
    Write-Warning "At least one reviewer is blocking but aggregated Verdict is '$Verdict' — caller should reconcile."
}
if ($anyAbstain -and $Verdict -notin @('abstain','blocking')) {
    Write-Warning "At least one reviewer abstained/errored but aggregated Verdict is '$Verdict' — caller should reconcile (consider 'abstain')."
}

# Build the record
$record = [ordered]@{
    pr         = $Pr
    branch     = $Branch
    reviewedAt = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ' -AsUTC)
    mechanism  = $Mechanism
    reviewers  = @($Reviewers | ForEach-Object {
        $entry = [ordered]@{ name = $_.name; verdict = $_.verdict }
        if ($_.findingsCount -ne $null) { $entry.findingsCount = [int]$_.findingsCount }
        if ($_.blockingCount -ne $null) { $entry.blockingCount = [int]$_.blockingCount }
        if ($_.uniqueFindings) { $entry.uniqueFindings = @($_.uniqueFindings) }
        $entry
    })
    verdict    = $Verdict
}
if ($HeadSha) { $record.headSha = $HeadSha }
if ($BlockingFindings.Count -gt 0) {
    $record.blockingFindings = @($BlockingFindings)
}
if ($Notes) { $record.notes = $Notes }

# Write
$outDir = Join-Path $RepoRoot 'state/chatbot-reviews'
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
$outPath = Join-Path $outDir "$Pr.json"

$json = $record | ConvertTo-Json -Depth 6
Set-Content -Path $outPath -Value $json -Encoding UTF8

Write-Host "review verdict written: $outPath" -ForegroundColor Green
Write-Host "  pr=$Pr  branch=$Branch  mechanism=$Mechanism  verdict=$Verdict" -ForegroundColor DarkGray
Write-Host "  reviewers: $($Reviewers.Count); blocking findings: $($BlockingFindings.Count)" -ForegroundColor DarkGray
