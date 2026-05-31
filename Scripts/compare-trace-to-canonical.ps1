# compare-trace-to-canonical.ps1 — detect regressions vs canonical trace shape
#
# Reads a fresh chatbot trace (either a saved run-*.json artifact or a live
# response captured ad-hoc) and diffs it against the prompt's _canonical.json.
# Reports each kind of divergence with a stable exit code so this can wire
# into CI gates.
#
# Exit codes:
#   0 — fresh trace matches canonical (no regressions)
#   1 — regression detected (canonical step missing, status wrong, invariant
#       attribute mismatch, or routing.confidence outside range)
#   2 — usage error (canonical missing, fresh missing, etc.)
#
# Divergence types reported:
#   - MissingStep         canonical step absent from fresh trace
#   - StatusMismatch      step present but its status changed
#   - InvariantViolated   invariantAttribute value differs from canonical
#   - RangeViolated       rangeAttribute value outside captured [min, max]
#   - ExtraStep           fresh trace has steps beyond the canonical sequence
#                         (informational — not a hard regression by itself)
#
# Usage:
#   pwsh Scripts/compare-trace-to-canonical.ps1 `
#        -Canonical state/quality/chatbot-qa/golden-traces/what-is-locrian/_canonical.json `
#        -Fresh     state/quality/chatbot-qa/golden-traces/what-is-locrian/run-1.json
#
#   pwsh Scripts/compare-trace-to-canonical.ps1 `
#        -PromptSlug what-is-locrian `
#        -Fresh     /tmp/probe.json
#
# Sweep mode (compare each prompt's most recent run against its canonical):
#   pwsh Scripts/compare-trace-to-canonical.ps1 -Sweep

[CmdletBinding(DefaultParameterSetName = "Single")]
param(
    [Parameter(ParameterSetName = "Single")]
    [string]$Canonical = "",

    [Parameter(ParameterSetName = "Single")]
    [string]$PromptSlug = "",

    [Parameter(ParameterSetName = "Single")]
    [string]$Fresh = "",

    [Parameter(ParameterSetName = "Sweep")]
    [switch]$Sweep,

    [string]$Root = "state/quality/chatbot-qa/golden-traces"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$rootPath = Join-Path $repoRoot $Root

function Get-FreshTrace {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Fresh trace file not found: $Path"
    }

    $content = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json

    # Accept two shapes: a record-golden-traces.ps1 artifact (top-level
    # 'response' field) or a raw /chat response (top-level 'trace').
    if ($null -ne $content.response -and $null -ne $content.response.trace) {
        return @($content.response.trace.steps)
    }
    if ($null -ne $content.trace -and $null -ne $content.trace.steps) {
        return @($content.trace.steps)
    }
    throw "Cannot find trace.steps in $Path — expected either response.trace.steps or trace.steps"
}

function Compare-Trace {
    param(
        [Parameter(Mandatory)] [string]$CanonicalPath,
        [Parameter(Mandatory)] [string]$FreshPath
    )

    if (-not (Test-Path $CanonicalPath)) {
        throw "Canonical not found: $CanonicalPath"
    }

    $canonical = Get-Content -LiteralPath $CanonicalPath -Raw | ConvertFrom-Json
    $freshSteps = Get-FreshTrace -Path $FreshPath

    $divergences = New-Object 'System.Collections.Generic.List[object]'
    $canonicalSteps = @($canonical.canonicalSteps)

    for ($i = 0; $i -lt $canonicalSteps.Count; $i++) {
        $canonStep = $canonicalSteps[$i]
        if ($i -ge $freshSteps.Count) {
            $divergences.Add([pscustomobject]@{
                kind     = "MissingStep"
                position = $i
                detail   = "canonical expects '$($canonStep.name)' at position $i but fresh trace ended after $($freshSteps.Count) step(s)"
            }) | Out-Null
            continue
        }
        $freshStep = $freshSteps[$i]

        if ($freshStep.name -ne $canonStep.name) {
            $divergences.Add([pscustomobject]@{
                kind     = "MissingStep"
                position = $i
                detail   = "canonical expects '$($canonStep.name)' at position $i but fresh has '$($freshStep.name)'"
            }) | Out-Null
            # When the sequence diverges, downstream comparisons would all
            # be noise — stop the per-step diff here.
            break
        }

        if ($freshStep.status -ne $canonStep.status) {
            $divergences.Add([pscustomobject]@{
                kind     = "StatusMismatch"
                position = $i
                step     = $canonStep.name
                detail   = "canonical status '$($canonStep.status)' vs fresh '$($freshStep.status)'"
            }) | Out-Null
        }

        # Invariant attribute check
        if ($null -ne $canonStep.invariantAttributes) {
            foreach ($prop in $canonStep.invariantAttributes.PSObject.Properties) {
                $canonVal = $prop.Value
                $freshAttrs = $freshStep.attributes
                if ($null -eq $freshAttrs) {
                    $divergences.Add([pscustomobject]@{
                        kind     = "InvariantViolated"
                        position = $i
                        step     = $canonStep.name
                        attr     = $prop.Name
                        detail   = "expected '$canonVal' but fresh step has no attributes block"
                    }) | Out-Null
                    continue
                }
                $freshProp = $freshAttrs.PSObject.Properties[$prop.Name]
                if ($null -eq $freshProp) {
                    $divergences.Add([pscustomobject]@{
                        kind     = "InvariantViolated"
                        position = $i
                        step     = $canonStep.name
                        attr     = $prop.Name
                        detail   = "expected '$canonVal' but fresh trace doesn't carry this attribute"
                    }) | Out-Null
                    continue
                }
                if ("$($freshProp.Value)" -ne "$canonVal") {
                    $divergences.Add([pscustomobject]@{
                        kind     = "InvariantViolated"
                        position = $i
                        step     = $canonStep.name
                        attr     = $prop.Name
                        detail   = "expected '$canonVal' but fresh has '$($freshProp.Value)'"
                    }) | Out-Null
                }
            }
        }

        # Range attribute check
        if ($null -ne $canonStep.rangeAttributes) {
            foreach ($prop in $canonStep.rangeAttributes.PSObject.Properties) {
                $range = $prop.Value
                $freshAttrs = $freshStep.attributes
                if ($null -eq $freshAttrs) { continue }
                $freshProp = $freshAttrs.PSObject.Properties[$prop.Name]
                if ($null -eq $freshProp) { continue }

                [double]$freshNum = 0
                if (-not [double]::TryParse([string]$freshProp.Value, [ref]$freshNum)) {
                    continue
                }

                $min = [double]$range.min
                $max = [double]$range.max
                if ($freshNum -lt $min -or $freshNum -gt $max) {
                    $divergences.Add([pscustomobject]@{
                        kind     = "RangeViolated"
                        position = $i
                        step     = $canonStep.name
                        attr     = $prop.Name
                        detail   = "fresh value $freshNum outside canonical range [$min, $max]"
                    }) | Out-Null
                }
            }
        }
    }

    if ($freshSteps.Count -gt $canonicalSteps.Count) {
        $extra = $freshSteps[$canonicalSteps.Count..($freshSteps.Count - 1)] | ForEach-Object { $_.name }
        $divergences.Add([pscustomobject]@{
            kind     = "ExtraStep"
            position = $canonicalSteps.Count
            detail   = "fresh trace has $($freshSteps.Count - $canonicalSteps.Count) step(s) beyond canonical: $($extra -join ', ')"
        }) | Out-Null
    }

    return $divergences.ToArray()
}

# ── Mode dispatch ────────────────────────────────────────────────────────
if ($Sweep) {
    $dirs = Get-ChildItem -Path $rootPath -Directory
    $totalDiverged = 0
    $totalMatched  = 0
    $totalSkipped  = 0

    Write-Host "─── Trace-vs-canonical sweep ───" -ForegroundColor Cyan

    foreach ($dir in $dirs) {
        $canonPath = Join-Path $dir.FullName "_canonical.json"
        if (-not (Test-Path $canonPath)) {
            $totalSkipped++
            continue
        }

        # Use the most recent run-*.json as the fresh trace.
        $latestRun = Get-ChildItem -Path $dir.FullName -Filter "run-*.json" |
                     Sort-Object LastWriteTime -Descending |
                     Select-Object -First 1
        if ($null -eq $latestRun) {
            $totalSkipped++
            continue
        }

        $divs = Compare-Trace -CanonicalPath $canonPath -FreshPath $latestRun.FullName
        if ($divs.Count -eq 0) {
            $totalMatched++
        } else {
            $totalDiverged++
            $regressions = @($divs | Where-Object { $_.kind -ne "ExtraStep" })
            $extras      = @($divs | Where-Object { $_.kind -eq "ExtraStep" })
            $tag = if ($regressions.Count -gt 0) { "✗" } else { "⚠" }
            $color = if ($regressions.Count -gt 0) { "Red" } else { "Yellow" }
            Write-Host "  $tag $($dir.Name) — $($regressions.Count) regression(s), $($extras.Count) extra-step warning(s)" -ForegroundColor $color
            foreach ($d in $divs) {
                Write-Host "      [$($d.kind)] $($d.detail)" -ForegroundColor DarkGray
            }
        }
    }

    Write-Host ""
    Write-Host "─── Sweep complete ───" -ForegroundColor Cyan
    Write-Host "  Matched   : $totalMatched"
    Write-Host "  Diverged  : $totalDiverged"
    Write-Host "  Skipped   : $totalSkipped (no canonical)"

    if ($totalDiverged -gt 0) { exit 1 } else { exit 0 }
}

# Single mode — resolve canonical from PromptSlug if not given directly.
if (-not $Canonical -and $PromptSlug) {
    $Canonical = Join-Path $rootPath "$PromptSlug/_canonical.json"
}
if (-not $Canonical) {
    Write-Error "Provide -Canonical, -PromptSlug, or -Sweep."
    exit 2
}
if (-not $Fresh) {
    Write-Error "Provide -Fresh (path to a saved run JSON or chat response JSON)."
    exit 2
}

$divs = Compare-Trace -CanonicalPath $Canonical -FreshPath $Fresh

Write-Host "─── Trace comparison ───" -ForegroundColor Cyan
Write-Host "Canonical : $Canonical"
Write-Host "Fresh     : $Fresh"
Write-Host ""

if ($divs.Count -eq 0) {
    Write-Host "✓ No divergences — fresh trace matches canonical shape." -ForegroundColor Green
    exit 0
}

$regressions = @($divs | Where-Object { $_.kind -ne "ExtraStep" })
$extras      = @($divs | Where-Object { $_.kind -eq "ExtraStep" })

if ($regressions.Count -gt 0) {
    Write-Host "✗ $($regressions.Count) regression(s):" -ForegroundColor Red
    foreach ($d in $regressions) {
        Write-Host "    [$($d.kind)] $($d.detail)" -ForegroundColor Yellow
    }
}
if ($extras.Count -gt 0) {
    Write-Host "⚠ $($extras.Count) extra-step warning(s):" -ForegroundColor Yellow
    foreach ($d in $extras) {
        Write-Host "    [$($d.kind)] $($d.detail)" -ForegroundColor DarkGray
    }
}

if ($regressions.Count -gt 0) { exit 1 } else { exit 0 }
