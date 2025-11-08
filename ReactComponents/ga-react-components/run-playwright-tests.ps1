#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run Playwright tests for GA React Components

.DESCRIPTION
    This script runs Playwright tests with proper setup and error handling.
    It ensures the dev server is running and executes the test suite.

.PARAMETER Headed
    Run tests in headed mode (show browser)

.PARAMETER Debug
    Run tests in debug mode with Playwright Inspector

.PARAMETER UI
    Run tests in UI mode for interactive debugging

.PARAMETER Test
    Run specific test file (e.g., "bsp-doom-explorer")

.EXAMPLE
    .\run-playwright-tests.ps1
    Run all tests in headless mode

.EXAMPLE
    .\run-playwright-tests.ps1 -Headed
    Run all tests with visible browser

.EXAMPLE
    .\run-playwright-tests.ps1 -Test bsp-doom-explorer -Headed
    Run BSP DOOM Explorer tests with visible browser

.EXAMPLE
    .\run-playwright-tests.ps1 -UI
    Run tests in interactive UI mode
#>

param(
    [switch]$Headed,
    [switch]$Debug,
    [switch]$UI,
    [string]$Test = ""
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

Write-ColorOutput "🎭 GA React Components - Playwright Test Runner" "Cyan"
Write-ColorOutput "================================================" "Cyan"
Write-Host ""

# Check if we're in the right directory
if (-not (Test-Path "package.json")) {
    Write-ColorOutput "❌ Error: package.json not found. Please run from ReactComponents/ga-react-components directory" "Red"
    exit 1
}

# Check if node_modules exists
if (-not (Test-Path "node_modules")) {
    Write-ColorOutput "📦 Installing dependencies..." "Yellow"
    npm ci
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "❌ Failed to install dependencies" "Red"
        exit 1
    }
}

# Check if Playwright browsers are installed
Write-ColorOutput "🔍 Checking Playwright browsers..." "Yellow"
$playwrightCheck = npx playwright --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput "📥 Installing Playwright browsers..." "Yellow"
    npx playwright install
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "❌ Failed to install Playwright browsers" "Red"
        exit 1
    }
}

# Check if dev server is running
Write-ColorOutput "🔍 Checking if dev server is running..." "Yellow"
$devServerRunning = $false
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5177" -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        $devServerRunning = $true
        Write-ColorOutput "✅ Dev server is already running" "Green"
    }
} catch {
    Write-ColorOutput "⚠️  Dev server is not running" "Yellow"
}

# Start dev server if not running
$devServerProcess = $null
if (-not $devServerRunning) {
    Write-ColorOutput "🚀 Starting dev server..." "Yellow"
    $devServerProcess = Start-Process -FilePath "npm" -ArgumentList "run", "dev" -PassThru -NoNewWindow
    
    # Wait for dev server to be ready
    Write-ColorOutput "⏳ Waiting for dev server to be ready..." "Yellow"
    $maxAttempts = 30
    $attempt = 0
    $serverReady = $false
    
    while ($attempt -lt $maxAttempts -and -not $serverReady) {
        Start-Sleep -Seconds 1
        $attempt++
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5177" -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $serverReady = $true
                Write-ColorOutput "✅ Dev server is ready!" "Green"
            }
        } catch {
            Write-Host "." -NoNewline
        }
    }
    
    Write-Host ""
    
    if (-not $serverReady) {
        Write-ColorOutput "❌ Dev server failed to start within 30 seconds" "Red"
        if ($devServerProcess) {
            Stop-Process -Id $devServerProcess.Id -Force
        }
        exit 1
    }
}

# Build test command
$testCommand = "npx playwright test"

if ($Test) {
    $testCommand += " tests/$Test.spec.ts"
}

if ($Headed) {
    $testCommand += " --headed"
}

if ($Debug) {
    $testCommand += " --debug"
}

if ($UI) {
    $testCommand = "npx playwright test --ui"
}

# Run tests
Write-Host ""
Write-ColorOutput "🧪 Running Playwright tests..." "Cyan"
Write-ColorOutput "Command: $testCommand" "Gray"
Write-Host ""

try {
    Invoke-Expression $testCommand
    $testExitCode = $LASTEXITCODE
} catch {
    Write-ColorOutput "❌ Test execution failed: $_" "Red"
    $testExitCode = 1
}

# Cleanup: Stop dev server if we started it
if ($devServerProcess) {
    Write-Host ""
    Write-ColorOutput "🛑 Stopping dev server..." "Yellow"
    Stop-Process -Id $devServerProcess.Id -Force
    Write-ColorOutput "✅ Dev server stopped" "Green"
}

# Summary
Write-Host ""
Write-ColorOutput "================================================" "Cyan"
if ($testExitCode -eq 0) {
    Write-ColorOutput "✅ All tests passed!" "Green"
} else {
    Write-ColorOutput "❌ Some tests failed" "Red"
}
Write-ColorOutput "================================================" "Cyan"

exit $testExitCode

