#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start all Guitar Alchemist services (Backend + Frontend)

.DESCRIPTION
    Starts the Aspire AppHost which orchestrates all services:
    - MongoDB (with MongoExpress UI)
    - GaApi (main API server)
    - GuitarAlchemistChatbot (Blazor chatbot)
    - ga-client (React frontend)

.PARAMETER NoBuild
    Skip building the solution before starting

.PARAMETER Dashboard
    Open the Aspire Dashboard in the browser after starting

.EXAMPLE
    .\start-all.ps1
    Start all services with build

.EXAMPLE
    .\start-all.ps1 -NoBuild
    Start all services without building

.EXAMPLE
    .\start-all.ps1 -Dashboard
    Start all services and open the Aspire Dashboard
#>

param(
    [switch]$NoBuild,
    [switch]$Dashboard
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

function Write-Url
{
    param([string]$Name, [string]$Url)
    Write-Host "  $Name" -NoNewline -ForegroundColor White
    Write-Host " → " -NoNewline -ForegroundColor DarkGray
    Write-Host $Url -ForegroundColor Cyan
}

# Get repository root
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

Write-Header "Guitar Alchemist - Start All Services"
Write-Info "Repository: $RepoRoot"
Write-Info "Started: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )"

# ============================================
# PREREQUISITES CHECK
# ============================================
Write-Step "Checking prerequisites..."

# Check .NET SDK
$dotnetVersion = dotnet --version 2> $null
if (-not $dotnetVersion)
{
    Write-Host "✗ .NET SDK not found!" -ForegroundColor Red
    Write-Info "Please install .NET 9 SDK from https://dotnet.microsoft.com/download"
    exit 1
}
Write-Success ".NET SDK $dotnetVersion found"

# Check Docker
$dockerVersion = docker --version 2> $null
if (-not $dockerVersion)
{
    Write-Host "✗ Docker not found!" -ForegroundColor Red
    Write-Info "Please install Docker Desktop from https://www.docker.com/products/docker-desktop"
    Write-Info "Docker is required for MongoDB container"
    exit 1
}
Write-Success "Docker found: $dockerVersion"

# Check if Docker is running
$dockerRunning = docker ps 2> $null
if (-not $dockerRunning)
{
    Write-Host "✗ Docker is not running!" -ForegroundColor Red
    Write-Info "Please start Docker Desktop"
    exit 1
}
Write-Success "Docker is running"

# Check Node.js (for React frontend)
$nodeVersion = node --version 2> $null
if (-not $nodeVersion)
{
    Write-Host "⚠ Node.js not found!" -ForegroundColor Yellow
    Write-Info "Node.js is required for the React frontend (ga-client)"
    Write-Info "Install from https://nodejs.org/"
    Write-Info "Continuing without React frontend..."
}
else
{
    Write-Success "Node.js $nodeVersion found"
}

# ============================================
# BUILD STEP
# ============================================
if (-not $NoBuild)
{
    Write-Step "Building solution..."

    $buildStart = Get-Date
    $buildOutput = dotnet build AllProjects.sln -c Debug --nologo 2>&1
    $buildExitCode = $LASTEXITCODE
    $buildDuration = (Get-Date) - $buildStart

    if ($buildExitCode -eq 0)
    {
        Write-Success "Build succeeded in $($buildDuration.TotalSeconds.ToString('F2') )s"
    }
    else
    {
        Write-Host "Build failed!" -ForegroundColor Red
        Write-Host $buildOutput
        exit 1
    }
}
else
{
    Write-Info "Skipping build (--NoBuild specified)"
}

# ============================================
# START ASPIRE APPHOST
# ============================================
Write-Header "Starting Aspire AppHost"

Write-Info "The AppHost will start all services:"
Write-Info "  • MongoDB (with MongoExpress UI)"
Write-Info "  • GaApi (main API server)"
Write-Info "  • GuitarAlchemistChatbot (Blazor chatbot)"
Write-Info "  • ga-client (React frontend)"

Write-Step "Launching Aspire AppHost..."
Write-Info "Press Ctrl+C to stop all services"
Write-Host ""

# Start the AppHost
try
{
    # Change to AppHost directory
    Push-Location "AllProjects.AppHost"

    # Display service URLs
    Write-Header "Service URLs"
    Write-Info "Once services are running, you can access them at:"
    Write-Host ""
    Write-Url "Aspire Dashboard" "https://localhost:15001"
    Write-Url "GaApi (Swagger)" "https://localhost:7001/swagger"
    Write-Url "GaApi (Health)" "https://localhost:7001/health"
    Write-Url "Chatbot" "https://localhost:7002"
    Write-Url "React Frontend" "http://localhost:5173"
    Write-Url "MongoExpress" "http://localhost:8081"
    Write-Host ""
    Write-Info "Note: Actual ports may vary. Check the Aspire Dashboard for exact URLs."
    Write-Host ""

    # Open dashboard if requested
    if ($Dashboard)
    {
        Write-Info "Opening Aspire Dashboard in browser..."
        Start-Sleep -Seconds 3
        Start-Process "https://localhost:15001"
    }

    Write-Header "Starting Services"
    Write-Info "Waiting for services to start..."
    Write-Host ""

    # Run the AppHost
    $aspireOutput = dotnet run --no-build 2>&1
    $aspireExitCode = $LASTEXITCODE

    if ($aspireExitCode -ne 0)
    {
        Write-Host "`n✗ Failed to start Aspire AppHost" -ForegroundColor Red

        # Check for common errors
        if ($aspireOutput -match "CliPath.*required|DashboardPath.*missing")
        {
            Write-Host "`nℹ Aspire workload is not installed or configured correctly" -ForegroundColor Yellow
            Write-Host "`nTo fix this, run:" -ForegroundColor Cyan
            Write-Host "  dotnet workload update" -ForegroundColor White
            Write-Host "  dotnet workload install aspire" -ForegroundColor White
            Write-Host "`nOr restore Aspire packages:" -ForegroundColor Cyan
            Write-Host "  dotnet restore AllProjects.AppHost" -ForegroundColor White
        }
        else
        {
            Write-Host "`nError output:" -ForegroundColor Yellow
            Write-Host $aspireOutput
        }

        Pop-Location
        exit 1
    }

}
catch
{
    Write-Host "`nError starting services: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}
finally
{
    Pop-Location
}

Write-Host "`n"
Write-Header "Services Stopped"
Write-Info "All services have been stopped."
Write-Info "Completed: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )"

