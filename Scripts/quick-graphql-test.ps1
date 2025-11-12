#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick GraphQL API Test
.DESCRIPTION
    Quick test of the GraphQL API endpoints without starting services
#>

$ErrorActionPreference = "Stop"

# Color output functions
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }

$GraphQLUrl = "https://localhost:7001/graphql"

Write-Host "`n🧪 Quick GraphQL API Test`n" -ForegroundColor Cyan

# Test 1: GraphQL Introspection
Write-Info "Testing GraphQL schema introspection..."
$introspectionQuery = @{
    query = "{ __schema { queryType { name } mutationType { name } } }"
} | ConvertTo-Json

try {
    $response = Invoke-WebRequest -Uri $GraphQLUrl -Method POST -Body $introspectionQuery -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
    $result = $response.Content | ConvertFrom-Json
    
    if ($result.data.__schema) {
        Write-Success "GraphQL API is working!"
        Write-Host "  Query type: $($result.data.__schema.queryType.name)" -ForegroundColor Gray
        Write-Host "  Mutation type: $($result.data.__schema.mutationType.name)" -ForegroundColor Gray
    }
} catch {
    Write-Error "GraphQL API is not responding. Is GaApi running?"
    Write-Host "`nTo start GaApi:" -ForegroundColor Yellow
    Write-Host "  cd Apps/ga-server/GaApi" -ForegroundColor Gray
    Write-Host "  dotnet run" -ForegroundColor Gray
    exit 1
}

# Test 2: Get Knowledge Gaps
Write-Info "Testing knowledge gap analysis..."
$getGapsQuery = @{
    query = @"
{
  getKnowledgeGaps {
    totalGaps
    highPriorityGaps
    gaps {
      category
      topic
      priority
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
        Write-Success "Knowledge gap analysis working!"
        Write-Host "  Total gaps: $($gaps.totalGaps)" -ForegroundColor Gray
        Write-Host "  High priority: $($gaps.highPriorityGaps)" -ForegroundColor Gray
        
        if ($gaps.gaps.Count -gt 0) {
            Write-Host "`n  Top gaps:" -ForegroundColor Gray
            $gaps.gaps | Select-Object -First 3 | ForEach-Object {
                Write-Host "    - $($_.category): $($_.topic) [$($_.priority)]" -ForegroundColor Gray
            }
        }
    }
} catch {
    Write-Error "Failed to get knowledge gaps: $_"
}

# Test 3: Get All Documents
Write-Info "Testing document retrieval..."
$getAllDocsQuery = @{
    query = "{ getAllDocuments(skip: 0, take: 5) { id title sourceType } }"
} | ConvertTo-Json

try {
    $response = Invoke-WebRequest -Uri $GraphQLUrl -Method POST -Body $getAllDocsQuery -ContentType "application/json" -SkipCertificateCheck -ErrorAction Stop
    $result = $response.Content | ConvertFrom-Json
    
    if ($result.data.getAllDocuments) {
        Write-Success "Document retrieval working!"
        Write-Host "  Documents found: $($result.data.getAllDocuments.Count)" -ForegroundColor Gray
        
        if ($result.data.getAllDocuments.Count -gt 0) {
            Write-Host "`n  Recent documents:" -ForegroundColor Gray
            $result.data.getAllDocuments | ForEach-Object {
                Write-Host "    - $($_.title) ($($_.sourceType))" -ForegroundColor Gray
            }
        }
    }
} catch {
    Write-Error "Failed to get documents: $_"
}

Write-Host "`n✨ GraphQL API test complete!`n" -ForegroundColor Green
Write-Host "GraphQL Playground: $GraphQLUrl" -ForegroundColor Cyan

