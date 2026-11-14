# Windows entry point for the canonical L2 Cloud Dev Smoke script.
#
# `bash` resolves to WSL on current Windows installations, where the Windows
# .NET SDK is not automatically available. Resolve Git Bash from the installed
# git.exe instead, then execute the same cross-platform shell script used by CI.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$gitCommand = Get-Command git -ErrorAction Stop
$gitRoot = Split-Path (Split-Path $gitCommand.Source -Parent) -Parent
$gitBash = Join-Path $gitRoot 'bin/bash.exe'
if (-not (Test-Path -LiteralPath $gitBash)) {
    throw "Git Bash was not found at '$gitBash'."
}

& $gitBash (Join-Path $PSScriptRoot 'cloud-validate.sh')
exit $LASTEXITCODE
