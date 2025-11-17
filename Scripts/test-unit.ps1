Param(
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = Split-Path -Parent $root
$runsettings = Join-Path $solutionDir 'Tests\RunSettings\Default.runsettings'

if (-not (Test-Path $runsettings)) {
  Write-Error "Runsettings not found: $runsettings"
}

$args = @('test', '--no-restore', '-s', $runsettings)
if ($NoBuild) { $args += '--no-build' }

Write-Host "Running unit tests (excluding Category=Integration) with settings: $runsettings" -ForegroundColor Cyan
dotnet @args
