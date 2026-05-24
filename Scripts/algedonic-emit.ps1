# algedonic-emit.ps1 — emit a VSM algedonic signal into the cross-repo inbox.
#
# Contract: docs/contracts/2026-05-24-algedonic-channel.contract.md
# Schema:   docs/contracts/algedonic-signal.schema.json
# Inbox:    state/algedonic/inbox.jsonl  (append-only, one JSON object per line)
#
# Usage:
#   pwsh Scripts/algedonic-emit.ps1 -Severity warn -Repo ga -Source quality-snapshot `
#        -Summary "Voicing analysis pass_pct dropped to 0.91" `
#        -Details "Baseline 0.94 -> 0.91 over last 3 runs. See state/quality/voicing-analysis/." `
#        -EvidenceUrl "https://github.com/GuitarAlchemist/ga/actions/runs/12345" `
#        -AffectedArtifacts state/quality/voicing-analysis/2026-05-24.json
#
# Switches:
#   -Ack                        Emit an ack line for an existing signal (-Id required).
#   -AckBy <handle>             Acker (defaults to git config user.name or 'unknown').
#   -Resolution <text>          Short note describing the resolution.
#   -Supersedes <id1>,<id2>     IDs this signal replaces (hidden by the projector).
#   -TtlHours <int>             Time after acked_at when collector may purge. Default 24.
#   -RouteTo <enum>             Escalation route. Default 'operator'.
#   -OnUnackAfterHours <int>    Escalation trigger. Default depends on severity.
#   -DryRun                     Validate + print the JSON to stdout. Do not append.
#   -InboxPath <path>           Override inbox location. Default state/algedonic/inbox.jsonl.
#
# The schema is enforced inline — we don't shell out to a JSON Schema engine,
# we re-implement the required fields + enum constraints. The contract is the
# spec of record; this script is a producer-side guard so emitters can't write
# malformed lines that break the projector.

[CmdletBinding(DefaultParameterSetName = 'emit')]
param(
    [Parameter(ParameterSetName = 'emit', Mandatory = $true)]
    [ValidateSet('info', 'warn', 'fail', 'critical')]
    [string]$Severity,

    [Parameter(ParameterSetName = 'emit', Mandatory = $true)]
    [ValidateSet('ga', 'ix', 'demerzel', 'tars', 'sentrux', 'hari')]
    [string]$Repo,

    [Parameter(ParameterSetName = 'emit', Mandatory = $true)]
    [ValidateLength(1, 64)]
    [string]$Source,

    [Parameter(ParameterSetName = 'emit', Mandatory = $true)]
    [ValidateLength(1, 140)]
    [string]$Summary,

    [Parameter(ParameterSetName = 'emit')]
    [string]$Details = "",

    [Parameter(ParameterSetName = 'emit')]
    [string]$EvidenceUrl = "",

    [Parameter(ParameterSetName = 'emit')]
    [string[]]$AffectedArtifacts = @(),

    [Parameter(ParameterSetName = 'emit')]
    [ValidateRange(0, 8760)]
    [int]$TtlHours = 24,

    [Parameter(ParameterSetName = 'emit')]
    [ValidateSet('operator', 'council', 'qa-architect', 'on-call')]
    [string]$RouteTo = 'operator',

    [Parameter(ParameterSetName = 'emit')]
    [int]$OnUnackAfterHours = -1,

    [Parameter(ParameterSetName = 'emit')]
    [string[]]$Supersedes = @(),

    [Parameter(ParameterSetName = 'ack', Mandatory = $true)]
    [switch]$Ack,

    [Parameter(ParameterSetName = 'ack', Mandatory = $true)]
    [string]$Id,

    [Parameter(ParameterSetName = 'ack')]
    [string]$AckBy = "",

    [Parameter(ParameterSetName = 'ack')]
    [string]$Resolution = "",

    [switch]$DryRun,
    [string]$InboxPath = ""
)

$ErrorActionPreference = 'Stop'

# Resolve repo root + inbox path. Script lives at <repoRoot>/Scripts/.
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $InboxPath) {
    $InboxPath = Join-Path $repoRoot 'state/algedonic/inbox.jsonl'
}

# Helper: write a JSON line atomically. We use Add-Content with -Encoding UTF8.
# .NET's FileStream Append on small lines is line-atomic on every platform we
# support (Windows NTFS, Linux ext4); for larger payloads we'd need a lock
# file. Algedonic signals are by design < 4 KB so single-write append is safe.
function Add-InboxLine {
    param([string]$Path, [string]$JsonLine)

    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    # PowerShell's Add-Content adds a trailing newline; we want one line per
    # signal so this matches the contract exactly.
    Add-Content -Path $Path -Value $JsonLine -Encoding utf8NoBOM
}

# UUIDv7 generator — 48-bit timestamp + 4-bit version + 12-bit rand_a + 2-bit
# variant + 62-bit rand_b. PowerShell 7 ships [System.Random] which is good
# enough for our id space (we don't need cryptographic randomness here).
function New-Uuidv7 {
    $unixMs = [int64][Math]::Floor((Get-Date).ToUniversalTime().Subtract([datetime]'1970-01-01').TotalMilliseconds)
    $bytes = New-Object byte[] 16

    # First 6 bytes: 48-bit big-endian timestamp
    $bytes[0] = [byte](($unixMs -shr 40) -band 0xFF)
    $bytes[1] = [byte](($unixMs -shr 32) -band 0xFF)
    $bytes[2] = [byte](($unixMs -shr 24) -band 0xFF)
    $bytes[3] = [byte](($unixMs -shr 16) -band 0xFF)
    $bytes[4] = [byte](($unixMs -shr  8) -band 0xFF)
    $bytes[5] = [byte](($unixMs       ) -band 0xFF)

    # Bytes 6-15: random
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $randPart = New-Object byte[] 10
    $rng.GetBytes($randPart)
    for ($i = 0; $i -lt 10; $i++) { $bytes[$i + 6] = $randPart[$i] }

    # Set version (7) in high nibble of byte 6
    $bytes[6] = [byte](($bytes[6] -band 0x0F) -bor 0x70)
    # Set variant (10) in top 2 bits of byte 8
    $bytes[8] = [byte](($bytes[8] -band 0x3F) -bor 0x80)

    # Build the canonical 8-4-4-4-12 hex string directly. We don't go through
    # [Guid] because the ctor field order on Windows is little-endian for the
    # first three groups, which would mis-order our big-endian timestamp.
    $hex = ($bytes | ForEach-Object { $_.ToString('x2') }) -join ''
    return "$($hex.Substring(0,8))-$($hex.Substring(8,4))-$($hex.Substring(12,4))-$($hex.Substring(16,4))-$($hex.Substring(20,12))"
}

function Get-NowIso {
    return (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}

function Get-GitUserName {
    try {
        $name = (& git config user.name 2>$null)
        if ($LASTEXITCODE -eq 0 -and $name) { return $name.Trim() }
    } catch {}
    return 'unknown'
}

# ──────────────────────────────────────────────────────────────────────
# Build the signal object
# ──────────────────────────────────────────────────────────────────────

if ($PSCmdlet.ParameterSetName -eq 'ack') {
    # Ack signal — same id as the original, supersedes empty.
    # Per the contract, an ack is itself a new line; the projector takes the
    # latest line per id.
    if (-not $AckBy) { $AckBy = Get-GitUserName }

    # We need to reconstruct the *required* fields (severity, repo, source,
    # summary, details) since the schema is required-everywhere. Read the
    # latest line for this id from the inbox if present; otherwise refuse —
    # acking a non-existent id is almost certainly a typo.
    if (-not (Test-Path $InboxPath)) {
        Write-Error "Inbox not found at $InboxPath; cannot ack id '$Id' (no prior signal to look up)."
        exit 2
    }
    $lines = Get-Content -Path $InboxPath -Encoding utf8
    $found = $null
    foreach ($line in $lines) {
        $trim = $line.Trim()
        if (-not $trim) { continue }
        try {
            $obj = $trim | ConvertFrom-Json
            if ($obj.id -eq $Id) { $found = $obj }
        } catch { continue }
    }
    if (-not $found) {
        Write-Error "No signal with id='$Id' found in $InboxPath."
        exit 3
    }

    $signal = [ordered]@{
        id          = $Id
        schema      = 'algedonic-signal-v0.1.0'
        emitted_at  = Get-NowIso
        repo        = $found.repo
        source      = $found.source
        severity    = $found.severity
        summary     = $found.summary
        details     = $found.details
        evidence_url = $found.evidence_url
        affected_artifacts = @($found.affected_artifacts)
        ttl_hours   = $found.ttl_hours
        escalation  = $found.escalation
        ack         = [ordered]@{
            acked       = $true
            acked_by    = $AckBy
            acked_at    = Get-NowIso
            resolution  = if ($Resolution) { $Resolution } else { $null }
        }
        supersedes  = @()
    }
} else {
    $id = New-Uuidv7

    # OnUnackAfterHours is a typed [int] parameter, so it can't carry $null
    # directly. Use a separate $unackHours variable that CAN be $null, so the
    # serialized JSON gets `null` for info-severity signals (per contract §4).
    if ($OnUnackAfterHours -lt 0) {
        # Default escalation per severity, per contract §4.
        switch ($Severity) {
            'info'     { $unackHours = $null }
            'warn'     { $unackHours = 24 }
            'fail'     { $unackHours = 4 }
            'critical' { $unackHours = 1 }
        }
    } else {
        $unackHours = $OnUnackAfterHours
    }
    if ($Severity -eq 'info') { $unackHours = $null }

    $signal = [ordered]@{
        id          = $id
        schema      = 'algedonic-signal-v0.1.0'
        emitted_at  = Get-NowIso
        repo        = $Repo
        source      = $Source
        severity    = $Severity
        summary     = $Summary
        details     = $Details
        evidence_url = if ($EvidenceUrl) { $EvidenceUrl } else { $null }
        affected_artifacts = @($AffectedArtifacts)
        ttl_hours   = $TtlHours
        escalation  = [ordered]@{
            on_unack_after_hours = $unackHours
            route_to             = $RouteTo
        }
        ack         = [ordered]@{
            acked       = $false
            acked_by    = $null
            acked_at    = $null
            resolution  = $null
        }
        supersedes  = @($Supersedes)
    }
}

# ──────────────────────────────────────────────────────────────────────
# Validate (inline; mirrors algedonic-signal.schema.json constraints)
# ──────────────────────────────────────────────────────────────────────

function Test-Signal {
    param($Sig)
    $errors = @()

    if (-not $Sig.id -or $Sig.id.Length -gt 36) { $errors += "id missing or > 36 chars" }
    if ($Sig.id -notmatch '^[A-Za-z0-9_-]+$') { $errors += "id contains illegal characters" }
    if ($Sig.schema -ne 'algedonic-signal-v0.1.0') { $errors += "schema must be 'algedonic-signal-v0.1.0'" }
    if (-not $Sig.emitted_at) { $errors += "emitted_at required" }
    if ($Sig.repo -notin @('ga','ix','demerzel','tars','sentrux','hari')) { $errors += "repo invalid" }
    if (-not $Sig.source -or $Sig.source.Length -gt 64) { $errors += "source missing or > 64 chars" }
    if ($Sig.severity -notin @('info','warn','fail','critical')) { $errors += "severity invalid" }
    if (-not $Sig.summary -or $Sig.summary.Length -gt 140) { $errors += "summary missing or > 140 chars" }
    if ($null -eq $Sig.details) { $errors += "details required (empty string ok)" }
    if ($null -eq $Sig.affected_artifacts) { $errors += "affected_artifacts required ([] ok)" }
    if ($Sig.ttl_hours -lt 0 -or $Sig.ttl_hours -gt 8760) { $errors += "ttl_hours out of range" }
    if (-not $Sig.escalation) { $errors += "escalation required" }
    elseif ($Sig.escalation.route_to -notin @('operator','council','qa-architect','on-call')) { $errors += "escalation.route_to invalid" }
    if (-not $Sig.ack) { $errors += "ack required" }
    if ($null -eq $Sig.supersedes) { $errors += "supersedes required ([] ok)" }
    if ($Sig.ack.acked -eq $true) {
        if (-not $Sig.ack.acked_by) { $errors += "ack.acked_by required when acked=true" }
        if (-not $Sig.ack.acked_at) { $errors += "ack.acked_at required when acked=true" }
    }

    return $errors
}

$validationErrors = Test-Signal $signal
if ($validationErrors.Count -gt 0) {
    Write-Error "Algedonic signal failed validation:`n  - $($validationErrors -join "`n  - ")"
    exit 4
}

# ──────────────────────────────────────────────────────────────────────
# Emit (or dry-run print)
# ──────────────────────────────────────────────────────────────────────

# Compact JSON (no indentation; one line per signal as the contract requires).
$json = $signal | ConvertTo-Json -Depth 8 -Compress

if ($DryRun) {
    Write-Output $json
    exit 0
}

Add-InboxLine -Path $InboxPath -JsonLine $json

# Print the id so a caller (CI workflow, script) can reference it later.
Write-Output $signal.id
exit 0
