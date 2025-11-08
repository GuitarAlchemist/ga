#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Move BSP code from GA.Business.Core to GA.BSP.Core

.DESCRIPTION
    This script moves BSP-related code from GA.Business.Core to GA.BSP.Core
    where it belongs according to proper separation of concerns.
#>

$ErrorActionPreference = "Stop"

Write-Host "MOVING BSP CODE TO PROPER PROJECT" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""

$bspSourcePath = "Common\GA.Business.Core\BSP"
$spatialSourcePath = "Common\GA.Business.Core\Spatial"
$destinationPath = "Common\GA.BSP.Core"

$movedFiles = @()

# Function to move files and track them
function Move-BSPFiles {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$SubFolder = ""
    )
    
    if (-not (Test-Path $SourcePath)) {
        Write-Host "Source path not found: $SourcePath" -ForegroundColor Yellow
        return
    }
    
    $files = Get-ChildItem -Path $SourcePath -File -Filter "*.cs"
    
    foreach ($file in $files) {
        $destDir = if ($SubFolder) { Join-Path $DestinationPath $SubFolder } else { $DestinationPath }
        
        if (-not (Test-Path $destDir)) {
            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
        }
        
        $destFile = Join-Path $destDir $file.Name
        
        Write-Host "Moving: $($file.Name) -> $destDir" -ForegroundColor Cyan
        Move-Item -Path $file.FullName -Destination $destFile -Force
        
        $script:movedFiles += $destFile
    }
}

try {
    # Move BSP directory files
    if (Test-Path $bspSourcePath) {
        Write-Host "Moving BSP directory files..." -ForegroundColor Cyan
        Move-BSPFiles -SourcePath $bspSourcePath -DestinationPath $destinationPath -SubFolder "BSP"
        
        # Remove empty BSP directory
        if ((Get-ChildItem -Path $bspSourcePath -Force | Measure-Object).Count -eq 0) {
            Remove-Item -Path $bspSourcePath -Force
            Write-Host "Removed empty BSP directory" -ForegroundColor Green
        }
    }
    
    # Move Spatial directory BSP files
    if (Test-Path $spatialSourcePath) {
        Write-Host "Moving Spatial BSP files..." -ForegroundColor Cyan
        $spatialFiles = Get-ChildItem -Path $spatialSourcePath -File -Filter "*BSP*.cs"
        
        foreach ($file in $spatialFiles) {
            $destDir = Join-Path $destinationPath "Spatial"
            if (-not (Test-Path $destDir)) {
                New-Item -Path $destDir -ItemType Directory -Force | Out-Null
            }
            
            $destFile = Join-Path $destDir $file.Name
            Write-Host "Moving: $($file.Name) -> $destDir" -ForegroundColor Cyan
            Move-Item -Path $file.FullName -Destination $destFile -Force
            $movedFiles += $destFile
        }
    }
    
    Write-Host ""
    Write-Host "Successfully moved BSP code!" -ForegroundColor Green
    
    # List what was moved
    Write-Host ""
    Write-Host "Moved files:" -ForegroundColor Cyan
    foreach ($file in $movedFiles) {
        Write-Host "  $($file.Replace((Get-Location).Path + '\', ''))" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "BSP code has been successfully moved to GA.BSP.Core project!" -ForegroundColor Green
    Write-Host "This follows proper separation of concerns - BSP code should be in the BSP project." -ForegroundColor Green
    
} catch {
    Write-Host "Error moving BSP code: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "BSP code move complete!" -ForegroundColor Green
