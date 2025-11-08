# Start All Development Services (Backend + Frontend)
# This script starts both the backend (with Aspire dashboard) and frontend in separate terminals

param(
    [switch]$SkipBuild = $false,
    [switch]$SkipInstall = $false,
    [int]$DashboardPort = 15001,
    [int]$FrontendPort = 5173
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "🎸 Guitar Alchemist - Full Stack Development" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
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

# Check if pnpm is installed
$pnpmVersion = pnpm --version 2>$null
if (-not $pnpmVersion) {
    Write-Host "⚠️  pnpm not found. Installing globally..." -ForegroundColor Yellow
    npm install -g pnpm
}

Write-Host "📦 pnpm version: $pnpmVersion" -ForegroundColor Green
Write-Host ""

# Install frontend dependencies if not skipped
if (-not $SkipInstall) {
    Write-Host "📥 Installing frontend dependencies..." -ForegroundColor Yellow
    Push-Location "$repoRoot/Apps/ga-client"
    pnpm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Frontend dependency installation failed!" -ForegroundColor Red
        exit 1
    }
    Pop-Location
    Write-Host "✅ Frontend dependencies installed!" -ForegroundColor Green
    Write-Host ""
}

Write-Host "🚀 Starting services..." -ForegroundColor Yellow
Write-Host ""
Write-Host "📊 Aspire Dashboard: https://localhost:$DashboardPort" -ForegroundColor Cyan
Write-Host "📱 Frontend: http://localhost:$FrontendPort" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop all services" -ForegroundColor Yellow
Write-Host ""

# Start backend in a new terminal
Write-Host "Starting backend..." -ForegroundColor Green
$backendScript = "$scriptDir/start-backend.ps1"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "& '$backendScript' -SkipBuild -Dashboard"

# Wait a moment for backend to start
Start-Sleep -Seconds 3

# Start frontend in a new terminal
Write-Host "Starting frontend..." -ForegroundColor Green
$frontendScript = "$scriptDir/start-frontend.ps1"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "& '$frontendScript' -SkipInstall"

Write-Host ""
Write-Host "✅ All services started!" -ForegroundColor Green
Write-Host ""
Write-Host "Services running in separate terminals:" -ForegroundColor Cyan
Write-Host "  • Backend (Aspire): https://localhost:$DashboardPort" -ForegroundColor Cyan
Write-Host "  • Frontend: http://localhost:$FrontendPort" -ForegroundColor Cyan
Write-Host ""

