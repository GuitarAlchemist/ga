#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Move Spatial directory to GA.BSP.Core

.DESCRIPTION
    This script moves the Spatial directory from GA.Business.Core to GA.BSP.Core
#>

$ErrorActionPreference = "Stop"

Write-Host "MOVING SPATIAL TO GA.BSP.CORE" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green
Write-Host ""

$spatialSource = "Common\GA.Business.Core\Spatial"
$spatialDest = "Common\GA.BSP.Core\Spatial"

if (Test-Path $spatialSource) {
    Write-Host "Moving Spatial directory..." -ForegroundColor Cyan
    
    if (Test-Path $spatialDest) {
        Write-Host "Destination exists - merging files..." -ForegroundColor Yellow
        
        # Get all files from source
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
        Write-Host "  Successfully moved Spatial directory" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "Spatial code moved to GA.BSP.Core successfully!" -ForegroundColor Green
    
} else {
    Write-Host "Spatial directory not found - may have been moved already" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Spatial move complete!" -ForegroundColor Green
