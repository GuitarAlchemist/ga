#!/usr/bin/env pwsh
# PowerShell script to run Playwright tests for Guitar Alchemist Chatbot

param(
    [Parameter(HelpMessage = "Test suite to run: all, progression, diagram, darkmode")]
    [ValidateSet("all", "progression", "diagram", "darkmode", "new")]
    [string]$Suite = "all",

    [Parameter(HelpMessage = "Show browser while testing")]
    [switch]$Headed,

    [Parameter(HelpMessage = "Slow motion delay in milliseconds")]
    [int]$SlowMo = 0,

    [Parameter(HelpMessage = "Browser to use: chromium, firefox, webkit")]
    [ValidateSet("chromium", "firefox", "webkit")]
    [string]$Browser = "chromium",

    [Parameter(HelpMessage = "Install Playwright browsers")]
    [switch]$Install
)

# Colors for output
$Green = "`e[32m"
$Yellow = "`e[33m"
$Red = "`e[31m"
$Blue = "`e[34m"
$Reset = "`e[0m"

function Write-ColorOutput
{
    param([string]$Color, [string]$Message)
    Write-Host "$Color$Message$Reset"
}

# Banner
Write-ColorOutput $Blue @"

╔═══════════════════════════════════════════════════════════╗
║   Guitar Alchemist Chatbot - Playwright Test Runner      ║
╚═══════════════════════════════════════════════════════════╝

"@

# Check if we need to install browsers
if ($Install)
{
    Write-ColorOutput $Yellow "Installing Playwright browsers..."
    $binPath = "bin/Debug/net10.0"

    if (Test-Path "$binPath/playwright.ps1")
    {
        & "$binPath/playwright.ps1" install
        Write-ColorOutput $Green "✓ Browsers installed successfully!"
    }
    else
    {
        Write-ColorOutput $Red "✗ Build the project first: dotnet build"
        exit 1
    }
    exit 0
}

# Check if chatbot is running
Write-ColorOutput $Yellow "Checking if chatbot is running..."
try
{
    $response = Invoke-WebRequest -Uri "https://localhost:7001" -SkipCertificateCheck -TimeoutSec 5 -ErrorAction Stop
    Write-ColorOutput $Green "✓ Chatbot is running at https://localhost:7001"
}
catch
{
    Write-ColorOutput $Red @"
✗ Chatbot is not running!

Please start the chatbot first:
  cd Apps/GuitarAlchemistChatbot
  dotnet run

Then run the tests again.
"@
    exit 1
}

# Build test filter based on suite
$filter = switch ($Suite)
{
    "progression" {
        "FullyQualifiedName~ChordProgressionTests"
    }
    "diagram" {
        "FullyQualifiedName~ChordDiagramTests"
    }
    "darkmode" {
        "FullyQualifiedName~DarkModeTests"
    }
    "new" {
        "FullyQualifiedName~ChordProgressionTests|FullyQualifiedName~ChordDiagramTests|FullyQualifiedName~DarkModeTests"
    }
    default {
        ""
    }
}

# Build test arguments
$testArgs = @()

if ($filter)
{
    $testArgs += "--filter"
    $testArgs += $filter
}

# Build Playwright options
$playwrightArgs = @()
$playwrightArgs += "Playwright.BrowserName=$Browser"

if ($Headed)
{
    $playwrightArgs += "Playwright.LaunchOptions.Headless=false"
}

if ($SlowMo -gt 0)
{
    $playwrightArgs += "Playwright.LaunchOptions.SlowMo=$SlowMo"
}

# Combine arguments
if ($playwrightArgs.Count -gt 0)
{
    $testArgs += "--"
    $testArgs += $playwrightArgs
}

# Display test configuration
Write-ColorOutput $Blue @"
Test Configuration:
  Suite:    $Suite
  Browser:  $Browser
  Headed:   $Headed
  SlowMo:   $( $SlowMo )ms

"@

# Run tests
Write-ColorOutput $Yellow "Running tests..."
Write-Host ""

$testCommand = "dotnet test"
if ($testArgs.Count -gt 0)
{
    $testCommand += " " + ($testArgs -join " ")
}

Write-ColorOutput $Blue "Command: $testCommand"
Write-Host ""

# Execute tests
$startTime = Get-Date
Invoke-Expression $testCommand
$exitCode = $LASTEXITCODE
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host ""

# Display results
if ($exitCode -eq 0)
{
    Write-ColorOutput $Green @"
╔═══════════════════════════════════════════════════════════╗
║                  ✓ ALL TESTS PASSED!                      ║
╚═══════════════════════════════════════════════════════════╝

Duration: $($duration.TotalSeconds.ToString("F2") ) seconds
"@
}
else
{
    Write-ColorOutput $Red @"
╔═══════════════════════════════════════════════════════════╗
║                  ✗ SOME TESTS FAILED                      ║
╚═══════════════════════════════════════════════════════════╝

Duration: $($duration.TotalSeconds.ToString("F2") ) seconds

Check the output above for details.
Screenshots saved in TestResults/ directory.
"@
}

exit $exitCode

