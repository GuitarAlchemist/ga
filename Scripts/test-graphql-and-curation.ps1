#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test GraphQL API and Autonomous Curation System
.DESCRIPTION
    This script tests the GraphQL API endpoints and runs the autonomous curation workflow.
    It checks prerequisites, starts services if needed, and runs comprehensive tests.
.PARAMETER SkipServiceStart
    Skip starting services (assumes they're already running)
.PARAMETER GraphQLOnly
    Only test GraphQL endpoints, skip autonomous curation
.PARAMETER CurationOnly
    Only test autonomous curation, skip GraphQL endpoint tests
.EXAMPLE
    .\test-graphql-and-curation.ps1
    .\test-graphql-and-curation.ps1 -SkipServiceStart
    .\test-graphql-and-curation.ps1 -GraphQLOnly
#>

param(
    [switch]$SkipServiceStart,
    [switch]$GraphQLOnly,
    [switch]$CurationOnly
)

$ErrorActionPreference = "Stop"

# Color output functions
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Step { param($Message) Write-Host "`n🔹 $Message" -ForegroundColor Blue }

# Configuration
$GaApiUrl = "https://localhost:7001"
$GraphQLUrl = "$GaApiUrl/graphql"
$OllamaUrl = "http://localhost:11434"

Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  GraphQL API & Autonomous Curation Test Suite             ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Step 1: Check Prerequisites
Write-Step "Checking Prerequisites"

# Check if Ollama is installed
try {
    $ollamaVersion = ollama --version 2>&1
    Write-Success "Ollama is installed: $ollamaVersion"
} catch {
    Write-Error "Ollama is not installed. Please install from https://ollama.ai"
    exit 1
}

# Check if .NET SDK is installed
try {
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK is installed: $dotnetVersion"
} catch {
    Write-Error ".NET SDK is not installed"
    exit 1
}

# Step 2: Start Services (if not skipped)
if (-not $SkipServiceStart) {
    Write-Step "Starting Services"
    
    # Check if Ollama is running
    Write-Info "Checking Ollama service..."
    try {
        $response = Invoke-WebRequest -Uri "$OllamaUrl/api/tags" -Method GET -TimeoutSec 5 -ErrorAction Stop
        Write-Success "Ollama is already running"
    } catch {
        Write-Warning "Ollama is not running. Starting Ollama..."
        Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden
        Start-Sleep -Seconds 5
        
        # Verify Ollama started
        try {
            $response = Invoke-WebRequest -Uri "$OllamaUrl/api/tags" -Method GET -TimeoutSec 5 -ErrorAction Stop
            Write-Success "Ollama started successfully"
        } catch {
            Write-Error "Failed to start Ollama"
            exit 1
        }
    }
    
    # Check if GaApi is running
    Write-Info "Checking GaApi service..."
    try {
        $response = Invoke-WebRequest -Uri "$GaApiUrl/health" -SkipCertificateCheck -TimeoutSec 5 -ErrorAction Stop
        Write-Success "GaApi is already running"
    } catch {
        Write-Warning "GaApi is not running. Please start it manually using:"
        Write-Host "  cd Apps/ga-server/GaApi" -ForegroundColor Yellow
        Write-Host "  dotnet run" -ForegroundColor Yellow
        Write-Host "`nOr use the Aspire dashboard:" -ForegroundColor Yellow
        Write-Host "  pwsh Scripts/start-all.ps1 -Dashboard" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Info "Skipping service startup (assuming services are running)"
}

# Step 3: Test GraphQL API
if (-not $CurationOnly) {
    Write-Step "Testing GraphQL API Endpoints"
    
    # Test 1: GraphQL Schema Introspection
    Write-Info "Test 1: GraphQL Schema Introspection"
    $introspectionQuery = @{
        query = @"
{
  __schema {
    queryType {
      name
      fields {
        name
        description
      }
    }
    mutationType {
      name
      fields {
        name
        description
      }
    }
  }
}
"@
    } | ConvertTo-Json
    
    try {
        $response = Invoke-WebRequest -Uri $GraphQLUrl -Method POST -Body $introspectionQuery -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.__schema) {
            Write-Success "GraphQL schema introspection successful"
            Write-Host "  Query fields: $($result.__schema.queryType.fields.Count)" -ForegroundColor Gray
            Write-Host "  Mutation fields: $($result.__schema.mutationType.fields.Count)" -ForegroundColor Gray
        } else {
            Write-Error "GraphQL schema introspection failed"
        }
    } catch {
        Write-Error "Failed to query GraphQL schema: $_"
    }
    
    # Test 2: Get All Documents
    Write-Info "Test 2: Get All Documents (Paginated)"
    $getAllDocsQuery = @{
        query = @"
{
  getAllDocuments(skip: 0, take: 5) {
    id
    sourceType
    sourceUrl
    title
    summary
    processedAt
  }
}
"@
    } | ConvertTo-Json
    
    try {
        $response = Invoke-WebRequest -Uri $GraphQLUrl -Method POST -Body $getAllDocsQuery -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.data.getAllDocuments) {
            Write-Success "Retrieved documents: $($result.data.getAllDocuments.Count)"
            foreach ($doc in $result.data.getAllDocuments) {
                Write-Host "  - $($doc.title) ($($doc.sourceType))" -ForegroundColor Gray
            }
        } else {
            Write-Warning "No documents found (this is OK if database is empty)"
        }
    } catch {
        Write-Error "Failed to get documents: $_"
    }
    
    # Test 3: Get Knowledge Gaps
    Write-Info "Test 3: Get Knowledge Gaps"
    $getGapsQuery = @{
        query = @"
{
  getKnowledgeGaps {
    totalGaps
    highPriorityGaps
    mediumPriorityGaps
    lowPriorityGaps
    gaps {
      category
      topic
      description
      priority
      suggestedSearchQuery
    }
  }
}
"@
    } | ConvertTo-Json
    
    try {
        $response = Invoke-WebRequest -Uri $GraphQLUrl -Method POST -Body $getGapsQuery -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.data.getKnowledgeGaps) {
            $gaps = $result.data.getKnowledgeGaps
            Write-Success "Knowledge gap analysis complete"
            Write-Host "  Total gaps: $($gaps.totalGaps)" -ForegroundColor Gray
            Write-Host "  High priority: $($gaps.highPriorityGaps)" -ForegroundColor Gray
            Write-Host "  Medium priority: $($gaps.mediumPriorityGaps)" -ForegroundColor Gray
            Write-Host "  Low priority: $($gaps.lowPriorityGaps)" -ForegroundColor Gray
        } else {
            Write-Warning "Knowledge gap analysis returned no data"
        }
    } catch {
        Write-Error "Failed to get knowledge gaps: $_"
    }
}

# Step 4: Test Autonomous Curation
if (-not $GraphQLOnly) {
    Write-Step "Testing Autonomous Curation Workflow"
    
    Write-Info "Starting autonomous curation (max 3 videos per gap, 10 total)"
    $curationMutation = @{
        query = @"
mutation {
  startAutonomousCuration(
    maxVideosPerGap: 3
    maxTotalVideos: 10
    minQualityScore: 0.7
  ) {
    success
    processedVideos
    acceptedVideos
    decisions {
      decisionTime
      action
      videoTitle
      videoUrl
      qualityScore
      reasoning
    }
  }
}
"@
    } | ConvertTo-Json
    
    try {
        Write-Info "This may take several minutes..."
        $response = Invoke-WebRequest -Uri $GraphQLUrl -Method POST -Body $curationMutation -ContentType "application/json" -SkipCertificateCheck -TimeoutSec 300 -ErrorAction Stop
        $result = $response.Content | ConvertFrom-Json
        
        if ($result.data.startAutonomousCuration.success) {
            $curation = $result.data.startAutonomousCuration
            Write-Success "Autonomous curation completed successfully!"
            Write-Host "`n📊 Results:" -ForegroundColor Cyan
            Write-Host "  Videos processed: $($curation.processedVideos)" -ForegroundColor Gray
            Write-Host "  Videos accepted: $($curation.acceptedVideos)" -ForegroundColor Green
            Write-Host "  Videos rejected: $($curation.processedVideos - $curation.acceptedVideos)" -ForegroundColor Yellow
            
            Write-Host "`n📋 Decisions:" -ForegroundColor Cyan
            foreach ($decision in $curation.decisions) {
                $color = if ($decision.action -eq "Accept") { "Green" } else { "Yellow" }
                Write-Host "  [$($decision.action)] $($decision.videoTitle)" -ForegroundColor $color
                Write-Host "    Quality: $($decision.qualityScore)" -ForegroundColor Gray
                Write-Host "    URL: $($decision.videoUrl)" -ForegroundColor Gray
                Write-Host "    Reasoning: $($decision.reasoning)" -ForegroundColor Gray
                Write-Host ""
            }
        } else {
            Write-Error "Autonomous curation failed"
        }
    } catch {
        Write-Error "Failed to run autonomous curation: $_"
    }
}

# Summary
Write-Host "`n╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Test Suite Complete                                       ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Explore GraphQL API at: $GraphQLUrl" -ForegroundColor Gray
Write-Host "  2. View processed documents in MongoDB" -ForegroundColor Gray
Write-Host "  3. Check Graphiti knowledge graph" -ForegroundColor Gray
Write-Host "  4. Review curation decisions" -ForegroundColor Gray

