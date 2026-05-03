# junie-review.ps1 — Windows-friendly junie invocation that survives multi-line prompts.
#
# The bare `junie --task "$multiline"` invocation breaks on Windows because
# cmd.exe re-quotes embedded newlines and quotes inside the --task string.
# This wrapper reads the prompt from a file (or pipeline) into a PowerShell
# variable and passes it as a single argument array element to junie. Native
# PowerShell variable passing bypasses the cmd-layer rewrite.
#
# Usage:
#   pwsh Scripts/junie-review.ps1 -PromptFile path/to/prompt.txt [-Project repo-root]
#   Get-Content prompt.txt -Raw | pwsh Scripts/junie-review.ps1 [-Project repo-root]
#
# Why this exists: PR #90's review attempted to run junie three times and
# all three failed with "The syntax of the command is incorrect." until we
# switched to file-based prompt passing.

[CmdletBinding()]
param(
    [string]$PromptFile,
    [string]$Project = (Get-Location).Path,

    # Pipeline input. ValueFromPipeline accepts both `Get-Content -Raw`
    # (single string) and `Get-Content` (string array of lines).
    [Parameter(ValueFromPipeline = $true)]
    [string[]]$StdinLines
)

begin {
    $ErrorActionPreference = 'Stop'
    $bufferedLines = New-Object System.Collections.Generic.List[string]
}

process {
    if ($StdinLines) { $bufferedLines.AddRange([string[]]$StdinLines) }
}

end {
    if ($PromptFile) {
        if (-not (Test-Path $PromptFile)) {
            Write-Error "Prompt file not found: $PromptFile"
            exit 2
        }
        $promptText = Get-Content -Raw -Path $PromptFile
    }
    elseif ($bufferedLines.Count -gt 0) {
        $promptText = $bufferedLines -join "`n"
    }
    else {
        Write-Error "Empty prompt — pass -PromptFile or pipe content via stdin."
        exit 2
    }

    if ([string]::IsNullOrWhiteSpace($promptText)) {
        Write-Error "Empty prompt — pass -PromptFile or pipe content via stdin."
        exit 2
    }

    if (-not (Test-Path $Project)) {
        Write-Error "Project directory not found: $Project"
        exit 2
    }

    # Pass the prompt as a single array argument. PowerShell's native exec
    # path (& with @args) preserves the string verbatim — no cmd.exe rewrite,
    # no quote re-interpretation, no newline mangling.
    $junieArgs = @('--task', $promptText, '--project', $Project)
    & junie @junieArgs
    exit $LASTEXITCODE
}
