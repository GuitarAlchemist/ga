#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start Guitar Alchemist with Graphiti integration for demonstration

.DESCRIPTION
    This script starts all necessary services for the Graphiti integration demo:
    - FalkorDB (graph database)
    - Graphiti Python service
    - GA API with Graphiti integration
    - React frontend with knowledge graph visualization

.PARAMETER SkipBuild
    Skip building the solution before starting services

.PARAMETER GraphitiOnly
    Start only Graphiti-related services (FalkorDB + Graphiti service)

.PARAMETER Verbose
    Enable verbose logging

.EXAMPLE
    .\Scripts\start-graphiti-demo.ps1
    Start all services for the full demo

.EXAMPLE
    .\Scripts\start-graphiti-demo.ps1 -GraphitiOnly
    Start only Graphiti services for testing
#>

param(
    [switch]$SkipBuild,
    [switch]$GraphitiOnly,
    [switch]$Verbose
)

# Set error handling
$ErrorActionPreference = "Stop"

# Colors for output
$Green = "`e[32m"
$Yellow = "`e[33m"
$Red = "`e[31m"
$Blue = "`e[34m"
$Reset = "`e[0m"

function Write-ColorOutput {
    param([string]$Message, [string]$Color = $Reset)
    Write-Host "$Color$Message$Reset"
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "🚀 $Message" $Blue
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✅ $Message" $Green
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠️  $Message" $Yellow
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "❌ $Message" $Red
}

function Test-Command {
    param([string]$Command)
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Test-Port {
    param([int]$Port)
    try {
        $connection = Test-NetConnection -ComputerName "localhost" -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue
        return $connection
    }
    catch {
        return $false
    }
}

function Wait-ForService {
    param(
        [string]$ServiceName,
        [string]$Url,
        [int]$TimeoutSeconds = 60
    )
    
    Write-Step "Waiting for $ServiceName to be ready..."
    $elapsed = 0
    $interval = 2
    
    while ($elapsed -lt $TimeoutSeconds) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 5 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                Write-Success "$ServiceName is ready!"
                return $true
            }
        }
        catch {
            # Service not ready yet
        }
        
        Start-Sleep $interval
        $elapsed += $interval
        Write-Host "." -NoNewline
    }
    
    Write-Error "$ServiceName failed to start within $TimeoutSeconds seconds"
    return $false
}

# Main execution
try {
    Write-ColorOutput @"
🎸 Guitar Alchemist × Graphiti Demo Startup
==========================================
"@ $Blue

    # Check prerequisites
    Write-Step "Checking prerequisites..."
    
    $missingTools = @()
    
    if (-not (Test-Command "docker")) {
        $missingTools += "docker"
    }
    
    if (-not (Test-Command "docker-compose")) {
        $missingTools += "docker-compose"
    }
    
    if (-not (Test-Command "dotnet")) {
        $missingTools += "dotnet"
    }
    
    if (-not (Test-Command "ollama")) {
        Write-Warning "Ollama not found. Please install Ollama and pull required models:"
        Write-Host "  ollama pull qwen2.5-coder:1.5b-base"
        Write-Host "  ollama pull nomic-embed-text"
    }
    
    if ($missingTools.Count -gt 0) {
        Write-Error "Missing required tools: $($missingTools -join ', ')"
        Write-Host "Please install the missing tools and try again."
        exit 1
    }
    
    Write-Success "All prerequisites found!"
    
    # Check if Ollama is running and has required models
    Write-Step "Checking Ollama setup..."
    try {
        $ollamaList = ollama list 2>$null
        if ($ollamaList -match "qwen2.5-coder:1.5b-base" -and $ollamaList -match "nomic-embed-text") {
            Write-Success "Ollama models are available!"
        } else {
            Write-Warning "Required Ollama models not found. Please run:"
            Write-Host "  ollama pull qwen2.5-coder:1.5b-base"
            Write-Host "  ollama pull nomic-embed-text"
        }
    }
    catch {
        Write-Warning "Could not check Ollama models. Make sure Ollama is running."
    }
    
    # Build solution if not skipped
    if (-not $SkipBuild) {
        Write-Step "Building solution..."
        dotnet build AllProjects.sln -c Debug --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed!"
            exit 1
        }
        Write-Success "Build completed!"
    }
    
    # Start services with Docker Compose
    Write-Step "Starting services with Docker Compose..."
    
    if ($GraphitiOnly) {
        Write-Host "Starting Graphiti services only..."
        docker-compose up -d falkordb graphiti-service
    } else {
        Write-Host "Starting all services..."
        docker-compose up -d
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start services!"
        exit 1
    }
    
    # Wait for services to be ready
    Write-Step "Waiting for services to be ready..."
    
    # Wait for FalkorDB
    if (-not (Wait-ForService "FalkorDB" "http://localhost:6379" 30)) {
        Write-Error "FalkorDB failed to start"
        exit 1
    }
    
    # Wait for Graphiti service
    if (-not (Wait-ForService "Graphiti Service" "http://localhost:8000/health" 60)) {
        Write-Error "Graphiti service failed to start"
        exit 1
    }
    
    if (-not $GraphitiOnly) {
        # Wait for GA API
        if (-not (Wait-ForService "GA API" "http://localhost:7001/health" 60)) {
            Write-Error "GA API failed to start"
            exit 1
        }
        
        # Wait for React frontend
        if (-not (Wait-ForService "React Frontend" "http://localhost:5173" 60)) {
            Write-Error "React frontend failed to start"
            exit 1
        }
    }
    
    # Display service URLs
    Write-Success "All services are ready! 🎉"
    Write-ColorOutput @"

📊 Service URLs:
================
• FalkorDB Browser:    http://localhost:3000
• Graphiti API:        http://localhost:8000
• Graphiti API Docs:   http://localhost:8000/docs
"@ $Green
    
    if (-not $GraphitiOnly) {
        Write-ColorOutput @"
• GA API:              http://localhost:7001
• GA API Swagger:      http://localhost:7001/swagger
• React Frontend:      http://localhost:5173
• Graphiti Demo:       http://localhost:5173/test/graphiti-demo
"@ $Green
    }
    
    Write-ColorOutput @"

🧪 Quick Test Commands:
=======================
# Test Graphiti health
curl http://localhost:8000/health

# Add a practice episode
curl -X POST http://localhost:8000/episodes \
  -H "Content-Type: application/json" \
  -d '{"user_id":"demo-user","episode_type":"practice","content":{"chord_practiced":"Cmaj7","duration_minutes":15,"accuracy":0.85}}'

# Search the knowledge graph
curl -X POST http://localhost:8000/search \
  -H "Content-Type: application/json" \
  -d '{"query":"jazz chords","search_type":"hybrid","limit":5}'

🎸 Happy learning with temporal knowledge graphs!
"@ $Blue

}
catch {
    Write-Error "An error occurred: $($_.Exception.Message)"
    Write-Host "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}
