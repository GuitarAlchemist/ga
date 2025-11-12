#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Migrates all controllers from GaApi to microservices
#>

$ErrorActionPreference = "Stop"

# Define controller migrations
$migrations = @{
    "GA.MusicTheory.Service" = @(
        "MusicTheoryController.cs",
        "ChordsController.cs", 
        "DslController.cs"
    )
    "GA.BSP.Service" = @(
        "BSPController.cs",
        "BSPRoomController.cs",
        "MusicRoomController.cs",
        "IntelligentBSPController.cs"
    )
    "GA.AI.Service" = @(
        "SemanticSearchController.cs",
        "VectorSearchController.cs",
        "VectorSearchStrategyController.cs",
        "AdvancedAIController.cs",
        "AdaptiveAIController.cs",
        "EnhancedPersonalizationController.cs"
    )
    "GA.Knowledge.Service" = @(
        "MusicalKnowledgeController.cs",
        "GuitarTechniquesController.cs",
        "SpecializedTuningsController.cs",
        "AssetsController.cs",
        "AssetRelationshipsController.cs",
        "InstrumentsController.cs"
    )
    "GA.Fretboard.Service" = @(
        "GuitarPlayingController.cs",
        "BiomechanicsController.cs",
        "ContextualChordsController.cs",
        "ChordProgressionsController.cs",
        "MonadicChordsController.cs",
        "GuitarAgentTasksController.cs"
    )
    "GA.Analytics.Service" = @(
        "SpectralAnalyticsController.cs",
        "GrothendieckController.cs",
        "InvariantsController.cs",
        "AdvancedAnalyticsController.cs",
        "MetricsController.cs"
    )
}

$gaApiPath = Join-Path $PSScriptRoot "..\Apps\ga-server\GaApi\Controllers"
$totalMigrated = 0
$totalSkipped = 0

Write-Host "🚀 Starting controller migration..." -ForegroundColor Cyan
Write-Host ""

foreach ($service in $migrations.Keys) {
    $servicePath = Join-Path $PSScriptRoot "..\Apps\ga-server\$service"
    $controllersPath = Join-Path $servicePath "Controllers"
    
    Write-Host "📦 Migrating to $service..." -ForegroundColor Yellow
    
    # Ensure Controllers directory exists
    if (-not (Test-Path $controllersPath)) {
        New-Item -ItemType Directory -Path $controllersPath -Force | Out-Null
    }
    
    foreach ($controller in $migrations[$service]) {
        $sourcePath = Join-Path $gaApiPath $controller
        $destPath = Join-Path $controllersPath $controller
        
        if (Test-Path $sourcePath) {
            # Copy controller
            Copy-Item $sourcePath $destPath -Force
            
            # Update namespace
            $content = Get-Content $destPath -Raw
            $namespace = "$service.Controllers"
            $content = $content.Replace('namespace GaApi.Controllers;', "namespace $namespace;")
            $content = $content.Replace('using GaApi.', "using $service.")
            Set-Content $destPath -Value $content -NoNewline
            
            Write-Host "  ✅ Migrated $controller" -ForegroundColor Green
            $totalMigrated++
        } else {
            Write-Host "  ⏭️  Skipped $controller (not found)" -ForegroundColor DarkGray
            $totalSkipped++
        }
    }
    
    Write-Host ""
}

Write-Host "✨ Migration complete!" -ForegroundColor Green
Write-Host "   Migrated: $totalMigrated controllers" -ForegroundColor White
Write-Host "   Skipped: $totalSkipped controllers" -ForegroundColor White

