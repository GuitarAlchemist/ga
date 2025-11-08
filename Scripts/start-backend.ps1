# Start Backend Services with Aspire Dashboard
# This script starts the Guitar Alchemist backend services including:
# - AllProjects.AppHost (Aspire orchestration with dashboard)
# - MongoDB
# - Redis
# - Other backend services

param(
    [switch]$Dashboard = $true,
    [switch]$SkipBuild = $false,
    [int]$DashboardPort = 15001
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "🎸 Guitar Alchemist - Backend Startup" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Build the solution if not skipped
if (-not $SkipBuild) {
    Write-Host "📦 Building solution..." -ForegroundColor Yellow
    Push-Location $repoRoot
    dotnet build AllProjects.slnx -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed!" -ForegroundColor Red
        exit 1
    }
    Pop-Location
    Write-Host "✅ Build successful!" -ForegroundColor Green
    Write-Host ""
}

# Start the Aspire AppHost
Write-Host "🚀 Starting Aspire AppHost..." -ForegroundColor Yellow
Push-Location "$repoRoot/AllProjects.AppHost"

if ($Dashboard) {
    Write-Host "📊 Dashboard will be available at: https://localhost:$DashboardPort" -ForegroundColor Cyan
    Write-Host ""
}

dotnet run

Pop-Location

