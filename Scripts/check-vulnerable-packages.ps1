#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fails when a project of AllProjects.slnx uses a vulnerable package that the baseline doesn't list.

.DESCRIPTION
    `dotnet list package --vulnerable` exits 0 even when it finds a vulnerable package. This script reads its JSON
    report, compares each package and advisory with .github/vulnerability-baseline.txt, and exits 1 on any pair the
    baseline doesn't list. A baseline line that is no longer found prints a warning, so it can go once its package is
    upgraded. On GitHub Actions the table of vulnerable packages also goes to the job summary.

    Run:
      pwsh Scripts/check-vulnerable-packages.ps1        (after dotnet restore AllProjects.slnx)

.PARAMETER Report
    A report from `dotnet list package --vulnerable --format json` to check instead of running the scan.

.PARAMETER Baseline
    The accepted vulnerable packages, one "<package id> <advisory URL>" per line.
#>

[CmdletBinding()]
param(
    [string]$Report,
    [string]$Baseline = (Join-Path (Split-Path $PSScriptRoot -Parent) '.github/vulnerability-baseline.txt')
)

$ErrorActionPreference = 'Stop'

if ($Report) {
    $json = Get-Content $Report -Raw
} else {
    $solution = Join-Path (Split-Path $PSScriptRoot -Parent) 'AllProjects.slnx'
    $json = dotnet list $solution package --vulnerable --include-transitive --format json | Out-String
    if ($LASTEXITCODE -ne 0) {
        Write-Host $json
        Write-Host "::error::dotnet list package failed with exit code $LASTEXITCODE"
        exit 1
    }
}
$scan = $json | ConvertFrom-Json
if ($scan.problems) {
    # Without a restore, or when a package source can't be reached, the report lists problems instead of packages
    $scan.problems | ForEach-Object { Write-Host "::error::$($_.project): $($_.text)" }
    exit 1
}

# '#' starts a comment
$known = @(Get-Content $Baseline | ForEach-Object { ($_ -replace '#.*$', '').Trim() } | Where-Object { $_ } |
    ForEach-Object { ($_ -split '\s+')[0..1] -join ' ' })

$found = @(foreach ($project in $scan.projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            foreach ($vulnerability in $package.vulnerabilities) {
                [pscustomobject]@{
                    Key      = "$($package.id) $($vulnerability.advisoryurl)"
                    Package  = "$($package.id) $($package.resolvedVersion)"
                    Severity = $vulnerability.severity
                    Advisory = $vulnerability.advisoryurl
                    Project  = [IO.Path]::GetFileNameWithoutExtension($project.path)
                }
            }
        }
    }
})
$groups = @($found | Group-Object Key | Sort-Object Name)
$new = @($groups | Where-Object Name -NotIn $known)
$gone = @($known | Where-Object { $_ -notin $groups.Name })

$summary = @('### Vulnerable packages', '', '| Package | Severity | Advisory | Projects | Baseline |', '|---|---|---|---|---|')
$summary += foreach ($group in $groups) {
    $first = $group.Group[0]
    $packages = ($group.Group.Package | Sort-Object -Unique) -join ', '
    $projects = @($group.Group.Project | Sort-Object -Unique).Count
    "| $packages | $($first.Severity) | $($first.Advisory) | $projects | $(if ($group.Name -in $known) { 'yes' } else { '**no**' }) |"
}
$summary | ForEach-Object { Write-Host $_ }
if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content $env:GITHUB_STEP_SUMMARY }

foreach ($key in $gone) {
    Write-Host "::warning::$key is no longer found: remove it from .github/vulnerability-baseline.txt"
}
foreach ($group in $new) {
    $first = $group.Group[0]
    $projects = ($group.Group.Project | Sort-Object -Unique) -join ', '
    Write-Host "::error::$($first.Package) has a $($first.Severity) vulnerability ($($first.Advisory)) in $projects. Upgrade it, or list it in .github/vulnerability-baseline.txt"
}
if ($new) { exit 1 }
Write-Host "$($groups.Count) package advisory(ies) found, all in the baseline"
