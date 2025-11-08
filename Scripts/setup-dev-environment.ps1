#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup development environment for Guitar Alchemist

.DESCRIPTION
    One-command setup for new developers:
    - Checks prerequisites (.NET, Docker, Node.js)
    - Installs required tools and workloads
    - Restores NuGet packages
    - Installs npm packages
    - Builds the solution
    - Sets up user secrets
    - Initializes MongoDB
    - Runs health checks

.PARAMETER SkipBuild
    Skip building the solution

.PARAMETER SkipMongo
    Skip MongoDB initialization

.EXAMPLE
    .\setup-dev-environment.ps1
    Full setup with all steps

.EXAMPLE
    .\setup-dev-environment.ps1 -SkipBuild
    Setup without building
#>

param(
    [switch]$SkipBuild,
    [switch]$SkipMongo
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

# Get repository root
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$script:StartTime = Get-Date

Write-Header "Guitar Alchemist - Development Environment Setup"
Write-Info "Repository: $RepoRoot"
Write-Info "Started: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )"

# ============================================
# STEP 1: CHECK PREREQUISITES
# ============================================
Write-Header "Step 1: Checking Prerequisites"

$script:Prerequisites = @{
    DotNet = $false
    Docker = $false
    Node = $false
    Git = $false
}

# Check .NET SDK
Write-Step "Checking .NET SDK..."
$dotnetVersion = dotnet --version 2> $null
if ($dotnetVersion)
{
    Write-Success ".NET SDK $dotnetVersion found"
    $script:Prerequisites.DotNet = $true
}
else
{
    Write-Failure ".NET SDK not found!"
    Write-Info "Download from: https://dotnet.microsoft.com/download/dotnet/9.0"
    Write-Info "Required: .NET 9.0 SDK"
}

# Check Docker
Write-Step "Checking Docker..."
$dockerVersion = docker --version 2> $null
if ($dockerVersion)
{
    Write-Success "Docker found: $dockerVersion"

    # Check if Docker is running
    $dockerRunning = docker ps 2> $null
    if ($dockerRunning)
    {
        Write-Success "Docker is running"
        $script:Prerequisites.Docker = $true
    }
    else
    {
        Write-Failure "Docker is not running!"
        Write-Info "Please start Docker Desktop"
    }
}
else
{
    Write-Failure "Docker not found!"
    Write-Info "Download from: https://www.docker.com/products/docker-desktop"
}

# Check Node.js
Write-Step "Checking Node.js..."
$nodeVersion = node --version 2> $null
if ($nodeVersion)
{
    Write-Success "Node.js $nodeVersion found"
    $script:Prerequisites.Node = $true

    # Check npm
    $npmVersion = npm --version 2> $null
    if ($npmVersion)
    {
        Write-Success "npm $npmVersion found"
    }
}
else
{
    Write-Failure "Node.js not found!"
    Write-Info "Download from: https://nodejs.org/ (LTS version recommended)"
}

# Check Git
Write-Step "Checking Git..."
$gitVersion = git --version 2> $null
if ($gitVersion)
{
    Write-Success "Git found: $gitVersion"
    $script:Prerequisites.Git = $true
}
else
{
    Write-Info "Git not found (optional but recommended)"
    Write-Info "Download from: https://git-scm.com/downloads"
}

# Verify critical prerequisites
if (-not $script:Prerequisites.DotNet)
{
    Write-Failure "`nCritical: .NET SDK is required. Please install it and run this script again."
    exit 1
}

if (-not $script:Prerequisites.Docker)
{
    Write-Failure "`nCritical: Docker is required. Please install and start it, then run this script again."
    exit 1
}

# ============================================
# STEP 2: INSTALL .NET WORKLOADS
# ============================================
Write-Header "Step 2: Installing .NET Workloads"

Write-Step "Updating .NET workloads..."
dotnet workload update 2>&1 | Out-Null

Write-Step "Installing Aspire workload..."
$aspireInstall = dotnet workload install aspire 2>&1
if ($LASTEXITCODE -eq 0)
{
    Write-Success "Aspire workload installed"
}
else
{
    Write-Info "Aspire workload may already be installed or using NuGet-based approach"
}

# ============================================
# STEP 3: RESTORE DEPENDENCIES
# ============================================
Write-Header "Step 3: Restoring Dependencies"

Write-Step "Restoring NuGet packages..."
$restoreOutput = dotnet restore AllProjects.sln 2>&1
if ($LASTEXITCODE -eq 0)
{
    Write-Success "NuGet packages restored"
}
else
{
    Write-Failure "Failed to restore NuGet packages"
    Write-Host $restoreOutput
    exit 1
}

if ($script:Prerequisites.Node)
{
    Write-Step "Installing React frontend dependencies..."
    Push-Location "Apps/ga-client"

    $npmInstall = npm ci 2>&1
    if ($LASTEXITCODE -eq 0)
    {
        Write-Success "React frontend dependencies installed"
    }
    else
    {
        Write-Info "Using npm install as fallback..."
        npm install 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0)
        {
            Write-Success "React frontend dependencies installed"
        }
        else
        {
            Write-Failure "Failed to install React frontend dependencies"
        }
    }

    Pop-Location
}

# ============================================
# STEP 4: BUILD SOLUTION
# ============================================
if (-not $SkipBuild)
{
    Write-Header "Step 4: Building Solution"

    Write-Step "Building AllProjects.sln..."
    $buildStart = Get-Date
    $buildOutput = dotnet build AllProjects.sln -c Debug --nologo 2>&1
    $buildDuration = (Get-Date) - $buildStart

    if ($LASTEXITCODE -eq 0)
    {
        Write-Success "Build succeeded in $($buildDuration.TotalSeconds.ToString('F2') )s"
    }
    else
    {
        Write-Failure "Build failed!"
        Write-Host $buildOutput
        exit 1
    }
}
else
{
    Write-Info "Skipping build (--SkipBuild specified)"
}

# ============================================
# STEP 5: SETUP USER SECRETS
# ============================================
Write-Header "Step 5: Setting Up User Secrets"

Write-Step "Configuring user secrets for GaApi..."
Push-Location "Apps/ga-server/GaApi"

# Initialize user secrets
dotnet user-secrets init 2>&1 | Out-Null

# Check if OpenAI API key is set
$openAiKey = dotnet user-secrets list 2>&1 | Select-String "OpenAI:ApiKey"
if (-not $openAiKey)
{
    Write-Info "OpenAI API key not configured"
    Write-Info "To enable vector search, set your OpenAI API key:"
    Write-Host "  cd Apps/ga-server/GaApi" -ForegroundColor Cyan
    Write-Host "  dotnet user-secrets set `"OpenAI:ApiKey`" `"your-api-key-here`"" -ForegroundColor Cyan
}
else
{
    Write-Success "OpenAI API key is configured"
}

Pop-Location

# ============================================
# STEP 6: INITIALIZE MONGODB
# ============================================
if (-not $SkipMongo -and $script:Prerequisites.Docker)
{
    Write-Header "Step 6: Initializing MongoDB"

    Write-Step "Checking for existing MongoDB container..."
    $mongoContainer = docker ps -a -q -f "name=mongodb" 2> $null

    if ($mongoContainer)
    {
        Write-Info "MongoDB container already exists"

        # Check if it's running
        $mongoRunning = docker ps -q -f "name=mongodb" 2> $null
        if ($mongoRunning)
        {
            Write-Success "MongoDB is running"
        }
        else
        {
            Write-Step "Starting MongoDB container..."
            docker start mongodb 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0)
            {
                Write-Success "MongoDB started"
            }
            else
            {
                Write-Failure "Failed to start MongoDB"
            }
        }
    }
    else
    {
        Write-Info "MongoDB will be created when you start the Aspire AppHost"
        Write-Info "Run: .\Scripts\start-all.ps1"
    }
}
else
{
    Write-Info "Skipping MongoDB initialization"
}

# ============================================
# STEP 7: INSTALL PLAYWRIGHT BROWSERS
# ============================================
Write-Header "Step 7: Installing Playwright Browsers"

Write-Step "Installing Playwright browsers for testing..."
Push-Location "Tests/GuitarAlchemistChatbot.Tests.Playwright"

if (Test-Path "bin/Debug/net9.0/playwright.ps1")
{
    pwsh bin/Debug/net9.0/playwright.ps1 install 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0)
    {
        Write-Success "Playwright browsers installed"
    }
    else
    {
        Write-Info "Playwright browsers will be installed on first test run"
    }
}
else
{
    Write-Info "Build the solution first to install Playwright browsers"
}

Pop-Location

# ============================================
# SUMMARY
# ============================================
$script:TotalDuration = (Get-Date) - $script:StartTime

Write-Header "Setup Complete!"

Write-Host "Prerequisites:" -ForegroundColor Cyan
Write-Host "  .NET SDK:  " -NoNewline
Write-Host $( if ($script:Prerequisites.DotNet)
{
    "✓"
}
else
{
    "✗"
} ) -ForegroundColor $( if ($script:Prerequisites.DotNet)
{
    "Green"
}
else
{
    "Red"
} )
Write-Host "  Docker:    " -NoNewline
Write-Host $( if ($script:Prerequisites.Docker)
{
    "✓"
}
else
{
    "✗"
} ) -ForegroundColor $( if ($script:Prerequisites.Docker)
{
    "Green"
}
else
{
    "Red"
} )
Write-Host "  Node.js:   " -NoNewline
Write-Host $( if ($script:Prerequisites.Node)
{
    "✓"
}
else
{
    "✗"
} ) -ForegroundColor $( if ($script:Prerequisites.Node)
{
    "Green"
}
else
{
    "Red"
} )
Write-Host "  Git:       " -NoNewline
Write-Host $( if ($script:Prerequisites.Git)
{
    "✓"
}
else
{
    "⚠"
} ) -ForegroundColor $( if ($script:Prerequisites.Git)
{
    "Green"
}
else
{
    "Yellow"
} )

Write-Host "`nSetup Duration: $($script:TotalDuration.TotalSeconds.ToString('F2') )s" -ForegroundColor Cyan
Write-Host "Completed: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )" -ForegroundColor Cyan

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "1. Start all services:" -ForegroundColor Yellow
Write-Host "   .\Scripts\start-all.ps1`n" -ForegroundColor White

Write-Host "2. Run tests:" -ForegroundColor Yellow
Write-Host "   .\Scripts\run-all-tests.ps1`n" -ForegroundColor White

Write-Host "3. Check service health:" -ForegroundColor Yellow
Write-Host "   .\Scripts\health-check.ps1`n" -ForegroundColor White

Write-Host "4. Access services:" -ForegroundColor Yellow
Write-Host "   Aspire Dashboard: https://localhost:15001" -ForegroundColor Cyan
Write-Host "   GaApi (Swagger):  https://localhost:7001/swagger" -ForegroundColor Cyan
Write-Host "   Chatbot:          https://localhost:7002" -ForegroundColor Cyan
Write-Host "   React Frontend:   http://localhost:5173`n" -ForegroundColor Cyan

Write-Success "Development environment is ready! 🎸"

