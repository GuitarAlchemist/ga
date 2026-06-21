<#
.SYNOPSIS
    The Galactic-Protocol governance gate — the single authority for two questions
    every loop/skill/overseer in this repo asks before it acts:

      1. "Is this JSON valid against its cross-repo contract?"   → Test-Contract
      2. "Am I allowed to run a cycle right now?"                → Test-GovernanceGate

.DESCRIPTION
    Before this module, the HALT-ALL marker parse (schema_version / expires_at /
    exempt_agents / scope, fail-closed on unknown version) was copy-pasted across
    .claude/skills/auto-optimize, ga-chatbot-afk-harness, and Scripts/
    dev-process-overseer.ps1 — and the overseer only checked the *local* kill switch,
    so an operator who set ~/.demerzel/HALT-ALL got "loop eligible" from the very tool
    meant to be the unified gate. This module concentrates that load-bearing,
    fail-closed logic in one tested place. Every consumer crosses this seam instead of
    re-deriving it.

    Architecture decision: serving/ranking stays where it is (ADR-0001); this is
    governance plumbing, layer-5 orchestration only.
#>

Set-StrictMode -Version Latest

# ── The contract-validation primitive ────────────────────────────────────────

<#
.SYNOPSIS
    Validate a JSON value against a docs/contracts/*.schema.json (draft 2020-12),
    returning a structured verdict instead of throwing.
.OUTPUTS
    [pscustomobject] @{ Valid = [bool]; Errors = [string[]]; Version = [int?] }
    Version is the object's `schema_version` field if present (for fail-closed checks).
#>
function Test-Contract {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $SchemaPath,
        # JSON string OR an already-parsed object/hashtable.
        [Parameter(Mandatory)][AllowNull()] $Data
    )

    if (-not (Test-Path -LiteralPath $SchemaPath)) {
        return [pscustomobject]@{ Valid = $false; Errors = @("schema not found: $SchemaPath"); Version = $null }
    }

    $json = if ($Data -is [string]) { $Data } else { $Data | ConvertTo-Json -Depth 32 }

    # schema_version is extracted directly so callers can distinguish "unknown
    # version" (fail-closed) from other validation failures (malformed → fall through).
    $version = $null
    try {
        $parsed = $json | ConvertFrom-Json -ErrorAction Stop
        if ($parsed.PSObject.Properties.Name -contains 'schema_version') { $version = [int]$parsed.schema_version }
    } catch { }

    try {
        Test-Json -Json $json -SchemaFile $SchemaPath -ErrorAction Stop | Out-Null
        return [pscustomobject]@{ Valid = $true; Errors = @(); Version = $version }
    } catch {
        return [pscustomobject]@{ Valid = $false; Errors = @($_.Exception.Message); Version = $version }
    }
}

# ── The HALT-ALL marker location (one definition, was inlined per-consumer) ───

<#
.SYNOPSIS Resolve the per-user HALT-ALL marker path, cross-platform.
#>
function Get-HaltMarkerPath {
    [CmdletBinding()]
    param()
    $home = if ($env:USERPROFILE) { $env:USERPROFILE } else { $env:HOME }
    return (Join-Path $home '.demerzel/HALT-ALL')
}

# ── The governance gate ──────────────────────────────────────────────────────

<#
.SYNOPSIS
    Decide whether the calling agent may run a cycle right now, honoring BOTH the
    cross-repo HALT-ALL marker and the per-repo state/.loop-halted kill switch.
    Implements every consumer obligation from the overseer-halt-marker contract:
    opportunistic read, fail-closed on unknown schema_version, honor expires_at,
    honor exempt_agents, honor scope, surface the reason.
.OUTPUTS
    [pscustomobject] @{
        Allowed  = [bool]   # $true = may act
        Halted   = [bool]   # convenience = -not Allowed
        Source   = 'none' | 'halt-all' | 'loop-halted' | 'unknown-version'
        Reason   = [string]
        HaltedBy = [string]
        Exempt   = [bool]   # agent was exempt from HALT-ALL (a local halt can still apply)
    }
#>
function Test-GovernanceGate {
    [CmdletBinding()]
    param(
        [string] $AgentId   = 'unknown',
        [string] $RepoRoot  = (Resolve-Path .).Path,
        # Overridable for tests; defaults to the canonical per-user / per-repo paths.
        [string] $MarkerPath = (Get-HaltMarkerPath),
        [string] $LoopHaltedPath = (Join-Path $RepoRoot 'state/.loop-halted'),
        [string] $SchemaPath = (Join-Path $PSScriptRoot '../docs/contracts/overseer-halt-marker.schema.json')
    )

    function New-Verdict($allowed, $source, $reason, $haltedBy, $exempt) {
        [pscustomobject]@{
            Allowed = [bool]$allowed; Halted = -not [bool]$allowed
            Source = $source; Reason = $reason; HaltedBy = $haltedBy; Exempt = [bool]$exempt
        }
    }

    $localHalted = Test-Path -LiteralPath $LoopHaltedPath
    $exempt = $false

    # ── Cross-repo HALT-ALL marker (opportunistic) ──
    if (Test-Path -LiteralPath $MarkerPath) {
        $raw = $null
        try { $raw = Get-Content -LiteralPath $MarkerPath -Raw -ErrorAction Stop } catch { }

        if ($raw) {
            $obj = $null
            try { $obj = $raw | ConvertFrom-Json -ErrorAction Stop } catch { }

            if ($null -eq $obj) {
                # Unparseable marker → fall through to the local kill switch (obligation 1).
            }
            else {
                $declaredVersion = if ($obj.PSObject.Properties.Name -contains 'schema_version') { [int]$obj.schema_version } else { $null }

                if ($declaredVersion -ne 1) {
                    # Obligation 5: unknown schema_version → fail-closed (newer producer than us).
                    return New-Verdict $false 'unknown-version' "HALT-ALL has unknown schema_version=$declaredVersion (fail-closed)" $null $false
                }

                $contract = Test-Contract -SchemaPath $SchemaPath -Data $raw
                if (-not $contract.Valid) {
                    # Schema-version is fine but the body is malformed → treat as unparseable, fall through.
                }
                else {
                    # Obligation 2: honor expires_at (past → treat as absent).
                    $expired = $false
                    if ($obj.PSObject.Properties.Name -contains 'expires_at' -and $obj.expires_at) {
                        try { $expired = ([datetime]::Parse($obj.expires_at)).ToUniversalTime() -lt (Get-Date).ToUniversalTime() } catch { }
                    }

                    if (-not $expired) {
                        # Obligation 3: honor exempt_agents.
                        $exemptList = @()
                        if ($obj.PSObject.Properties.Name -contains 'exempt_agents' -and $obj.exempt_agents) { $exemptList = @($obj.exempt_agents) }
                        $exempt = $exemptList -contains $AgentId

                        # Obligation 4: honor scope (v1 honored set; others reserved = no-act).
                        $scope = if ($obj.PSObject.Properties.Name -contains 'scope' -and $obj.scope) { $obj.scope } else { 'loops-only' }
                        $scopeActs = $scope -in @('loops-only', 'loops-and-batch', 'global')

                        if (-not $exempt -and $scopeActs) {
                            $by = if ($obj.PSObject.Properties.Name -contains 'halted_by') { $obj.halted_by } else { $null }
                            return New-Verdict $false 'halt-all' $obj.reason $by $false
                        }
                    }
                }
            }
        }
    }

    # ── Per-repo kill switch (also blocks an exempt agent) ──
    if ($localHalted) {
        $reason = ''
        try { $reason = (Get-Content -LiteralPath $LoopHaltedPath -Raw -ErrorAction Stop).Trim() } catch { }
        return New-Verdict $false 'loop-halted' $reason $null $exempt
    }

    return New-Verdict $true 'none' $null $null $exempt
}

Export-ModuleMember -Function Test-Contract, Test-GovernanceGate, Get-HaltMarkerPath
