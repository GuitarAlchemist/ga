<#
.SYNOPSIS
    Bridge from a Claude Code session (terminal or extension) into the running Antigravity IDE.

.DESCRIPTION
    Wraps the Antigravity CLI (a VS Code-fork CLI at bin/antigravity) so a
    finished Claude Code task can hand off visual artifacts to the IDE:

      - jump the active editor to a file:line that the agent just edited
      - open a diff in Antigravity for human review
      - add a sibling repo to the active workspace
      - drop a structured handoff note under state/handoffs/ that Antigravity
        native AI can pick up by reading the workspace

    No state is shared between the two AIs other than the file system. That
    is the integration: cheap, reliable, no API surface to fight.

.PARAMETER Goto
    Path to a file (with optional :line:char). Example:
      -Goto Apps/GaQaMcp/Tools/QaTools.cs:97:13

.PARAMETER Diff
    Two paths separated by a comma. Example:
      -Diff "old.cs,new.cs"

.PARAMETER AddFolder
    Add a folder to the currently active Antigravity window.

.PARAMETER Handoff
    A short string describing what just landed. Writes a timestamped note
    under state/handoffs/ that the other AI surface can read.

.EXAMPLE
    pwsh Scripts/antigravity-bridge.ps1 -Goto "Apps/GaQaMcp/Tools/QaTools.cs:97"

.EXAMPLE
    pwsh Scripts/antigravity-bridge.ps1 -Handoff "Phase 2 drift wiring landed in PR #112. Tests at QaToolsScoreQualityDriftTests."

.NOTES
    Plan: docs/plans/2026-05-05-tools-antigravity-claude-code-integration-plan.md
#>
[CmdletBinding(DefaultParameterSetName = 'Goto')]
param(
    [Parameter(ParameterSetName = 'Goto', Position = 0)]
    [string]$Goto,

    [Parameter(ParameterSetName = 'Diff')]
    [string]$Diff,

    [Parameter(ParameterSetName = 'AddFolder')]
    [string]$AddFolder,

    [Parameter(ParameterSetName = 'Handoff')]
    [string]$Handoff,

    [Parameter(ParameterSetName = 'Handoff')]
    [ValidateSet('claude-code', 'antigravity-native', 'agent', 'human')]
    [string]$From = 'claude-code'
)

$ErrorActionPreference = 'Stop'

$AntigravityCli = 'C:/Users/spare/AppData/Local/Programs/Antigravity/bin/antigravity'
if (-not (Test-Path $AntigravityCli)) {
    throw "Antigravity CLI not found at $AntigravityCli. Is Antigravity installed at the standard path?"
}

# Repo root — script lives in <repo>/Scripts/, so parent is the root.
$RepoRoot = Split-Path -Parent $PSScriptRoot

switch ($PSCmdlet.ParameterSetName) {
    'Goto' {
        if (-not $Goto) {
            Write-Host "Usage: -Goto <path[:line[:char]]>"
            return
        }
        # Antigravity --goto wants the file path resolvable from CWD or absolute.
        # If it's relative, join from repo root so calling from any cwd works.
        $parts = $Goto -split ':', 2
        $filePath = $parts[0]
        $tail = if ($parts.Count -gt 1) { ":$($parts[1])" } else { '' }
        if (-not [System.IO.Path]::IsPathRooted($filePath)) {
            $filePath = Join-Path $RepoRoot $filePath
        }
        & $AntigravityCli --reuse-window --goto "${filePath}${tail}"
        Write-Host "→ Antigravity: jumped to ${filePath}${tail}"
    }
    'Diff' {
        $pair = $Diff -split ','
        if ($pair.Count -ne 2) {
            throw "Diff expects two paths separated by a comma."
        }
        $a, $b = $pair | ForEach-Object {
            if ([System.IO.Path]::IsPathRooted($_.Trim())) { $_.Trim() }
            else { Join-Path $RepoRoot $_.Trim() }
        }
        & $AntigravityCli --reuse-window --diff $a $b
        Write-Host "→ Antigravity: diff $a vs $b"
    }
    'AddFolder' {
        $folder = $AddFolder
        if (-not [System.IO.Path]::IsPathRooted($folder)) {
            $folder = Join-Path $RepoRoot $folder
        }
        if (-not (Test-Path $folder)) {
            throw "Folder not found: $folder"
        }
        & $AntigravityCli --reuse-window --add $folder
        Write-Host "→ Antigravity: added $folder to active window"
    }
    'Handoff' {
        $handoffDir = Join-Path $RepoRoot 'state/handoffs'
        if (-not (Test-Path $handoffDir)) {
            New-Item -ItemType Directory -Path $handoffDir -Force | Out-Null
        }
        $stamp = (Get-Date -Format 'yyyy-MM-ddTHH-mm-ssZ').Replace(':', '-')
        $file = Join-Path $handoffDir "$stamp-$From.md"

        # Capture lightweight context: branch, last commit, working-tree summary.
        $branch = (git -C $RepoRoot rev-parse --abbrev-ref HEAD 2>$null) ?? '<no-git>'
        $head   = (git -C $RepoRoot log -1 --format='%h %s' 2>$null) ?? '<no-commits>'
        $dirty  = (git -C $RepoRoot status --short 2>$null | Measure-Object -Line).Lines
@"
---
from: $From
at: $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')
branch: $branch
head: $head
working_tree_dirty_lines: $dirty
---

$Handoff
"@ | Set-Content -Path $file -Encoding UTF8
        Write-Host "→ Handoff written: $file"
        Write-Host "  Pick up from the other AI surface by reading $($file.Replace($RepoRoot, '<repo>'))"
    }
}
