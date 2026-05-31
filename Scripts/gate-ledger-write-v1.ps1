# Appends a v1 row to state/quality/gate-ledger.jsonl conforming to the
# unified Quality Gate Ledger contract:
#   ix/docs/contracts/2026-05-24-quality-gate-ledger.contract.md
#   ix/docs/contracts/quality-gate-ledger.schema.json
#
# This is the cross-repo successor to Scripts/gate-ledger-write.ps1 (which
# writes the legacy v0 chatbot-PR row). Both formats coexist in the same
# JSONL file — consumers branch on the presence of `schema_version`.
#
# Usage (typical — quality snapshotter):
#   pwsh Scripts/gate-ledger-write-v1.ps1 `
#       -Source chatbot-qa `
#       -Domain chatbot `
#       -Decision pass `
#       -MetricName pass_pct `
#       -MetricValue 94.0 `
#       -MetricThreshold 90.0 `
#       -EvidenceKind file `
#       -EvidenceRef state/quality/chatbot-qa/2026-05-24.json
#
# Degraded-environment example (decision=skip):
#   pwsh Scripts/gate-ledger-write-v1.ps1 `
#       -Source chatbot-qa `
#       -Domain chatbot `
#       -Decision skip `
#       -MetricName pass_pct `
#       -MetricValue 0 `
#       -ExtraJson '{"value_unknown":true,"degraded_reason":"backend_unavailable"}'
#
# Extra fields go in -ExtraJson (a literal JSON object). Use this for
# producer-specific context (PR number, run id, degraded reasons) that
# doesn't fit the standard fields. Consumers MUST ignore unknown keys.

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Domain,
    [Parameter(Mandatory=$true)]
    [ValidateSet('pass','fail','warn','skip')]
    [string]$Decision,
    [Parameter(Mandatory=$true)][string]$MetricName,
    [Parameter(Mandatory=$true)][double]$MetricValue,
    [double]$MetricThreshold,
    [ValidateSet('improving','stable','degrading','unknown')]
    [string]$MetricTrend,
    [ValidateSet('url','file','sha','run-id','pr')]
    [string]$EvidenceKind,
    [string]$EvidenceRef,
    [string]$RunAt,
    [string[]]$Supersedes,
    [string]$ExtraJson,
    [string]$RepoRoot = (Resolve-Path .).Path,
    [string]$LedgerPath
)

$ErrorActionPreference = 'Stop'

# UUID v7 is not native to PowerShell; we use a sortable timestamp-prefixed
# random UUID instead. Format matches `xxxxxxxx-xxxx-7xxx-xxxx-xxxxxxxxxxxx`
# so consumers that only check for v7-shape stay happy.
function New-UuidV7Like {
    # 48 bits of unix-ms timestamp + 4-bit version (7) + 12 random + 2-bit
    # variant + 62 random. We keep the byte layout v7-compatible.
    $ms = [BitConverter]::GetBytes([int64]([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()))
    [Array]::Reverse($ms)  # big-endian
    $tsBytes = $ms[2..7]   # 48-bit timestamp

    $rnd = New-Object byte[] 10
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($rnd)

    $bytes = New-Object byte[] 16
    [Array]::Copy($tsBytes, 0, $bytes, 0, 6)
    [Array]::Copy($rnd,     0, $bytes, 6, 10)

    # Version 7
    $bytes[6] = ($bytes[6] -band 0x0F) -bor 0x70
    # Variant 10xxxxxx
    $bytes[8] = ($bytes[8] -band 0x3F) -bor 0x80

    $hex = ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
    return "{0}-{1}-{2}-{3}-{4}" -f `
        $hex.Substring(0,8),  `
        $hex.Substring(8,4),  `
        $hex.Substring(12,4), `
        $hex.Substring(16,4), `
        $hex.Substring(20,12)
}

# Build the entry as an OrderedDictionary so JSON key order is stable.
$entry = [ordered]@{
    schema_version = 1
    schema         = 'quality-gate-ledger-v1'
    id             = New-UuidV7Like
    run_at         = if ($RunAt) { $RunAt } else { (Get-Date).ToUniversalTime().ToString('o') }
    source         = $Source
    domain         = $Domain
    decision       = $Decision
    metric         = [ordered]@{
        name  = $MetricName
        value = $MetricValue
    }
}

if ($PSBoundParameters.ContainsKey('MetricThreshold')) {
    $entry.metric.threshold = $MetricThreshold
}
if ($MetricTrend) {
    $entry.metric.trend = $MetricTrend
}
if ($EvidenceKind -and $EvidenceRef) {
    $entry.evidence = [ordered]@{
        kind = $EvidenceKind
        ref  = $EvidenceRef
    }
}
if ($Supersedes -and $Supersedes.Count -gt 0) {
    $entry.supersedes = @($Supersedes)
}
if ($ExtraJson) {
    try {
        $entry.extra = $ExtraJson | ConvertFrom-Json -AsHashtable
    } catch {
        Write-Warning "gate-ledger-write-v1: -ExtraJson did not parse as JSON; ignoring. Error: $_"
    }
}

$outDir = if ($LedgerPath) {
    Split-Path -Parent $LedgerPath
} else {
    Join-Path $RepoRoot 'state/quality'
}
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}
$outPath = if ($LedgerPath) { $LedgerPath } else { Join-Path $outDir 'gate-ledger.jsonl' }

# JSONL = one compact JSON object per line.
$line = $entry | ConvertTo-Json -Depth 6 -Compress
Add-Content -Path $outPath -Value $line -Encoding UTF8

Write-Host "gate ledger v1 row appended: $outPath" -ForegroundColor Green
Write-Host "  source=$Source domain=$Domain decision=$Decision metric=$MetricName=$MetricValue" -ForegroundColor DarkGray
