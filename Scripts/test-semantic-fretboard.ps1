#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test script for semantic fretboard indexing and natural language querying with real Ollama models

.DESCRIPTION
    This script sets up Ollama, downloads required models, and runs comprehensive tests
    for the semantic fretboard system including real LLM integration tests.

.PARAMETER TestType
    Type of tests to run: All, Integration, Performance, Unit

.PARAMETER SkipOllamaSetup
    Skip Ollama installation and model download

.PARAMETER OllamaUrl
    Ollama base URL (default: http://localhost:11434)

.EXAMPLE
    ./test-semantic-fretboard.ps1 -TestType Integration
    ./test-semantic-fretboard.ps1 -TestType All -SkipOllamaSetup
#>

param(
    [ValidateSet("All", "Integration", "Performance", "Unit")]
    [string]$TestType = "Integration",

    [switch]$SkipOllamaSetup,

    [string]$OllamaUrl = "http://localhost:11434",

    [switch]$Verbose
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Enable verbose output if requested
if ($Verbose) {
    $VerbosePreference = "Continue"
}

Write-Host "🎸 Guitar Alchemist Semantic Fretboard Test Runner" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Function to check if Ollama is running
function Test-OllamaRunning {
    param([string]$Url)

    try {
        $response = Invoke-RestMethod -Uri "$Url/api/tags" -Method Get -TimeoutSec 5
        return $true
    }
    catch {
        return $false
    }
}

# Function to download Ollama model
function Install-OllamaModel {
    param(
        [string]$ModelName,
        [string]$OllamaUrl
    )

    Write-Host "📥 Downloading model: $ModelName" -ForegroundColor Yellow

    try {
        $body = @{ name = $ModelName } | ConvertTo-Json
        $response = Invoke-RestMethod -Uri "$OllamaUrl/api/pull" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 600
        Write-Host "✅ Successfully downloaded: $ModelName" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "❌ Failed to download: $ModelName - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Function to check if model exists
function Test-OllamaModel {
    param(
        [string]$ModelName,
        [string]$OllamaUrl
    )

    try {
        $response = Invoke-RestMethod -Uri "$OllamaUrl/api/tags" -Method Get
        $models = $response.models | ForEach-Object { $_.name }
        return $models -contains $ModelName
    }
    catch {
        return $false
    }
}

# Setup Ollama if not skipped
if (-not $SkipOllamaSetup) {
    Write-Host "🔧 Setting up Ollama..." -ForegroundColor Cyan

    # Check if Ollama is running
    if (-not (Test-OllamaRunning -Url $OllamaUrl)) {
        Write-Host "❌ Ollama is not running at $OllamaUrl" -ForegroundColor Red
        Write-Host "Please start Ollama first:" -ForegroundColor Yellow
        Write-Host "  1. Install Ollama from https://ollama.ai" -ForegroundColor Yellow
        Write-Host "  2. Run: ollama serve" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "✅ Ollama is running at $OllamaUrl" -ForegroundColor Green

    # Required models
    $requiredModels = @(
        "nomic-embed-text",  # Embedding model
        "llama3.2:latest"    # LLM model
    )

    # Download required models
    foreach ($model in $requiredModels) {
        if (Test-OllamaModel -ModelName $model -OllamaUrl $OllamaUrl) {
            Write-Host "✅ Model already available: $model" -ForegroundColor Green
        }
        else {
            $success = Install-OllamaModel -ModelName $model -OllamaUrl $OllamaUrl
            if (-not $success) {
                Write-Host "❌ Failed to download required model: $model" -ForegroundColor Red
                Write-Host "Tests may fail without this model." -ForegroundColor Yellow
            }
        }
    }
}

# Build the solution
Write-Host "🔨 Building solution..." -ForegroundColor Cyan
dotnet build AllProjects.sln -c Debug --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build successful" -ForegroundColor Green

# Run tests based on type
Write-Host "🧪 Running $TestType tests..." -ForegroundColor Cyan

$testFilter = switch ($TestType) {
    "Integration" { "TestCategory=Integration&TestCategory=Ollama" }
    "Performance" { "TestCategory=Performance&TestCategory=Ollama" }
    "Unit" { "TestCategory!=Integration&TestCategory!=Performance&TestCategory!=Ollama" }
    "All" { "" }
}

$testCommand = "dotnet test AllProjects.sln -c Debug --logger console --verbosity normal"
if ($testFilter) {
    $testCommand += " --filter `"$testFilter`""
}

Write-Host "Executing: $testCommand" -ForegroundColor Gray
Invoke-Expression $testCommand

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
}
else {
    Write-Host "❌ Some tests failed" -ForegroundColor Red
    exit 1
}

Write-Host "`n🎉 Semantic Fretboard testing completed successfully!" -ForegroundColor Green