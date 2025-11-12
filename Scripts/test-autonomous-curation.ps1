#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test script for the Autonomous Curation system

.DESCRIPTION
    This script demonstrates the autonomous curation workflow:
    1. Analyzes knowledge gaps in the Guitar Alchemist knowledge base
    2. Searches YouTube for relevant educational videos
    3. Evaluates video quality using Ollama LLM
    4. Processes accepted videos through the retroaction loop
    5. Updates the Graphiti knowledge graph

.PARAMETER BaseUrl
    The base URL of the GaApi service (default: https://localhost:7001)

.PARAMETER Mode
    The curation mode: 'quick' (5 videos max) or 'custom' (configurable)

.PARAMETER MaxVideosPerGap
    Maximum videos to process per knowledge gap (custom mode only)

.PARAMETER MaxTotalVideos
    Maximum total videos to process (custom mode only)

.PARAMETER MinQualityScore
    Minimum quality score (0.0-1.0) for accepting videos (custom mode only)

.EXAMPLE
    .\test-autonomous-curation.ps1
    Run in quick mode (default)

.EXAMPLE
    .\test-autonomous-curation.ps1 -Mode custom -MaxVideosPerGap 5 -MaxTotalVideos 20 -MinQualityScore 0.8
    Run in custom mode with specific parameters
#>

param(
    [string]$BaseUrl = "https://localhost:7001",
    [ValidateSet("quick", "custom")]
    [string]$Mode = "quick",
    [int]$MaxVideosPerGap = 3,
    [int]$MaxTotalVideos = 10,
    [double]$MinQualityScore = 0.7
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Blue
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

# Check if GaApi is running
function Test-GaApiRunning {
    Write-Info "Checking if GaApi is running at $BaseUrl..."
    try {
        $response = Invoke-WebRequest -Uri "$BaseUrl/health" -Method Get -SkipCertificateCheck -TimeoutSec 5 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Success "GaApi is running"
            return $true
        }
    }
    catch {
        Write-Warning "GaApi is not running at $BaseUrl"
        Write-Info "Please start GaApi first using: dotnet run --project Apps/ga-server/GaApi/GaApi.csproj"
        return $false
    }
    return $false
}

# Step 1: Analyze knowledge gaps
function Get-KnowledgeGaps {
    Write-Header "Step 1: Analyzing Knowledge Gaps"
    
    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/autonomous-curation/analyze-gaps" -Method Get -SkipCertificateCheck
        
        Write-Success "Knowledge gap analysis complete"
        Write-Info "Total gaps found: $($response.gaps.Count)"
        
        # Display gaps by priority
        $criticalGaps = $response.gaps | Where-Object { $_.priority -eq "Critical" }
        $highGaps = $response.gaps | Where-Object { $_.priority -eq "High" }
        $mediumGaps = $response.gaps | Where-Object { $_.priority -eq "Medium" }
        $lowGaps = $response.gaps | Where-Object { $_.priority -eq "Low" }
        
        Write-Host "`nGaps by Priority:" -ForegroundColor Yellow
        Write-Host "  Critical: $($criticalGaps.Count)" -ForegroundColor Red
        Write-Host "  High:     $($highGaps.Count)" -ForegroundColor Yellow
        Write-Host "  Medium:   $($mediumGaps.Count)" -ForegroundColor Blue
        Write-Host "  Low:      $($lowGaps.Count)" -ForegroundColor Gray
        
        # Display top 5 gaps
        Write-Host "`nTop 5 Knowledge Gaps:" -ForegroundColor Yellow
        $response.gaps | Select-Object -First 5 | ForEach-Object {
            Write-Host "  [$($_.priority)] $($_.category): $($_.topic)" -ForegroundColor Cyan
            Write-Host "    Reason: $($_.reason)" -ForegroundColor Gray
        }
        
        return $response
    }
    catch {
        Write-Error "Failed to analyze knowledge gaps: $_"
        throw
    }
}

# Step 2: Start autonomous curation
function Start-AutonomousCuration {
    param(
        [string]$Mode,
        [int]$MaxVideosPerGap,
        [int]$MaxTotalVideos,
        [double]$MinQualityScore
    )
    
    Write-Header "Step 2: Starting Autonomous Curation ($Mode mode)"
    
    try {
        if ($Mode -eq "quick") {
            Write-Info "Starting quick curation (max 5 videos)..."
            $response = Invoke-RestMethod -Uri "$BaseUrl/api/autonomous-curation/start/quick" -Method Post -SkipCertificateCheck
        }
        else {
            Write-Info "Starting custom curation..."
            Write-Info "  Max videos per gap: $MaxVideosPerGap"
            Write-Info "  Max total videos: $MaxTotalVideos"
            Write-Info "  Min quality score: $MinQualityScore"
            
            $body = @{
                maxVideosPerGap = $MaxVideosPerGap
                maxTotalVideos = $MaxTotalVideos
                minQualityScore = $MinQualityScore
                focusPriorities = @("Critical", "High")
            } | ConvertTo-Json
            
            $response = Invoke-RestMethod -Uri "$BaseUrl/api/autonomous-curation/start" -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck
        }
        
        Write-Success "Autonomous curation started"
        Write-Info "Session ID: $($response.sessionId)"
        
        return $response
    }
    catch {
        Write-Error "Failed to start autonomous curation: $_"
        throw
    }
}

# Step 3: Monitor curation progress
function Show-CurationResults {
    param($Results)
    
    Write-Header "Step 3: Curation Results"
    
    Write-Host "`nSummary:" -ForegroundColor Yellow
    Write-Host "  Total videos evaluated: $($Results.totalVideosEvaluated)" -ForegroundColor Cyan
    Write-Host "  Videos accepted: $($Results.videosAccepted)" -ForegroundColor Green
    Write-Host "  Videos rejected: $($Results.videosRejected)" -ForegroundColor Red
    Write-Host "  Videos needing review: $($Results.videosNeedingReview)" -ForegroundColor Yellow
    
    if ($Results.acceptedVideos -and $Results.acceptedVideos.Count -gt 0) {
        Write-Host "`nAccepted Videos:" -ForegroundColor Green
        $Results.acceptedVideos | ForEach-Object {
            Write-Host "  ✓ $($_.title)" -ForegroundColor Green
            Write-Host "    URL: $($_.url)" -ForegroundColor Gray
            Write-Host "    Channel: $($_.channelName)" -ForegroundColor Gray
            Write-Host "    Quality Score: $($_.qualityScore)" -ForegroundColor Cyan
            Write-Host "    Knowledge Gap: $($_.knowledgeGap)" -ForegroundColor Yellow
        }
    }
    
    if ($Results.rejectedVideos -and $Results.rejectedVideos.Count -gt 0) {
        Write-Host "`nRejected Videos:" -ForegroundColor Red
        $Results.rejectedVideos | Select-Object -First 3 | ForEach-Object {
            Write-Host "  ✗ $($_.title)" -ForegroundColor Red
            Write-Host "    Reason: $($_.rejectionReason)" -ForegroundColor Gray
        }
        if ($Results.rejectedVideos.Count -gt 3) {
            Write-Host "  ... and $($Results.rejectedVideos.Count - 3) more" -ForegroundColor Gray
        }
    }
    
    if ($Results.videosNeedingReview -gt 0) {
        Write-Host "`nVideos Needing Manual Review: $($Results.videosNeedingReview)" -ForegroundColor Yellow
        Write-Info "These videos scored between 0.5 and 0.7 and require manual review"
    }
}

# Main execution
try {
    Write-Header "Autonomous Curation Test Script"
    Write-Info "Testing the Graphiti-Powered Autonomous Retroaction Loop"
    
    # Check if GaApi is running
    if (-not (Test-GaApiRunning)) {
        Write-Error "GaApi is not running. Please start it first."
        exit 1
    }
    
    # Step 1: Analyze knowledge gaps
    $gapAnalysis = Get-KnowledgeGaps
    
    if ($gapAnalysis.gaps.Count -eq 0) {
        Write-Warning "No knowledge gaps found. The knowledge base appears to be complete!"
        exit 0
    }
    
    # Step 2: Start autonomous curation
    $curationResults = Start-AutonomousCuration -Mode $Mode -MaxVideosPerGap $MaxVideosPerGap -MaxTotalVideos $MaxTotalVideos -MinQualityScore $MinQualityScore
    
    # Step 3: Show results
    Show-CurationResults -Results $curationResults
    
    Write-Header "Test Complete"
    Write-Success "Autonomous curation test completed successfully!"
    
    if ($curationResults.videosAccepted -gt 0) {
        Write-Info "Next steps:"
        Write-Info "  1. Review accepted videos in MongoDB (processedDocuments collection)"
        Write-Info "  2. Check Graphiti knowledge graph for new entities and relationships"
        Write-Info "  3. Monitor retroaction loop processing in the logs"
    }
}
catch {
    Write-Error "Test failed: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}

