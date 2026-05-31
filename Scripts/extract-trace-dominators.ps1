# extract-trace-dominators.ps1 — derive canonical trace shapes from golden runs
#
# Reads golden-trace artifacts captured by record-golden-traces.ps1 and emits
# a per-prompt canonical-trace JSON capturing what every successful run had
# in common: the dominator step sequence and the invariant attributes.
#
# Output: state/quality/chatbot-qa/golden-traces/<prompt-slug>/_canonical.json
#
# Schema (v1):
#   {
#     "schemaVersion":   1,
#     "promptId":        "<slug>",
#     "runCount":        N,
#     "extractedAt":     "<UTC ISO>",
#     "canonicalSteps":  [
#       {
#         "name":             "orchestration.answer",
#         "status":           "completed",
#         "invariantAttributes": { "agent.id": "skill.modes", ... },
#         "rangeAttributes":    { "routing.confidence": { "min": 0.91, "max": 0.99 }, ... }
#       },
#       ...
#     ]
#   }
#
# Definitions:
#   - canonicalSteps  : step (name, status) pairs present in EVERY run, in the
#                       common order. Any step missing from one run is excluded.
#   - invariantAttributes : attribute keys whose value is identical across
#                       every run for that step.
#   - rangeAttributes : numeric attribute keys that vary; min/max captured.
#
# Usage:
#   pwsh Scripts/extract-trace-dominators.ps1                          # all prompts
#   pwsh Scripts/extract-trace-dominators.ps1 -Filter "modes"          # filter by category or slug
#   pwsh Scripts/extract-trace-dominators.ps1 -PromptDir what-is-locrian
#   pwsh Scripts/extract-trace-dominators.ps1 -MinRuns 2               # require >=2 golden runs to extract

[CmdletBinding()]
param(
    [string]$Root      = "state/quality/chatbot-qa/golden-traces",
    [string]$PromptDir = "",
    [string]$Filter    = "",
    [int]$MinRuns      = 1
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$rootPath = Join-Path $repoRoot $Root

if (-not (Test-Path $rootPath)) {
    Write-Error "Golden-trace root not found: $rootPath`nRun Scripts/record-golden-traces.ps1 first."
    exit 1
}

# ── Collect prompt directories to process ────────────────────────────────
function Get-PromptDirs {
    param([string]$Root, [string]$Filter, [string]$Single)

    if ($Single) {
        $p = Join-Path $Root $Single
        if (-not (Test-Path $p)) {
            Write-Error "Prompt directory not found: $p"
            exit 1
        }
        return @(Get-Item -LiteralPath $p)
    }

    $all = Get-ChildItem -Path $Root -Directory
    if ($Filter) {
        # Match against folder name OR the prompt/category stored in _meta.json
        $all = $all | Where-Object {
            $name = $_.Name
            $meta = Join-Path $_.FullName "_meta.json"
            $matched = $name -like "*$Filter*"
            if (-not $matched -and (Test-Path $meta)) {
                $m = Get-Content -LiteralPath $meta -Raw | ConvertFrom-Json
                if ($m.category -like "*$Filter*" -or $m.prompt -like "*$Filter*") {
                    $matched = $true
                }
            }
            return $matched
        }
    }
    return $all
}

# ── Load run files for a prompt directory ────────────────────────────────
function Get-RunArtifacts {
    param([string]$DirPath)

    Get-ChildItem -Path $DirPath -Filter "run-*.json" |
        Sort-Object Name |
        ForEach-Object {
            try {
                $content = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                [pscustomobject]@{
                    Path     = $_.FullName
                    Artifact = $content
                }
            }
            catch {
                Write-Warning "Failed to parse $($_.FullName): $($_.Exception.Message)"
                $null
            }
        } | Where-Object { $null -ne $_ }
}

# ── Extract canonical steps from N runs ──────────────────────────────────
function Get-CanonicalSteps {
    param([System.Collections.IEnumerable]$Runs)

    $runList = @($Runs)
    if ($runList.Count -eq 0) { return @() }

    # Pull steps from each run into a list-of-lists. PowerShell unwraps
    # single-element foreach output, so we build a List<object[]> explicitly
    # to keep array shape regardless of run count.
    $perRunSteps = New-Object 'System.Collections.Generic.List[object]'
    foreach ($r in $runList) {
        $steps = $r.Artifact.response.trace.steps
        if ($null -eq $steps) {
            $perRunSteps.Add(@()) | Out-Null
        } else {
            $perRunSteps.Add(@($steps)) | Out-Null
        }
    }

    # Step sequence intersection: a step (name, status) is canonical only if
    # it appears in every run in the SAME ordered position. Walk the shortest
    # sequence and verify each (name, status) pair matches across all runs.
    $shortestLen = [int]::MaxValue
    foreach ($run in $perRunSteps) {
        if ($run.Count -lt $shortestLen) { $shortestLen = $run.Count }
    }
    if ($shortestLen -eq [int]::MaxValue) { $shortestLen = 0 }

    $canonical = New-Object 'System.Collections.Generic.List[object]'

    for ($i = 0; $i -lt $shortestLen; $i++) {
        $refStep = $perRunSteps[0][$i]
        $allMatch = $true
        foreach ($run in $perRunSteps) {
            $s = $run[$i]
            if ($s.name -ne $refStep.name -or $s.status -ne $refStep.status) {
                $allMatch = $false
                break
            }
        }
        if (-not $allMatch) {
            # Shape diverged at this position — stop the canonical sequence.
            # Future enhancement: try longest-common-subsequence instead of
            # strict prefix match. v1 is conservative.
            break
        }

        # Collect attribute analysis across runs for this step position.
        $attrInvariants = [ordered]@{}
        $attrRanges     = [ordered]@{}

        $allAttrKeys = @{}
        foreach ($run in $perRunSteps) {
            $attrs = $run[$i].attributes
            if ($null -ne $attrs) {
                foreach ($prop in $attrs.PSObject.Properties) {
                    $allAttrKeys[$prop.Name] = $true
                }
            }
        }

        foreach ($key in $allAttrKeys.Keys) {
            $values = New-Object 'System.Collections.Generic.List[object]'
            $allPresent = $true
            foreach ($run in $perRunSteps) {
                $attrs = $run[$i].attributes
                if ($null -eq $attrs) { $allPresent = $false; break }
                $prop = $attrs.PSObject.Properties[$key]
                if ($null -eq $prop) { $allPresent = $false; break }
                $values.Add($prop.Value) | Out-Null
            }
            if (-not $allPresent) { continue }

            $unique = @($values | Select-Object -Unique)
            if ($unique.Count -eq 1) {
                $attrInvariants[$key] = $unique[0]
            }
            else {
                # Range only meaningful for numeric values.
                $allNumeric = $true
                $numericValues = New-Object 'System.Collections.Generic.List[double]'
                foreach ($v in $values) {
                    [double]$num = 0
                    if ([double]::TryParse([string]$v, [ref]$num)) {
                        $numericValues.Add($num) | Out-Null
                    } else {
                        $allNumeric = $false
                        break
                    }
                }
                if ($allNumeric -and $numericValues.Count -eq $values.Count) {
                    $attrRanges[$key] = [ordered]@{
                        min = ($numericValues | Measure-Object -Minimum).Minimum
                        max = ($numericValues | Measure-Object -Maximum).Maximum
                    }
                }
                # else: non-numeric variable attribute — intentionally not captured
                # (varies meaningfully run-to-run; not a useful invariant).
            }
        }

        $canonical.Add([ordered]@{
            name                 = $refStep.name
            status               = $refStep.status
            invariantAttributes  = $attrInvariants
            rangeAttributes      = $attrRanges
        }) | Out-Null
    }

    return $canonical.ToArray()
}

# ── Main loop ────────────────────────────────────────────────────────────
$dirs = Get-PromptDirs -Root $rootPath -Filter $Filter -Single $PromptDir

Write-Host "─── Trace dominator extraction ───" -ForegroundColor Cyan
Write-Host "Root        : $rootPath"
Write-Host "Prompt dirs : $($dirs.Count)"
Write-Host "MinRuns     : $MinRuns"

$extracted = 0
$skippedTooFew = 0
$failed = 0
$summary = @()

foreach ($dir in $dirs) {
    $runs = @(Get-RunArtifacts -DirPath $dir.FullName)
    if ($runs.Count -lt $MinRuns) {
        $skippedTooFew++
        continue
    }

    try {
        $canonical = Get-CanonicalSteps -Runs $runs

        $meta = $null
        $metaPath = Join-Path $dir.FullName "_meta.json"
        if (Test-Path $metaPath) {
            $meta = Get-Content -LiteralPath $metaPath -Raw | ConvertFrom-Json
        }

        $artifact = [ordered]@{
            schemaVersion   = 1
            promptId        = $dir.Name
            prompt          = if ($meta) { $meta.prompt } else { $null }
            category        = if ($meta) { $meta.category } else { $null }
            runCount        = $runs.Count
            extractedAt     = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ" -AsUTC)
            canonicalSteps  = $canonical
        }

        $outPath = Join-Path $dir.FullName "_canonical.json"
        $artifact | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outPath -Encoding UTF8

        # Also emit a pruned signature — just (name, status, agent.id) per
        # canonical step. Strips run-dependent values (routing.confidence,
        # response.length, traceId, etc.) so signatures are stable across
        # environments and can be committed as CI test fixtures.
        $signatureSteps = New-Object 'System.Collections.Generic.List[object]'
        foreach ($step in $canonical) {
            $agentId = $null
            if ($null -ne $step.invariantAttributes) {
                if ($step.invariantAttributes.Contains("agent.id")) {
                    $agentId = $step.invariantAttributes["agent.id"]
                }
            }
            $signatureSteps.Add([ordered]@{
                name    = $step.name
                status  = $step.status
                agentId = $agentId
            }) | Out-Null
        }

        $signature = [ordered]@{
            schemaVersion = 1
            promptId      = $dir.Name
            prompt        = if ($meta) { $meta.prompt } else { $null }
            category      = if ($meta) { $meta.category } else { $null }
            steps         = $signatureSteps.ToArray()
        }

        $sigPath = Join-Path $dir.FullName "_signature.json"
        $signature | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $sigPath -Encoding UTF8

        $extracted++
        $summary += [pscustomobject]@{
            promptId   = $dir.Name
            runs       = $runs.Count
            stepCount  = $canonical.Count
        }

        Write-Host "  ✓ $($dir.Name) — $($runs.Count) run(s), $($canonical.Count) canonical step(s)"
    }
    catch {
        $failed++
        Write-Host "  ✗ $($dir.Name): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "─── Done ───" -ForegroundColor Cyan
Write-Host "  Extracted        : $extracted"
Write-Host "  Skipped (too few): $skippedTooFew (had <$MinRuns runs)"
Write-Host "  Failed           : $failed"

# Cross-prompt summary — quick scan for outliers (very short canonicals
# often mean the trace shape diverged early, which is itself useful signal).
if ($summary.Count -gt 0) {
    Write-Host ""
    Write-Host "Canonical-step distribution:" -ForegroundColor Cyan
    $byStepCount = $summary | Group-Object stepCount | Sort-Object Name
    foreach ($g in $byStepCount) {
        Write-Host "  $($g.Count) prompt(s) with $($g.Name) step(s)"
    }
}
