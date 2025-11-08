#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run tests for GA Client (React Chatbot)

.DESCRIPTION
    This script runs unit tests (Vitest) and E2E tests (Playwright) for the GA Client application.

.PARAMETER Unit
    Run only unit tests (Vitest)

.PARAMETER E2E
    Run only E2E tests (Playwright)

.PARAMETER Headed
    Run Playwright tests in headed mode (show browser)

.PARAMETER UI
    Run tests in UI mode for interactive debugging

.PARAMETER Coverage
    Run unit tests with coverage report

.PARAMETER Watch
    Run unit tests in watch mode

.EXAMPLE
    .\run-tests.ps1
    Run all tests (unit + E2E)

.EXAMPLE
    .\run-tests.ps1 -Unit
    Run only unit tests

.EXAMPLE
    .\run-tests.ps1 -E2E -Headed
    Run E2E tests with visible browser

.EXAMPLE
    .\run-tests.ps1 -Unit -Coverage
    Run unit tests with coverage report
#>

param(
    [Parameter(HelpMessage="Run only unit tests")]
    [switch]$Unit,
    
    [Parameter(HelpMessage="Run only E2E tests")]
    [switch]$E2E,
    
    [Parameter(HelpMessage="Run Playwright tests in headed mode")]
    [switch]$Headed,
    
    [Parameter(HelpMessage="Run tests in UI mode")]
    [switch]$UI,
    
    [Parameter(HelpMessage="Run unit tests with coverage")]
    [switch]$Coverage,
    
    [Parameter(HelpMessage="Run unit tests in watch mode")]
    [switch]$Watch
)

# Color output functions
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-ColorOutput "========================================" "Cyan"
    Write-ColorOutput $Message "Cyan"
    Write-ColorOutput "========================================" "Cyan"
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "▶ $Message" "Yellow"
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✅ $Message" "Green"
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "❌ $Message" "Red"
}

function Write-Info {
    param([string]$Message)
    Write-ColorOutput "ℹ $Message" "Gray"
}

# Determine what to run
$runUnit = $Unit -or (-not $E2E)
$runE2E = $E2E -or (-not $Unit)

Write-Header "GA Client Test Suite"

Write-Info "Test Directory: $PSScriptRoot"
Write-Info "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

# Track results
$unitTestsPassed = $true
$e2eTestsPassed = $true

# ============================================
# UNIT TESTS (Vitest)
# ============================================
if ($runUnit) {
    Write-Header "Unit Tests (Vitest)"
    
    try {
        Write-Step "Running Vitest tests..."
        
        # Build test command
        $vitestCommand = "npm run test"
        
        if ($Coverage) {
            $vitestCommand = "npm run test:coverage"
        } elseif ($UI) {
            $vitestCommand = "npm run test:ui"
        } elseif ($Watch) {
            $vitestCommand = "npm run test -- --watch"
        } else {
            $vitestCommand = "npm run test -- --run"
        }
        
        Write-Info "Command: $vitestCommand"
        Write-Host ""
        
        Invoke-Expression $vitestCommand
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Unit tests passed!"
        } else {
            Write-Error "Unit tests failed!"
            $unitTestsPassed = $false
        }
    } catch {
        Write-Error "Failed to run unit tests: $_"
        $unitTestsPassed = $false
    }
}

# ============================================
# E2E TESTS (Playwright)
# ============================================
if ($runE2E) {
    Write-Header "E2E Tests (Playwright)"
    
    try {
        Write-Step "Checking Playwright installation..."
        
        # Check if Playwright browsers are installed
        $playwrightInstalled = Test-Path "node_modules/@playwright/test"
        
        if (-not $playwrightInstalled) {
            Write-Info "Installing Playwright..."
            npm install --save-dev @playwright/test
        }
        
        # Install browsers if needed
        Write-Info "Installing Playwright browsers (if needed)..."
        npx playwright install chromium
        
        Write-Step "Running Playwright tests..."
        
        # Build test command
        $playwrightCommand = "npm run test:e2e"
        
        if ($Headed) {
            $playwrightCommand = "npm run test:e2e:headed"
        } elseif ($UI) {
            $playwrightCommand = "npm run test:e2e:ui"
        }
        
        Write-Info "Command: $playwrightCommand"
        Write-Host ""
        
        Invoke-Expression $playwrightCommand
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "E2E tests passed!"
        } else {
            Write-Error "E2E tests failed!"
            $e2eTestsPassed = $false
        }
    } catch {
        Write-Error "Failed to run E2E tests: $_"
        $e2eTestsPassed = $false
    }
}

# ============================================
# SUMMARY
# ============================================
Write-Header "Test Summary"

if ($runUnit) {
    if ($unitTestsPassed) {
        Write-Success "Unit Tests: PASSED"
    } else {
        Write-Error "Unit Tests: FAILED"
    }
}

if ($runE2E) {
    if ($e2eTestsPassed) {
        Write-Success "E2E Tests: PASSED"
    } else {
        Write-Error "E2E Tests: FAILED"
    }
}

Write-Host ""
Write-Info "Completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

# Exit with appropriate code
if ((-not $runUnit -or $unitTestsPassed) -and (-not $runE2E -or $e2eTestsPassed)) {
    Write-Success "All tests passed! 🎉"
    exit 0
} else {
    Write-Error "Some tests failed!"
    exit 1
}

