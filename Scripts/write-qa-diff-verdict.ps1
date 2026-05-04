#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Write a Phase 0 QA diff verdict for the current repo or explicit paths.

.EXAMPLE
    pwsh Scripts/write-qa-diff-verdict.ps1

.EXAMPLE
    pwsh Scripts/write-qa-diff-verdict.ps1 -Path docs/contracts/foo.md,AllProjects.slnx -FailOnBlock
#>

param(
    [string]$RepoRoot,
    [string]$Repo = "guitar-alchemist/ga",
    [string]$TargetKind = "working_tree",
    [string]$TargetRef,
    [string]$StorageKey,
    [string]$BaseRef,
    [string]$HeadRef,
    [string]$Sha,
    [string]$BaseSha,
    [string]$OutputRoot,
    [string[]]$Path = @(),
    [switch]$ExcludeUntracked,
    [switch]$FailOnBlock
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$defaultRepoRoot = Split-Path -Parent $scriptDir
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = $defaultRepoRoot
}

$cliArgs = @()
$cliArgs += @("--repo-root", $RepoRoot)
$cliArgs += @("--repo", $Repo)
$cliArgs += @("--target-kind", $TargetKind)

if (-not [string]::IsNullOrWhiteSpace($TargetRef)) { $cliArgs += @("--target-ref", $TargetRef) }
if (-not [string]::IsNullOrWhiteSpace($StorageKey)) { $cliArgs += @("--storage-key", $StorageKey) }
if (-not [string]::IsNullOrWhiteSpace($BaseRef)) { $cliArgs += @("--base-ref", $BaseRef) }
if (-not [string]::IsNullOrWhiteSpace($HeadRef)) { $cliArgs += @("--head-ref", $HeadRef) }
if (-not [string]::IsNullOrWhiteSpace($Sha)) { $cliArgs += @("--sha", $Sha) }
if (-not [string]::IsNullOrWhiteSpace($BaseSha)) { $cliArgs += @("--base-sha", $BaseSha) }
if (-not [string]::IsNullOrWhiteSpace($OutputRoot)) { $cliArgs += @("--output-root", $OutputRoot) }
foreach ($item in $Path) {
    foreach ($expandedPath in ($item -split ",")) {
        if (-not [string]::IsNullOrWhiteSpace($expandedPath)) {
            $cliArgs += @("--path", $expandedPath.Trim())
        }
    }
}
if ($ExcludeUntracked) { $cliArgs += "--exclude-untracked" }
if ($FailOnBlock) { $cliArgs += "--fail-on-block" }

dotnet run --project (Join-Path $RepoRoot "Apps/GaQaCli/GaQaCli.csproj") -- @cliArgs
exit $LASTEXITCODE
