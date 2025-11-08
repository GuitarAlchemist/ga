#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Build projects from bottom-up in dependency order

.DESCRIPTION
    This script builds projects starting from the lowest layer (GA.Core) and works up the dependency chain
#>

$ErrorActionPreference = "Stop"

Write-Host "BUILDING CORE LAYERS IN DEPENDENCY ORDER" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""

# Define the build order from lowest to highest dependency layers
$buildOrder = @(
    @{ Name = "GA.Core"; Path = "Common\GA.Core\GA.Core.csproj"; Layer = "Foundation" },
    @{ Name = "GA.Business.Config"; Path = "Common\GA.Business.Config\GA.Business.Config.csproj"; Layer = "Configuration" },
    @{ Name = "GA.Data.EntityFramework"; Path = "Common\GA.Data.EntityFramework\GA.Data.EntityFramework.csproj"; Layer = "Data" },
    @{ Name = "GA.Business.Core"; Path = "Common\GA.Business.Core\GA.Business.Core.csproj"; Layer = "Business Core" },
    @{ Name = "GA.BSP.Core"; Path = "Common\GA.BSP.Core\GA.BSP.Core.csproj"; Layer = "BSP" },
    @{ Name = "GA.Business.AI"; Path = "Common\GA.Business.AI\GA.Business.AI.csproj"; Layer = "AI" }
)

$successfulBuilds = @()
$failedBuilds = @()

foreach ($project in $buildOrder) {
    Write-Host ""
    Write-Host "Building $($project.Name) ($($project.Layer) Layer)..." -ForegroundColor Cyan
    Write-Host "Project: $($project.Path)" -ForegroundColor Gray
    
    try {
        $result = dotnet build $project.Path --verbosity minimal --no-restore
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ $($project.Name) built successfully!" -ForegroundColor Green
            $successfulBuilds += $project
        } else {
            Write-Host "❌ $($project.Name) failed to build" -ForegroundColor Red
            $failedBuilds += $project
        }
        
    } catch {
        Write-Host "❌ $($project.Name) failed with exception: $($_.Exception.Message)" -ForegroundColor Red
        $failedBuilds += $project
    }
}

Write-Host ""
Write-Host "BUILD SUMMARY" -ForegroundColor Yellow
Write-Host "=============" -ForegroundColor Yellow
Write-Host ""

Write-Host "✅ Successful Builds ($($successfulBuilds.Count)):" -ForegroundColor Green
foreach ($project in $successfulBuilds) {
    Write-Host "  - $($project.Name) ($($project.Layer))" -ForegroundColor Green
}

Write-Host ""
Write-Host "❌ Failed Builds ($($failedBuilds.Count)):" -ForegroundColor Red
foreach ($project in $failedBuilds) {
    Write-Host "  - $($project.Name) ($($project.Layer))" -ForegroundColor Red
}

Write-Host ""
if ($failedBuilds.Count -eq 0) {
    Write-Host "🎉 ALL CORE LAYERS BUILD SUCCESSFULLY!" -ForegroundColor Green
    Write-Host "The project architecture is properly organized and functional!" -ForegroundColor Green
} else {
    Write-Host "Next steps: Fix syntax issues in failed projects, starting with the lowest layer." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Core layer build test complete!" -ForegroundColor Green
