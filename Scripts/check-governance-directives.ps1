# Reads state/governance/directives.json (Tier 2 Demerzel integration) and
# determines whether /chatbot-iterate's current iteration should proceed.
#
# Schema: docs/schemas/governance-directives.schema.json.
#
# Called by /chatbot-iterate Step 0 alongside the killswitch sentinel check.
# Composition: ANY block-class directive that targets the current scope is
# sufficient to refuse the iteration. Advisory directives surface a warning
# but don't refuse.
#
# Exit codes:
#   0 = clear (no directives, or only advisories — proceed)
#   2 = blocked (one or more directives target the current scope)
#   3 = file present but invalid (schema violation or unparseable JSON)
#
# Usage from /chatbot-iterate:
#   pwsh Scripts/check-governance-directives.ps1 -Slug memory-session-scope -Json
#
# Stand-alone:
#   pwsh Scripts/check-governance-directives.ps1            # check whole-track
#   pwsh Scripts/check-governance-directives.ps1 -Slug X    # check one slug

[CmdletBinding()]
param(
    [string]$Slug,
    [int]$Pr,
    [switch]$Json,
    [string]$RepoRoot = (Resolve-Path .).Path,
    [int]$StaleDays = 7
)

$ErrorActionPreference = 'Stop'

$directivesFile = Join-Path $RepoRoot 'state/governance/directives.json'

function Emit {
    param([int]$ExitCode, [string]$State, [string]$Reason, [array]$Blocks, [array]$Advisories)
    if ($Json) {
        $obj = [ordered]@{
            state       = $State
            reason      = $Reason
            blocks      = $Blocks
            advisories  = $Advisories
            exitCode    = $ExitCode
        }
        $obj | ConvertTo-Json -Depth 5 -Compress
    } else {
        $colour = switch ($State) {
            'clear'   { 'Green' }
            'blocked' { 'Red' }
            'invalid' { 'Magenta' }
            default   { 'Yellow' }
        }
        Write-Host ''
        Write-Host "Governance directives: $State" -ForegroundColor $colour
        if ($Reason) { Write-Host "  $Reason" -ForegroundColor DarkGray }
        if ($Blocks -and $Blocks.Count -gt 0) {
            Write-Host '  Blocking:' -ForegroundColor Red
            foreach ($b in $Blocks) {
                Write-Host ("    [{0}] {1}" -f $b.type, $b.reason) -ForegroundColor Red
                if ($b.verdictId) { Write-Host ("      verdict: {0}" -f $b.verdictId) -ForegroundColor DarkGray }
            }
        }
        if ($Advisories -and $Advisories.Count -gt 0) {
            Write-Host '  Advisories:' -ForegroundColor Yellow
            foreach ($a in $Advisories) {
                Write-Host ("    {0}" -f $a.reason) -ForegroundColor Yellow
            }
        }
        Write-Host ''
    }
    exit $ExitCode
}

# No file present = no directives = clear
if (-not (Test-Path $directivesFile)) {
    Emit 0 'clear' 'No state/governance/directives.json present.' @() @()
}

# Parse + validate basic shape
$doc = $null
try { $doc = Get-Content $directivesFile -Raw | ConvertFrom-Json } catch { }
if (-not $doc) {
    Emit 3 'invalid' "Couldn't parse $directivesFile as JSON." @() @()
}
if (-not $doc.schemaVersion -or -not $doc.directives) {
    Emit 3 'invalid' 'Directives file missing required fields (schemaVersion, directives).' @() @()
}

# Staleness sanity check — refuse stale files
if ($doc.issuedAt) {
    try {
        $issued = [datetime]::Parse($doc.issuedAt).ToUniversalTime()
        $ageDays = ((Get-Date).ToUniversalTime() - $issued).TotalDays
        if ($ageDays -gt $StaleDays) {
            Emit 0 'clear' ("Directives file is {0:N1} days old (> {1}-day stale cutoff). Treating as unset." -f $ageDays, $StaleDays) @() @()
        }
    } catch { }
}

# Apply each directive
$now = (Get-Date).ToUniversalTime()
$blocks = @()
$advisories = @()
foreach ($d in $doc.directives) {
    # Expiry check
    if ($d.expiresAt) {
        try {
            $exp = [datetime]::Parse($d.expiresAt).ToUniversalTime()
            if ($now -gt $exp) { continue }
        } catch { }
    }

    # Does this directive target the current scope?
    $targets = $false
    if ($d.scope.chatbotTrack -eq $true) { $targets = $true }
    if ($Slug -and $d.scope.slug -eq $Slug) { $targets = $true }
    if ($Pr -gt 0 -and $d.scope.pr -eq $Pr) { $targets = $true }
    if (-not $targets) { continue }

    # Classify
    if ($d.type -eq 'advisory') {
        $advisories += $d
    } else {
        $blocks += $d
    }
}

if ($blocks.Count -gt 0) {
    $reason = ("{0} blocking directive(s) target this iteration scope." -f $blocks.Count)
    Emit 2 'blocked' $reason $blocks $advisories
}

if ($advisories.Count -gt 0) {
    Emit 0 'clear' ("{0} advisory directive(s); proceeding with warning." -f $advisories.Count) @() $advisories
}

Emit 0 'clear' 'No directives target this iteration scope.' @() @()
