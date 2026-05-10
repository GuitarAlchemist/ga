# Workaround wrapper for /octo:review on Windows.
#
# As of 2026-05-10, the octo plugin's orchestrator (v9.13.0) spawns
# codex/gemini agents via bash + `env` and passes PATH unquoted. On
# Windows, PATH contains entries like:
#   C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v13.0\bin
#   C:\Program Files (x86)\NVIDIA Corporation\PhysX\Common
#   C:\Program Files\dotnet
# Bash word-splits on the spaces and `env` looks for an executable
# literally named "Files/NVIDIA", fails with "No such file or directory",
# exits 127. Every Round 1 agent fails this way silently and the
# orchestrator returns false-green {"findings": []}.
#
# This wrapper strips all space-bearing PATH entries before invoking the
# orchestrator, then restores the original PATH. Side effect: tools that
# need NVIDIA / dotnet on PATH (e.g. dotnet build) won't be reachable
# *during the review*. That's acceptable — review is read-only and the
# specialist agents don't need either toolchain.
#
# Usage:
#   pwsh Scripts/octo-review-clean.ps1 -Pr 155
#   pwsh Scripts/octo-review-clean.ps1 -Pr 155 -Focus correctness,security
#   pwsh Scripts/octo-review-clean.ps1 -Pr 155 -Publish auto
#
# Defaults match what /octo:review's slash command uses in supervised mode.
#
# Companion: Scripts/octo-gate-liveness.ps1 — verifies the review actually
# produced findings after this wrapper completes.
#
# Background: docs/solutions/tooling/2026-05-10-octo-plugin-install-corruption-silent-gate-failure.md

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Pr,

    [string[]]$Focus = @('correctness','security','architecture','tdd'),

    [ValidateSet('human','ai-assisted','autonomous','unknown')]
    [string]$Provenance = 'ai-assisted',

    [ValidateSet('ask','auto','never')]
    [string]$Publish = 'never',

    [ValidateSet('auto','none')]
    [string]$Debate = 'auto',

    [string]$PluginRoot = (Join-Path $env:USERPROFILE '.claude\plugins\cache\nyldn-plugins\octo\9.13.0'),

    [switch]$ShowStrippedPaths
)

$ErrorActionPreference = 'Stop'

$orchestrate = Join-Path $PluginRoot 'scripts/orchestrate.sh'
if (-not (Test-Path $orchestrate)) {
    throw "orchestrate.sh not found at $orchestrate — adjust -PluginRoot if octo is installed elsewhere"
}

# Capture original PATH; restore on any exit path
$origPath = $env:PATH
$origPSPath = $env:Path

try {
    $entries = $origPath -split ';'
    $clean = @()
    $stripped = @()
    foreach ($e in $entries) {
        if (-not $e) { continue }
        if ($e -match ' ') {
            $stripped += $e
        } else {
            $clean += $e
        }
    }

    if ($stripped.Count -eq 0) {
        Write-Host 'No space-bearing PATH entries to strip — your PATH is already orchestrator-safe.' -ForegroundColor Green
    }
    else {
        Write-Host ("Stripped {0} space-bearing PATH entries before review (will be restored on exit)" -f $stripped.Count) -ForegroundColor Yellow
        if ($ShowStrippedPaths) {
            foreach ($s in $stripped) {
                Write-Host "  - $s" -ForegroundColor DarkGray
            }
        }
    }

    # Apply the scrubbed PATH for the child process
    $env:PATH = $clean -join ';'
    $env:Path = $env:PATH

    # Build the profile JSON the orchestrator's code-review pipeline expects.
    # Matches the schema in /octo:review's slash command.
    $profile = [ordered]@{
        target     = $Pr
        focus      = $Focus
        provenance = $Provenance
        autonomy   = 'autonomous'   # bypass interactive Q&A
        publish    = $Publish
        debate     = $Debate
    } | ConvertTo-Json -Compress

    Write-Host "Profile: $profile" -ForegroundColor DarkGray
    Write-Host "Invoking orchestrator (this typically takes 3-8 minutes)..." -ForegroundColor Cyan
    Write-Host ''

    # bash is on the scrubbed PATH only if it lives outside Program Files.
    # Git for Windows installs to C:\Program Files\Git\... so we may have
    # just stripped it. Look it up before invoking.
    $bashExe = $null
    foreach ($candidate in @('C:\Program Files\Git\bin\bash.exe',
                              'C:\Program Files\Git\usr\bin\bash.exe',
                              'C:\Windows\System32\bash.exe')) {
        if (Test-Path $candidate) { $bashExe = $candidate; break }
    }
    if (-not $bashExe) {
        # Last resort — try whatever the original PATH would have resolved
        $env:PATH = $origPath
        $bashExe = (Get-Command bash -ErrorAction SilentlyContinue).Source
        $env:PATH = $clean -join ';'
    }
    if (-not $bashExe) {
        throw 'bash.exe not found — install Git for Windows or set $bashExe explicitly'
    }

    # Convert the Windows orchestrate path to a bash-friendly path
    $orchestrateBash = $orchestrate -replace '\\','/' -replace '^([A-Za-z]):','/$1' -replace '^/([A-Za-z])','/$1'
    # Simpler: pass as-is, bash on Windows accepts both
    & $bashExe $orchestrate code-review $profile
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        Write-Host ''
        Write-Host "Orchestrator exited $exitCode — see ~/.claude-octopus/results/" -ForegroundColor Yellow
    }

    Write-Host ''
    Write-Host 'Now run gate liveness check:' -ForegroundColor Cyan
    Write-Host '  pwsh Scripts/octo-gate-liveness.ps1' -ForegroundColor White

    exit $exitCode
}
finally {
    $env:PATH = $origPath
    $env:Path = $origPSPath
}
