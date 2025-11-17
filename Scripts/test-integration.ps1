Param(
  [switch]$SkipDocker,
  [int]$WaitSeconds = 15,
  [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

function Test-Command {
  param([string]$Name)
  $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = Split-Path -Parent $root
$composeFile = Join-Path $solutionDir 'docker-compose.yml'
$runsettings = Join-Path $solutionDir 'Tests\RunSettings\Integration.runsettings'

if (-not (Test-Path $runsettings)) {
  Write-Error "Runsettings not found: $runsettings"
}

if (-not $SkipDocker) {
  if (Test-Path $composeFile) {
    Write-Host "Starting backend services via docker compose..." -ForegroundColor Cyan
    if (Test-Command 'docker') {
      try {
        & docker compose -f $composeFile up -d
      } catch {
        if (Test-Command 'docker-compose') {
          & docker-compose -f $composeFile up -d
        } else {
          throw "Neither 'docker compose' nor 'docker-compose' is available. Install Docker Desktop or start backend manually."
        }
      }
      Write-Host "Waiting $WaitSeconds seconds for services to become healthy..." -ForegroundColor DarkGray
      Start-Sleep -Seconds $WaitSeconds
    } else {
      Write-Warning "Docker is not installed or not in PATH. Start the backend manually or pass -SkipDocker."
    }
  } else {
    Write-Warning "docker-compose.yml not found at $composeFile. Skipping backend startup."
  }
}

$args = @('test', '--no-restore', '-s', $runsettings)
if ($NoBuild) { $args += '--no-build' }

Write-Host "Running INTEGRATION tests only with settings: $runsettings" -ForegroundColor Yellow
dotnet @args
