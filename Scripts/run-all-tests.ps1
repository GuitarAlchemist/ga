#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Comprehensive test suite runner for Guitar Alchemist project

.DESCRIPTION
    Runs all backend tests (NUnit, xUnit, Aspire integration) and frontend tests (Playwright)
    Provides detailed output, timing, and summary report

.PARAMETER SkipBuild
    Skip the build step and run tests directly

.PARAMETER BackendOnly
    Run only backend tests (skip Playwright)

.PARAMETER PlaywrightOnly
    Run only Playwright tests (skip backend)

.PARAMETER Verbose
    Show detailed output from test runs

.EXAMPLE
    .\run-all-tests.ps1
    Run all tests with build

.EXAMPLE
    .\run-all-tests.ps1 -SkipBuild
    Run all tests without building

.EXAMPLE
    .\run-all-tests.ps1 -BackendOnly
    Run only backend tests
#>

param(
    [switch]$SkipBuild,
    [switch]$BackendOnly,
    [switch]$PlaywrightOnly,
    [switch]$Verbose
)

# Color functions
function Write-Header
{
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success
{
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Failure
{
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info
{
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Yellow
}

function Write-Step
{
    param([string]$Message)
    Write-Host "`n▶ $Message" -ForegroundColor Blue
}

# Test results tracking
$script:TestResults = @{
    BuildSuccess = $false
    BackendTests = @{
        Passed = 0
        Failed = 0
        Skipped = 0
        Duration = 0
    }
    PlaywrightTests = @{
        Passed = 0
        Failed = 0
        Skipped = 0
        Duration = 0
    }
    TotalDuration = 0
}

$script:StartTime = Get-Date

# Get repository root
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

Write-Header "Guitar Alchemist - Comprehensive Test Suite"
Write-Info "Repository: $RepoRoot"
Write-Info "Started: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )"

# ============================================
# BUILD STEP
# ============================================
if (-not $SkipBuild -and -not $PlaywrightOnly)
{
    Write-Step "Building solution..."

    $buildStart = Get-Date
    $buildOutput = dotnet build AllProjects.sln -c Debug --nologo 2>&1
    $buildExitCode = $LASTEXITCODE
    $buildDuration = (Get-Date) - $buildStart

    if ($buildExitCode -eq 0)
    {
        Write-Success "Build succeeded in $($buildDuration.TotalSeconds.ToString('F2') )s"
        $script:TestResults.BuildSuccess = $true
    }
    else
    {
        Write-Failure "Build failed!"
        if ($Verbose)
        {
            Write-Host $buildOutput
        }
        exit 1
    }
}

# ============================================
# BACKEND TESTS
# ============================================
if (-not $PlaywrightOnly)
{
    Write-Header "Backend Tests"

    Write-Step "Running all .NET tests (NUnit + xUnit)..."

    $backendStart = Get-Date

    # Run all tests in the solution
    $testOutput = dotnet test AllProjects.sln `
        --no-build `
        --configuration Debug `
        --logger "console;verbosity=normal" `
        --logger "trx;LogFileName=test-results.trx" `
        2>&1

    $testExitCode = $LASTEXITCODE
    $backendDuration = (Get-Date) - $backendStart
    $script:TestResults.BackendTests.Duration = $backendDuration.TotalSeconds

    # Parse test results from output
    $testOutputStr = $testOutput | Out-String
    if ($testOutputStr -match "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)")
    {
        $script:TestResults.BackendTests.Failed = [int]$Matches[1]
        $script:TestResults.BackendTests.Passed = [int]$Matches[2]
        $script:TestResults.BackendTests.Skipped = [int]$Matches[3]
    }
    elseif ($testOutputStr -match "Failed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)")
    {
        $script:TestResults.BackendTests.Failed = [int]$Matches[1]
        $script:TestResults.BackendTests.Passed = [int]$Matches[2]
        $script:TestResults.BackendTests.Skipped = [int]$Matches[3]
    }
    elseif ($testOutputStr -match "Total tests:\s+(\d+)")
    {
        # Alternative parsing for different output format
        $totalTests = [int]$Matches[1]
        if ($testOutputStr -match "Passed:\s+(\d+)")
        {
            $script:TestResults.BackendTests.Passed = [int]$Matches[1]
        }
        if ($testOutputStr -match "Failed:\s+(\d+)")
        {
            $script:TestResults.BackendTests.Failed = [int]$Matches[1]
        }
        if ($testOutputStr -match "Skipped:\s+(\d+)")
        {
            $script:TestResults.BackendTests.Skipped = [int]$Matches[1]
        }
    }

    if ($Verbose)
    {
        Write-Host $testOutput
    }

    if ($testExitCode -eq 0)
    {
        Write-Success "Backend tests passed: $( $script:TestResults.BackendTests.Passed ) passed, $( $script:TestResults.BackendTests.Skipped ) skipped in $($backendDuration.TotalSeconds.ToString('F2') )s"
    }
    else
    {
        Write-Failure "Backend tests failed: $( $script:TestResults.BackendTests.Failed ) failed, $( $script:TestResults.BackendTests.Passed ) passed, $( $script:TestResults.BackendTests.Skipped ) skipped"
    }

    # Show individual test project results
    Write-Info "`nTest Projects:"
    Write-Info "  - GA.Business.Core.Tests (NUnit)"
    Write-Info "  - AllProjects.AppHost.Tests (xUnit - Aspire Integration)"
}

# ============================================
# PLAYWRIGHT TESTS
# ============================================
if (-not $BackendOnly)
{
    Write-Header "Playwright Tests"

    Write-Step "Running Playwright tests for Blazor Chatbot..."

    $playwrightStart = Get-Date

    # Ensure Playwright browsers are installed
    Write-Info "Checking Playwright installation..."
    Push-Location "Tests/GuitarAlchemistChatbot.Tests.Playwright"

    # Install Playwright if needed
    $playwrightInstall = pwsh build.ps1 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        Write-Info "Installing Playwright browsers..."
        dotnet build
        pwsh bin/Debug/net9.0/playwright.ps1 install
    }

    # Run Playwright tests
    $playwrightOutput = dotnet test `
        --no-build `
        --configuration Debug `
        --logger "console;verbosity=normal" `
        --logger "trx;LogFileName=playwright-results.trx" `
        2>&1

    $playwrightExitCode = $LASTEXITCODE
    Pop-Location

    $playwrightDuration = (Get-Date) - $playwrightStart
    $script:TestResults.PlaywrightTests.Duration = $playwrightDuration.TotalSeconds

    # Parse Playwright test results
    $playwrightOutputStr = $playwrightOutput | Out-String
    if ($playwrightOutputStr -match "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)")
    {
        $script:TestResults.PlaywrightTests.Failed = [int]$Matches[1]
        $script:TestResults.PlaywrightTests.Passed = [int]$Matches[2]
        $script:TestResults.PlaywrightTests.Skipped = [int]$Matches[3]
    }
    elseif ($playwrightOutputStr -match "Failed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+)")
    {
        $script:TestResults.PlaywrightTests.Failed = [int]$Matches[1]
        $script:TestResults.PlaywrightTests.Passed = [int]$Matches[2]
        $script:TestResults.PlaywrightTests.Skipped = [int]$Matches[3]
    }
    elseif ($playwrightOutputStr -match "Total tests:\s+(\d+)")
    {
        # Alternative parsing for different output format
        $totalTests = [int]$Matches[1]
        if ($playwrightOutputStr -match "Passed:\s+(\d+)")
        {
            $script:TestResults.PlaywrightTests.Passed = [int]$Matches[1]
        }
        if ($playwrightOutputStr -match "Failed:\s+(\d+)")
        {
            $script:TestResults.PlaywrightTests.Failed = [int]$Matches[1]
        }
        if ($playwrightOutputStr -match "Skipped:\s+(\d+)")
        {
            $script:TestResults.PlaywrightTests.Skipped = [int]$Matches[1]
        }
    }

    if ($Verbose)
    {
        Write-Host $playwrightOutput
    }

    if ($playwrightExitCode -eq 0)
    {
        Write-Success "Playwright tests passed: $( $script:TestResults.PlaywrightTests.Passed ) passed, $( $script:TestResults.PlaywrightTests.Skipped ) skipped in $($playwrightDuration.TotalSeconds.ToString('F2') )s"
    }
    else
    {
        Write-Failure "Playwright tests failed: $( $script:TestResults.PlaywrightTests.Failed ) failed, $( $script:TestResults.PlaywrightTests.Passed ) passed, $( $script:TestResults.PlaywrightTests.Skipped ) skipped"
    }

    Write-Info "`nPlaywright Test Suites:"
    Write-Info "  - Chord Diagram Tests"
    Write-Info "  - Chord Progression Tests"
    Write-Info "  - Context Persistence Tests"
    Write-Info "  - Dark Mode Tests"
    Write-Info "  - Function Calling Tests"
    Write-Info "  - MCP Integration Tests"
    Write-Info "  - Tab Viewer Tests"
}

# ============================================
# SUMMARY REPORT
# ============================================
$script:TotalDuration = (Get-Date) - $script:StartTime

Write-Header "Test Summary"

# Calculate totals
$totalPassed = $script:TestResults.BackendTests.Passed + $script:TestResults.PlaywrightTests.Passed
$totalFailed = $script:TestResults.BackendTests.Failed + $script:TestResults.PlaywrightTests.Failed
$totalSkipped = $script:TestResults.BackendTests.Skipped + $script:TestResults.PlaywrightTests.Skipped
$totalTests = $totalPassed + $totalFailed + $totalSkipped

Write-Host "Build:       " -NoNewline
if ($script:TestResults.BuildSuccess -or $SkipBuild)
{
    Write-Host "✓ SUCCESS" -ForegroundColor Green
}
else
{
    Write-Host "✗ FAILED" -ForegroundColor Red
}

if (-not $PlaywrightOnly)
{
    Write-Host "`nBackend:     " -NoNewline
    if ($script:TestResults.BackendTests.Failed -eq 0)
    {
        Write-Host "✓ $( $script:TestResults.BackendTests.Passed ) passed" -ForegroundColor Green
    }
    else
    {
        Write-Host "✗ $( $script:TestResults.BackendTests.Failed ) failed, $( $script:TestResults.BackendTests.Passed ) passed" -ForegroundColor Red
    }
    Write-Host "             Duration: $($script:TestResults.BackendTests.Duration.ToString('F2') )s"
}

if (-not $BackendOnly)
{
    Write-Host "`nPlaywright:  " -NoNewline
    if ($script:TestResults.PlaywrightTests.Failed -eq 0)
    {
        Write-Host "✓ $( $script:TestResults.PlaywrightTests.Passed ) passed" -ForegroundColor Green
    }
    else
    {
        Write-Host "✗ $( $script:TestResults.PlaywrightTests.Failed ) failed, $( $script:TestResults.PlaywrightTests.Passed ) passed" -ForegroundColor Red
    }
    Write-Host "             Duration: $($script:TestResults.PlaywrightTests.Duration.ToString('F2') )s"
}

Write-Host "`n----------------------------------------"
Write-Host "Total Tests: $totalTests" -ForegroundColor Cyan
Write-Host "  Passed:    $totalPassed" -ForegroundColor Green
Write-Host "  Failed:    $totalFailed" -ForegroundColor $( if ($totalFailed -eq 0)
{
    "Green"
}
else
{
    "Red"
} )
Write-Host "  Skipped:   $totalSkipped" -ForegroundColor Yellow
Write-Host "`nTotal Duration: $($script:TotalDuration.TotalSeconds.ToString('F2') )s" -ForegroundColor Cyan
Write-Host "Completed: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )" -ForegroundColor Cyan
Write-Host "========================================`n"

# Exit with appropriate code
if ($totalFailed -gt 0)
{
    Write-Failure "Tests failed! See details above."
    exit 1
}
else
{
    Write-Success "All tests passed!"
    exit 0
}

