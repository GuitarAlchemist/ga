# Repo-local development process overseer.
#
# Reads loop baselines, kill switches, oracle output, and git state, then
# recommends whether Claude Code should continue, switch to /goal, run a /loop,
# or pause for operator attention.
#
# Usage:
#   pwsh Scripts/dev-process-overseer.ps1
#   pwsh Scripts/dev-process-overseer.ps1 -Domain chatbot-qa
#   pwsh Scripts/dev-process-overseer.ps1 -Json

[CmdletBinding()]
param(
    [string]$RepoRoot = (Resolve-Path .).Path,
    [string]$Domain = '',
    [switch]$Json,
    [string]$OutPath = 'state/governance/dev-process-overseer.json',
    [switch]$NoEmit
)

$ErrorActionPreference = 'Stop'

function Add-Finding {
    param(
        [System.Collections.Generic.List[object]]$Findings,
        [string]$Severity,
        [string]$Code,
        [string]$Message,
        [string]$Recommendation
    )

    $Findings.Add([ordered]@{
        severity       = $Severity
        code           = $Code
        message        = $Message
        recommendation = $Recommendation
    }) | Out-Null
}

function Read-JsonOrNull {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Convert-GitGlobToRegex {
    param([string]$Pattern)

    $normalized = ($Pattern -replace '\\', '/').Trim()
    $escaped = [regex]::Escape($normalized)
    $escaped = $escaped -replace '\\\*\\\*', '.*'
    $escaped = $escaped -replace '\\\*', '[^/]*'
    $escaped = $escaped -replace '\\\?', '[^/]'
    return "^$escaped$"
}

function Test-PathMatchesAnyPattern {
    param(
        [string]$Path,
        [object[]]$Patterns
    )

    $normalized = ($Path -replace '\\', '/').Trim()
    foreach ($pattern in @($Patterns)) {
        if (-not $pattern) { continue }
        $regex = Convert-GitGlobToRegex -Pattern ([string]$pattern)
        if ($normalized -match $regex) { return $true }
    }

    return $false
}

function Get-GitStatusEntries {
    param([string]$RepoRoot)

    $raw = & git -C $RepoRoot status --porcelain=v1 2>$null
    $entries = @()
    foreach ($line in @($raw)) {
        if (-not $line -or $line.Length -lt 4) { continue }

        $path = $line.Substring(3)
        if ($path -match ' -> ') {
            $path = ($path -split ' -> ')[-1]
        }

        $entries += [ordered]@{
            status = $line.Substring(0, 2)
            path   = ($path -replace '\\', '/')
        }
    }

    return $entries
}

function Get-BaselineFiles {
    param([string]$RepoRoot, [string]$Domain)

    $qualityRoot = Join-Path $RepoRoot 'state/quality'
    if (-not (Test-Path -LiteralPath $qualityRoot)) { return @() }

    if ($Domain) {
        $path = Join-Path $qualityRoot "$Domain/baseline.json"
        if (Test-Path -LiteralPath $path) { return @($path) }
        return @()
    }

    return @(Get-ChildItem -LiteralPath $qualityRoot -Recurse -Filter baseline.json -File -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName })
}

function Get-DomainFromBaselinePath {
    param([string]$RepoRoot, [string]$BaselinePath)

    $relative = [System.IO.Path]::GetRelativePath((Join-Path $RepoRoot 'state/quality'), $BaselinePath)
    return (($relative -replace '\\', '/') -split '/')[0]
}

$findings = [System.Collections.Generic.List[object]]::new()
$statusEntries = @(Get-GitStatusEntries -RepoRoot $RepoRoot)
$baselineFiles = @(Get-BaselineFiles -RepoRoot $RepoRoot -Domain $Domain)
$loopHaltedPath = Join-Path $RepoRoot 'state/.loop-halted'

$branch = ''
$headSha = ''
try { $branch = (& git -C $RepoRoot branch --show-current 2>$null) } catch { }
try { $headSha = (& git -C $RepoRoot rev-parse --short HEAD 2>$null) } catch { }

if (Test-Path -LiteralPath $loopHaltedPath) {
    Add-Finding $findings 'block' 'global-loop-halted' `
        'The global loop kill switch is active.' `
        'Do not start /loop or /goal automation until state/.loop-halted is intentionally cleared.'
}

if (-not $baselineFiles) {
    Add-Finding $findings 'warn' 'no-quality-baselines' `
        'No state/quality/*/baseline.json files were found for oversight.' `
        'Add a baseline.json with oracle, allow_edit, protected_paths, and stop-condition metadata before running autonomous loops.'
}

$domainReports = @()
foreach ($baselinePath in $baselineFiles) {
    $domainName = Get-DomainFromBaselinePath -RepoRoot $RepoRoot -BaselinePath $baselinePath
    $domainRoot = Split-Path -Parent $baselinePath
    $baseline = Read-JsonOrNull -Path $baselinePath
    $lockPath = Join-Path $domainRoot '.lock'
    $stopPath = Join-Path $domainRoot '.STOP'
    $lastPath = Join-Path $domainRoot 'last.json'
    $historyPath = Join-Path $domainRoot 'loop-history.jsonl'
    $last = Read-JsonOrNull -Path $lastPath

    $allowEdit = @()
    $protected = @()
    if ($baseline -and $baseline.scope_boundary) {
        $allowEdit = @($baseline.scope_boundary.allow_edit)
        $protected = @($baseline.scope_boundary.protected_paths)
    }

    $outOfScope = @()
    $protectedTouched = @()
    if ($allowEdit.Count -gt 0) {
        foreach ($entry in $statusEntries) {
            if ($entry.path -like 'state/quality/*' -or $entry.path -like 'state/digests/*') { continue }
            if (-not (Test-PathMatchesAnyPattern -Path $entry.path -Patterns $allowEdit)) {
                $outOfScope += $entry
            }
        }
    }

    if ($protected.Count -gt 0) {
        foreach ($entry in $statusEntries) {
            if (Test-PathMatchesAnyPattern -Path $entry.path -Patterns $protected) {
                $protectedTouched += $entry
            }
        }
    }

    $lock = $null
    if (Test-Path -LiteralPath $lockPath) {
        $lockItem = Get-Item -LiteralPath $lockPath
        $lock = [ordered]@{
            present       = $true
            lastWriteTime = $lockItem.LastWriteTime.ToString('o')
            ageMinutes    = [Math]::Round(((Get-Date) - $lockItem.LastWriteTime).TotalMinutes, 1)
            content       = (Get-Content -LiteralPath $lockPath -Raw).Trim()
        }

        if ($lock.ageMinutes -gt 90) {
            Add-Finding $findings 'warn' 'stale-loop-lock' `
                "Domain '$domainName' has a lock older than 90 minutes." `
                'Check whether the loop is still alive. If not, record the abort in loop-history.jsonl and clear the stale lock.'
        }
    } else {
        $lock = [ordered]@{ present = $false }
    }

    if (Test-Path -LiteralPath $stopPath) {
        Add-Finding $findings 'block' 'domain-stop-active' `
            "Domain '$domainName' has a .STOP sentinel." `
            "Do not run this domain's loop until $stopPath is intentionally removed."
    }

    $oracleStatus = $null
    $metricValue = $null
    $lastWriteTime = $null
    if (Test-Path -LiteralPath $lastPath) {
        $lastWriteTime = (Get-Item -LiteralPath $lastPath).LastWriteTime.ToString('o')
    }
    if ($last) {
        if ($last.PSObject.Properties.Name -contains 'oracle_status') { $oracleStatus = $last.oracle_status }
        if ($last.PSObject.Properties.Name -contains 'metric_value') { $metricValue = $last.metric_value }
    }

    if (-not $last) {
        Add-Finding $findings 'warn' 'missing-oracle-output' `
            "Domain '$domainName' has no readable last.json oracle output." `
            'Run the declared oracle once in supervised mode before enabling /loop or /goal.'
    } elseif ($null -eq $metricValue -or -not $oracleStatus) {
        Add-Finding $findings 'block' 'oracle-shape-invalid' `
            "Domain '$domainName' last.json is missing metric_value or oracle_status." `
            'Treat the oracle as unreliable. Keep Claude in supervised mode until the oracle emits the baseline contract shape.'
    } elseif ($oracleStatus -ne 'ok') {
        Add-Finding $findings 'block' 'oracle-not-ok' `
            "Domain '$domainName' oracle_status is '$oracleStatus'." `
            'Fix the oracle or environment before letting an optimization loop edit code.'
    }

    if ($outOfScope.Count -gt 0) {
        Add-Finding $findings 'block' 'dirty-outside-loop-scope' `
            "Domain '$domainName' has dirty files outside allow_edit." `
            'Isolate, stash, commit, or move unrelated changes before allowing Claude to commit loop work.'
    }

    if ($protectedTouched.Count -gt 0) {
        Add-Finding $findings 'block' 'protected-path-dirty' `
            "Domain '$domainName' has dirty protected paths." `
            'Stop the loop and require explicit human review before any commit.'
    }

    $recentHistory = @()
    if (Test-Path -LiteralPath $historyPath) {
        $recentHistory = @(Get-Content -LiteralPath $historyPath -Tail 5)
        foreach ($line in $recentHistory) {
            if ($line -match 'aborted-oracle-unreliable') {
                Add-Finding $findings 'warn' 'recent-oracle-abort' `
                    "Domain '$domainName' recently aborted because the oracle was unreliable." `
                    'Require one clean supervised oracle run before re-enabling unattended optimization.'
                break
            }
        }
    }

    $domainReports += [ordered]@{
        domain             = $domainName
        baselinePath       = [System.IO.Path]::GetRelativePath($RepoRoot, $baselinePath) -replace '\\', '/'
        lock               = $lock
        stopPresent        = (Test-Path -LiteralPath $stopPath)
        lastJsonPath       = if (Test-Path -LiteralPath $lastPath) { [System.IO.Path]::GetRelativePath($RepoRoot, $lastPath) -replace '\\', '/' } else { $null }
        lastJsonWriteTime  = $lastWriteTime
        oracleStatus       = $oracleStatus
        metricValue        = $metricValue
        outOfScopeDirty    = @($outOfScope)
        protectedDirty     = @($protectedTouched)
        recentHistoryLines = $recentHistory
    }
}

$blockCount = @($findings | Where-Object { $_.severity -eq 'block' }).Count
$warnCount = @($findings | Where-Object { $_.severity -eq 'warn' }).Count

$workflowMode = if ($blockCount -gt 0) {
    'pause'
} elseif ($warnCount -gt 0) {
    'supervised-goal'
} else {
    'loop-eligible'
}

$goalTemplate = @'
/goal The repo is safe for the current autonomous development cycle: the oracle command has been run and printed a valid metric_value with oracle_status ok; git status contains no dirty files outside the active loop allow_edit scope; protected paths are untouched; required build/test commands have been run and surfaced with exit code 0; or stop after 8 turns and summarize blockers.
'@.Trim()

$loopTemplate = @'
/loop 1h "Run the dev process overseer, then continue the active quality loop only if it reports loop-eligible; otherwise summarize blockers and stop."
'@.Trim()

$report = [ordered]@{
    schemaVersion   = '0.1'
    emittedAt       = (Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ' -AsUTC)
    repo            = [ordered]@{
        root   = $RepoRoot
        branch = $branch
        head   = $headSha
    }
    workflowMode    = $workflowMode
    counts          = [ordered]@{
        dirtyFiles = $statusEntries.Count
        blocks     = $blockCount
        warnings   = $warnCount
    }
    findings        = @($findings)
    domains         = @($domainReports)
    recommendations = [ordered]@{
        immediateAction = switch ($workflowMode) {
            'pause'           { 'Pause autonomous edits. Resolve block findings before allowing Claude to commit or continue a loop.' }
            'supervised-goal' { 'Use /goal with a bounded, verifiable condition and keep operator supervision on.' }
            default           { 'Eligible for scheduled /loop or /goal automation, subject to normal review gates.' }
        }
        claudeGoal = $goalTemplate
        claudeLoop = $loopTemplate
    }
}

$artifactPath = if ([System.IO.Path]::IsPathRooted($OutPath)) { $OutPath } else { Join-Path $RepoRoot $OutPath }
if (-not $NoEmit -and $artifactPath) {
    $artifactDirectory = Split-Path -Parent $artifactPath
    if ($artifactDirectory) {
        New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
    }

    $artifactJson = $report | ConvertTo-Json -Depth 12
    $temporaryPath = "$artifactPath.tmp"
    Set-Content -LiteralPath $temporaryPath -Value $artifactJson -Encoding UTF8
    Move-Item -LiteralPath $temporaryPath -Destination $artifactPath -Force
}

if ($Json) {
    $report | ConvertTo-Json -Depth 12
    exit 0
}

Write-Host "Development process overseer" -ForegroundColor Cyan
Write-Host ("Repo: {0} @ {1}" -f $branch, $headSha)
Write-Host ("Mode: {0}  Blocks: {1}  Warnings: {2}  Dirty files: {3}" -f $workflowMode, $blockCount, $warnCount, $statusEntries.Count)
Write-Host ''

if ($findings.Count -eq 0) {
    Write-Host 'No process findings.' -ForegroundColor Green
} else {
    foreach ($finding in $findings) {
        $color = if ($finding.severity -eq 'block') { 'Red' } elseif ($finding.severity -eq 'warn') { 'Yellow' } else { 'Gray' }
        Write-Host ("[{0}] {1}: {2}" -f $finding.severity.ToUpperInvariant(), $finding.code, $finding.message) -ForegroundColor $color
        Write-Host ("  -> {0}" -f $finding.recommendation)
    }
}

Write-Host ''
Write-Host 'Recommended Claude /goal:' -ForegroundColor Cyan
Write-Host $goalTemplate
Write-Host ''
Write-Host 'Recommended Claude /loop:' -ForegroundColor Cyan
Write-Host $loopTemplate
