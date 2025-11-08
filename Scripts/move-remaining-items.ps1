#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Move remaining non-core items from GA.Business.Core

.DESCRIPTION
    This script moves the last remaining items that don't belong in core business logic
#>

$ErrorActionPreference = "Stop"

Write-Host "MOVING REMAINING NON-CORE ITEMS" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

# Move Spatial to GA.BSP.Core (it already exists there, so merge)
$spatialSource = "Common\GA.Business.Core\Spatial"
$spatialDest = "Common\GA.BSP.Core\Spatial"

if (Test-Path $spatialSource) {
    Write-Host "Moving Spatial directory to GA.BSP.Core..." -ForegroundColor Cyan
    
    if (Test-Path $spatialDest) {
        # Merge - move individual files
        $spatialFiles = Get-ChildItem -Path $spatialSource -File -Recurse
        foreach ($file in $spatialFiles) {
            $relativePath = $file.FullName.Substring($spatialSource.Length + 1)
            $destFile = Join-Path $spatialDest $relativePath
            $destDir = Split-Path $destFile -Parent
            
            if (-not (Test-Path $destDir)) {
                New-Item -Path $destDir -ItemType Directory -Force | Out-Null
            }
            
            Move-Item -Path $file.FullName -Destination $destFile -Force
            Write-Host "  Moved: $relativePath" -ForegroundColor Green
        }
        
        # Remove empty source directory
        Remove-Item -Path $spatialSource -Recurse -Force
        Write-Host "  Removed empty Spatial directory from GA.Business.Core" -ForegroundColor Green
    } else {
        # Simple move
        Move-Item -Path $spatialSource -Destination $spatialDest -Force
        Write-Host "  ✅ Moved Spatial directory to GA.BSP.Core" -ForegroundColor Green
    }
} else {
    Write-Host "Spatial directory not found - already moved" -ForegroundColor Yellow
}

# Check service-related extensions that should be moved
$extensionsPath = "Common\GA.Business.Core\Extensions"
if (Test-Path $extensionsPath) {
    Write-Host ""
    Write-Host "Analyzing Extensions directory..." -ForegroundColor Cyan
    
    $serviceExtensions = @(
        "AIServiceExtensions.cs",
        "FretboardServiceExtensions.cs", 
        "GrothendieckServiceExtensions.cs",
        "ServiceCollectionExtensions.cs",
        "TonalBSPServiceExtensions.cs"
    )
    
    # Create Extensions directory in service projects
    $serviceExtensionsDir = "Common\GA.Business.Services\Extensions"
    if (-not (Test-Path $serviceExtensionsDir)) {
        New-Item -Path $serviceExtensionsDir -ItemType Directory -Force | Out-Null
        Write-Host "  Created service extensions directory" -ForegroundColor Green
    }
    
    foreach ($ext in $serviceExtensions) {
        $extFile = Join-Path $extensionsPath $ext
        if (Test-Path $extFile) {
            $destFile = Join-Path $serviceExtensionsDir $ext
            Move-Item -Path $extFile -Destination $destFile -Force
            Write-Host "  ✅ Moved service extension: $ext" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "FINAL CLEANUP SUMMARY" -ForegroundColor Yellow
Write-Host "=====================" -ForegroundColor Yellow
Write-Host ""

Write-Host "GA.Business.Core now contains only:" -ForegroundColor Green
Write-Host "  ✅ Core domain entities (Notes, Intervals, Chords, Scales)" -ForegroundColor Green
Write-Host "  ✅ Core business logic (Atonal, Tonal, Fretboard)" -ForegroundColor Green  
Write-Host "  ✅ Core business rules (Invariants)" -ForegroundColor Green
Write-Host "  ✅ Essential domain extensions" -ForegroundColor Green

Write-Host ""
Write-Host "Moved to appropriate projects:" -ForegroundColor Cyan
Write-Host "  📁 Configuration → GA.Business.Config" -ForegroundColor Cyan
Write-Host "  📁 Data → GA.Data.EntityFramework" -ForegroundColor Cyan
Write-Host "  📁 Analytics → GA.Business.Analytics" -ForegroundColor Cyan
Write-Host "  📁 Services → Specialized service projects" -ForegroundColor Cyan
Write-Host "  📁 Spatial → GA.BSP.Core" -ForegroundColor Cyan

Write-Host ""
Write-Host "✅ GA.Business.Core is now properly focused on core business domain!" -ForegroundColor Green

Write-Host ""
Write-Host "Remaining items move complete!" -ForegroundColor Green
