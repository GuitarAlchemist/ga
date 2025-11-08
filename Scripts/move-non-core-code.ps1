#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Move non-core code from GA.Business.Core to appropriate higher-level projects

.DESCRIPTION
    This script moves code that doesn't belong in the core business layer to appropriate projects
#>

$ErrorActionPreference = "Stop"

Write-Host "MOVING NON-CORE CODE TO APPROPRIATE PROJECTS" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

$sourcePath = "Common\GA.Business.Core"

# Define what should be moved and where
$moveOperations = @(
    @{
        Source = "$sourcePath\Configuration"
        Destination = "Common\GA.Business.Config\Configuration"
        Description = "Configuration loaders belong in GA.Business.Config"
    },
    @{
        Source = "$sourcePath\Config"
        Destination = "Common\GA.Business.Config\Config"
        Description = "Config classes belong in GA.Business.Config"
    },
    @{
        Source = "$sourcePath\Data"
        Destination = "Common\GA.Data.EntityFramework\Data"
        Description = "Data context belongs in GA.Data.EntityFramework"
    },
    @{
        Source = "$sourcePath\Examples"
        Destination = "Examples\BusinessCore"
        Description = "Examples should be in Examples directory"
    },
    @{
        Source = "$sourcePath\Analytics"
        Destination = "Common\GA.Business.Analytics\Analytics"
        Description = "Analytics should be in separate project"
    },
    @{
        Source = "$sourcePath\Assets"
        Destination = "Common\GA.Business.Assets\Assets"
        Description = "Assets should be in separate project"
    },
    @{
        Source = "$sourcePath\Diagnostics"
        Destination = "Common\GA.Business.Diagnostics\Diagnostics"
        Description = "Diagnostics should be in separate project"
    },
    @{
        Source = "$sourcePath\Microservices"
        Destination = "Common\GA.Business.Microservices\Microservices"
        Description = "Microservices should be in separate project"
    },
    @{
        Source = "$sourcePath\Performance"
        Destination = "Common\GA.Business.Performance\Performance"
        Description = "Performance should be in separate project"
    }
)

$movedItems = @()
$skippedItems = @()

foreach ($operation in $moveOperations) {
    Write-Host ""
    Write-Host "Moving: $($operation.Source)" -ForegroundColor Cyan
    Write-Host "To: $($operation.Destination)" -ForegroundColor Cyan
    Write-Host "Reason: $($operation.Description)" -ForegroundColor Gray
    
    if (-not (Test-Path $operation.Source)) {
        Write-Host "  Source not found - skipping" -ForegroundColor Yellow
        $skippedItems += $operation
        continue
    }
    
    try {
        # Create destination directory if it doesn't exist
        $destDir = Split-Path $operation.Destination -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -Path $destDir -ItemType Directory -Force | Out-Null
            Write-Host "  Created destination directory: $destDir" -ForegroundColor Green
        }
        
        # Move the directory
        Move-Item -Path $operation.Source -Destination $operation.Destination -Force
        Write-Host "  ✅ Successfully moved!" -ForegroundColor Green
        $movedItems += $operation
        
    } catch {
        Write-Host "  ❌ Failed to move: $($_.Exception.Message)" -ForegroundColor Red
        $skippedItems += $operation
    }
}

Write-Host ""
Write-Host "MOVE SUMMARY" -ForegroundColor Yellow
Write-Host "============" -ForegroundColor Yellow
Write-Host ""

Write-Host "✅ Successfully Moved ($($movedItems.Count)):" -ForegroundColor Green
foreach ($item in $movedItems) {
    Write-Host "  - $(Split-Path $item.Source -Leaf) -> $(Split-Path $item.Destination -Parent)" -ForegroundColor Green
}

Write-Host ""
Write-Host "⚠️ Skipped ($($skippedItems.Count)):" -ForegroundColor Yellow
foreach ($item in $skippedItems) {
    Write-Host "  - $(Split-Path $item.Source -Leaf)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "GA.Business.Core is now cleaner and focused on core business logic!" -ForegroundColor Green
Write-Host "Next: Update project references and namespaces in moved code." -ForegroundColor Cyan

Write-Host ""
Write-Host "Non-core code move complete!" -ForegroundColor Green
