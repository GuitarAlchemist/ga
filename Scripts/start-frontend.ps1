# Start Frontend Development Server
# This script starts the ga-client frontend development server

param(
    [switch]$SkipInstall = $false,
    [int]$Port = 5173
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$frontendDir = "$repoRoot/Apps/ga-client"

Write-Host "🎸 Guitar Alchemist - Frontend Startup" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Check if pnpm is installed
$pnpmVersion = pnpm --version 2>$null
if (-not $pnpmVersion) {
    Write-Host "⚠️  pnpm not found. Installing globally..." -ForegroundColor Yellow
    npm install -g pnpm
}

Write-Host "📦 pnpm version: $pnpmVersion" -ForegroundColor Green
Write-Host ""

# Install dependencies if not skipped
if (-not $SkipInstall) {
    Write-Host "📥 Installing dependencies..." -ForegroundColor Yellow
    Push-Location $frontendDir
    pnpm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Dependency installation failed!" -ForegroundColor Red
        exit 1
    }
    Pop-Location
    Write-Host "✅ Dependencies installed!" -ForegroundColor Green
    Write-Host ""
}

# Start the development server
Write-Host "🚀 Starting frontend development server..." -ForegroundColor Yellow
Write-Host "📱 Frontend will be available at: http://localhost:$Port" -ForegroundColor Cyan
Write-Host ""

Push-Location $frontendDir
pnpm run dev

Pop-Location

