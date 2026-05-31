#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Context-staleness linter (opt-in, warning-only).

.DESCRIPTION
    Reads .context-staleness.json from the repo root. For each rule, if any file
    matching `code_path` was modified in the current commit AND `context_file`
    has not been touched in `max_staleness_days`, emit a WARNING to stderr.

    This is informational; it always exits 0. The Karpathy + Cherny instrumentation
    discipline prefers a warning surface over a hard gate for documentation drift,
    since hard gates create incentive to lie about doc updates.

.NOTES
    Rules whose `context_file` does not exist are silently skipped, so this config
    can be seeded before the parallel CONTEXT.md sweep lands.

    Per O'Reilly "Context Management": detect when code changes outpace the context
    artifacts that explain them, before the explanation rots beyond cheap repair.
#>

[CmdletBinding()]
param(
    [string]$ConfigPath = ".context-staleness.json",
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Continue"

if ([string]::IsNullOrEmpty($RepoRoot))
{
    $RepoRoot = (git rev-parse --show-toplevel 2>$null)
    if ([string]::IsNullOrEmpty($RepoRoot))
    {
        # Not in a repo or git missing — silent exit, this is a warning surface.
        exit 0
    }
}

$resolvedConfig = Join-Path $RepoRoot $ConfigPath
if (-not (Test-Path $resolvedConfig))
{
    # No config = nothing to check. Silent.
    exit 0
}

try
{
    $config = Get-Content $resolvedConfig -Raw | ConvertFrom-Json
}
catch
{
    [Console]::Error.WriteLine("[staleness] Failed to parse ${ConfigPath}: $_")
    exit 0
}

if ($null -eq $config.rules -or $config.rules.Count -eq 0)
{
    exit 0
}

# Gather staged files (pre-commit context). Fall back to last-commit diff if none staged.
$stagedFiles = @(git diff --cached --name-only 2>$null | Where-Object { $_ })
if ($stagedFiles.Count -eq 0)
{
    $stagedFiles = @(git diff --name-only HEAD~1 HEAD 2>$null | Where-Object { $_ })
}

if ($stagedFiles.Count -eq 0)
{
    exit 0
}

# Convert a glob like Common/GA.Business.ML/Agents/** into a regex
function ConvertTo-GlobRegex
{
    param([string]$Glob)
    # Escape regex specials except * and /
    $escaped = [regex]::Escape($Glob)
    # Restore globs: \*\* -> .* and \* -> [^/]*
    $escaped = $escaped -replace '\\\*\\\*', '.*'
    $escaped = $escaped -replace '\\\*', '[^/]*'
    # Normalize path separators (handle both / and \)
    $escaped = $escaped -replace '/', '[/\\\\]'
    return "^$escaped$"
}

$now = Get-Date

foreach ($rule in $config.rules)
{
    $codePath = $rule.code_path
    $contextFile = $rule.context_file
    $maxDays = [int]$rule.max_staleness_days

    $resolvedContext = Join-Path $RepoRoot $contextFile
    if (-not (Test-Path $resolvedContext))
    {
        # Rule disabled — context file doesn't exist yet. Skip silently.
        continue
    }

    $regex = ConvertTo-GlobRegex -Glob $codePath
    $codeChanged = $stagedFiles | Where-Object { $_ -match $regex }

    if (-not $codeChanged)
    {
        continue
    }

    # Last-modified date from git log (more accurate than file mtime across checkouts).
    $lastTouchedRaw = git log -1 --format=%cI -- $contextFile 2>$null
    $lastTouched = $null
    if (-not [string]::IsNullOrEmpty($lastTouchedRaw))
    {
        $lastTouched = [datetimeoffset]::Parse($lastTouchedRaw).LocalDateTime
    }
    else
    {
        # File present but never committed — fall back to mtime.
        $lastTouched = (Get-Item $resolvedContext).LastWriteTime
    }

    $ageDays = [int]($now - $lastTouched).TotalDays

    if ($ageDays -gt $maxDays)
    {
        $msg = "[staleness] $codePath changed but $contextFile last touched $ageDays days ago."
        [Console]::Error.WriteLine($msg)
    }
}

# Always succeed — this is a warning surface, not a gate.
exit 0
