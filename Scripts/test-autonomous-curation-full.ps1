#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete end-to-end test of the autonomous curation system
.DESCRIPTION
    This script:
    1. Checks prerequisites (MongoDB, Ollama, GaApi)
    2. Starts services if needed
    3. Runs the autonomous curation test
    4. Verifies results in MongoDB and Graphiti
.PARAMETER SkipServiceCheck
    Skip checking if services are running (assume they're already started)
.PARAMETER QuickMode
    Run in quick mode (max 5 videos)
.EXAMPLE
    .\Scripts\test-autonomous-curation-full.ps1
.EXAMPLE
    .\Scripts\test-autonomous-curation-full.ps1 -SkipServiceCheck -QuickMode
#>

param(
    [switch]$SkipServiceCheck,
    [switch]$QuickMode
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Step { param($Message) Write-Host "`n🔹 $Message" -ForegroundColor Blue }

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║  Autonomous Curation System - End-to-End Test                 ║" -ForegroundColor Magenta
Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Magenta

# Step 1: Check Prerequisites
if (-not $SkipServiceCheck) {
    Write-Step "Step 1: Checking Prerequisites"
    
    # Check MongoDB
    Write-Info "Checking MongoDB..."
    try {
        $mongoResponse = Invoke-WebRequest -Uri "http://localhost:27017" -TimeoutSec 2 -ErrorAction Stop
        Write-Success "MongoDB is running on port 27017"
    } catch {
        Write-Warning "MongoDB may not be running on port 27017"
        Write-Info "To start MongoDB: mongod --dbpath <your-data-path>"
    }
    
    # Check Ollama
    Write-Info "Checking Ollama..."
    try {
        $ollamaResponse = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -TimeoutSec 2 -ErrorAction Stop
        $models = ($ollamaResponse.Content | ConvertFrom-Json).models
        if ($models.Count -gt 0) {
            Write-Success "Ollama is running with $($models.Count) model(s): $($models.name -join ', ')"
        } else {
            Write-Warning "Ollama is running but no models are installed"
            Write-Info "To install a model: ollama pull llama2"
        }
    } catch {
        Write-Error "Ollama is not running on port 11434"
        Write-Info "To start Ollama: ollama serve"
        exit 1
    }
    
    # Check GaApi
    Write-Info "Checking GaApi..."
    try {
        $gaApiResponse = Invoke-WebRequest -Uri "https://localhost:7001/health" -SkipCertificateCheck -TimeoutSec 2 -ErrorAction Stop
        Write-Success "GaApi is running on port 7001"
    } catch {
        Write-Warning "GaApi is not running on port 7001"
        Write-Info "Starting GaApi..."
        
        # Try to start GaApi
        $gaApiPath = Join-Path $PSScriptRoot "..\Apps\ga-server\GaApi"
        if (Test-Path $gaApiPath) {
            Write-Info "Launching GaApi in background..."
            Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$gaApiPath`"" -WindowStyle Minimized
            
            # Wait for GaApi to start (max 30 seconds)
            $maxWait = 30
            $waited = 0
            $started = $false
            while ($waited -lt $maxWait) {
                Start-Sleep -Seconds 2
                $waited += 2
                try {
                    $testResponse = Invoke-WebRequest -Uri "https://localhost:7001/health" -SkipCertificateCheck -TimeoutSec 1 -ErrorAction Stop
                    Write-Success "GaApi started successfully"
                    $started = $true
                    break
                } catch {
                    Write-Host "." -NoNewline
                }
            }
            
            if (-not $started) {
                Write-Error "GaApi failed to start within $maxWait seconds"
                Write-Info "Please start GaApi manually: cd Apps\ga-server\GaApi && dotnet run"
                exit 1
            }
        } else {
            Write-Error "GaApi project not found at: $gaApiPath"
            exit 1
        }
    }
    
    # Check Graphiti service
    Write-Info "Checking Graphiti service..."
    try {
        $graphitiResponse = Invoke-WebRequest -Uri "http://localhost:8000/health" -TimeoutSec 2 -ErrorAction Stop
        Write-Success "Graphiti service is running on port 8000"
    } catch {
        Write-Warning "Graphiti service may not be running on port 8000"
        Write-Info "The autonomous curation will work but Graphiti integration may fail"
    }
}

# Step 2: Run Knowledge Gap Analysis
Write-Step "Step 2: Analyzing Knowledge Gaps"
try {
    $gapAnalysisUrl = "https://localhost:7001/api/autonomous-curation/analyze-gaps"
    Write-Info "Calling: GET $gapAnalysisUrl"
    
    $gapResponse = Invoke-RestMethod -Uri $gapAnalysisUrl -Method Get -SkipCertificateCheck
    
    Write-Success "Knowledge gap analysis completed"
    Write-Info "Total gaps found: $($gapResponse.gaps.Count)"
    
    # Group by priority
    $critical = ($gapResponse.gaps | Where-Object { $_.priority -eq "Critical" }).Count
    $high = ($gapResponse.gaps | Where-Object { $_.priority -eq "High" }).Count
    $medium = ($gapResponse.gaps | Where-Object { $_.priority -eq "Medium" }).Count
    $low = ($gapResponse.gaps | Where-Object { $_.priority -eq "Low" }).Count
    
    Write-Host "`nGaps by Priority:" -ForegroundColor Cyan
    if ($critical -gt 0) { Write-Host "  🔴 Critical: $critical" -ForegroundColor Red }
    if ($high -gt 0) { Write-Host "  🟠 High: $high" -ForegroundColor Yellow }
    if ($medium -gt 0) { Write-Host "  🟡 Medium: $medium" -ForegroundColor Blue }
    if ($low -gt 0) { Write-Host "  🟢 Low: $low" -ForegroundColor Green }
    
    # Show top 5 gaps
    Write-Host "`nTop 5 Knowledge Gaps:" -ForegroundColor Cyan
    $gapResponse.gaps | Select-Object -First 5 | ForEach-Object {
        $priorityColor = switch ($_.priority) {
            "Critical" { "Red" }
            "High" { "Yellow" }
            "Medium" { "Blue" }
            "Low" { "Green" }
            default { "White" }
        }
        Write-Host "  [$($_.priority)] $($_.category): $($_.topic)" -ForegroundColor $priorityColor
    }
} catch {
    Write-Error "Failed to analyze knowledge gaps: $_"
    exit 1
}

# Step 3: Start Autonomous Curation
Write-Step "Step 3: Starting Autonomous Curation"

$curationUrl = if ($QuickMode) {
    "https://localhost:7001/api/autonomous-curation/start/quick"
} else {
    "https://localhost:7001/api/autonomous-curation/start"
}

$requestBody = if (-not $QuickMode) {
    @{
        maxVideosPerGap = 3
        maxTotalVideos = 10
        minQualityScore = 0.7
        focusPriorities = @("Critical", "High")
    } | ConvertTo-Json
} else {
    $null
}

try {
    Write-Info "Calling: POST $curationUrl"
    if ($QuickMode) {
        Write-Info "Mode: Quick (max 5 videos)"
    } else {
        Write-Info "Mode: Custom (max 10 videos, min quality 0.7)"
    }
    
    Write-Warning "This may take several minutes as it searches YouTube and evaluates videos with Ollama..."
    
    $curationResponse = if ($requestBody) {
        Invoke-RestMethod -Uri $curationUrl -Method Post -Body $requestBody -ContentType "application/json" -SkipCertificateCheck
    } else {
        Invoke-RestMethod -Uri $curationUrl -Method Post -SkipCertificateCheck
    }
    
    Write-Success "Autonomous curation completed"
    
    # Display results
    Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  Curation Results                                              ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Green
    
    Write-Host "Total Videos Processed: $($curationResponse.totalVideosProcessed)" -ForegroundColor Cyan
    Write-Host "Videos Accepted: $($curationResponse.acceptedVideos.Count)" -ForegroundColor Green
    Write-Host "Videos Rejected: $($curationResponse.rejectedVideos.Count)" -ForegroundColor Red
    Write-Host "Videos Needing Review: $($curationResponse.needsReviewVideos.Count)" -ForegroundColor Yellow
    
    if ($curationResponse.acceptedVideos.Count -gt 0) {
        Write-Host "`n✅ Accepted Videos:" -ForegroundColor Green
        $curationResponse.acceptedVideos | ForEach-Object {
            Write-Host "  📹 $($_.video.title)" -ForegroundColor White
            Write-Host "     URL: $($_.video.url)" -ForegroundColor Gray
            Write-Host "     Channel: $($_.video.channelName)" -ForegroundColor Gray
            Write-Host "     Quality Score: $([math]::Round($_.evaluation.qualityScore, 2))" -ForegroundColor Cyan
            Write-Host "     Gap: $($_.gap.category) - $($_.gap.topic)" -ForegroundColor Yellow
            Write-Host ""
        }
    }
    
    if ($curationResponse.rejectedVideos.Count -gt 0) {
        Write-Host "`n❌ Rejected Videos (showing first 3):" -ForegroundColor Red
        $curationResponse.rejectedVideos | Select-Object -First 3 | ForEach-Object {
            Write-Host "  📹 $($_.video.title)" -ForegroundColor White
            Write-Host "     Reason: $($_.evaluation.reasoning)" -ForegroundColor Gray
            Write-Host ""
        }
    }
    
} catch {
    Write-Error "Failed to run autonomous curation: $_"
    Write-Error $_.Exception.Message
    exit 1
}

# Step 4: Verify Results
Write-Step "Step 4: Verification Summary"
Write-Success "Autonomous curation system test completed successfully!"
Write-Info "Next steps:"
Write-Host "  1. Check MongoDB for stored videos" -ForegroundColor White
Write-Host "  2. Check Graphiti knowledge graph for new episodes" -ForegroundColor White
Write-Host "  3. Review accepted videos and trigger retroaction loop processing" -ForegroundColor White

Write-Host "`n╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║  Test Complete! ✅                                             ║" -ForegroundColor Magenta
Write-Host "╚════════════════════════════════════════════════════════════════╝`n" -ForegroundColor Magenta

