#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Move high-level services from GA.Business.Core to appropriate service projects

.DESCRIPTION
    This script moves application-level services that don't belong in the core business layer
#>

$ErrorActionPreference = "Stop"

Write-Host "MOVING HIGH-LEVEL SERVICES" -ForegroundColor Green
Write-Host "==========================" -ForegroundColor Green
Write-Host ""

$sourcePath = "Common\GA.Business.Core\Services"

# Create service project directories
$serviceProjects = @(
    "Common\GA.Business.Analytics",
    "Common\GA.Business.Configuration", 
    "Common\GA.Business.Validation",
    "Common\GA.Business.Personalization"
)

foreach ($project in $serviceProjects) {
    if (-not (Test-Path $project)) {
        New-Item -Path $project -ItemType Directory -Force | Out-Null
        Write-Host "Created service project directory: $project" -ForegroundColor Green
    }
}

# Define service moves
$serviceMoves = @(
    @{ File = "AdvancedMusicalAnalyticsService.cs"; Destination = "Common\GA.Business.Analytics\Services" },
    @{ File = "MusicalAnalyticsService.cs"; Destination = "Common\GA.Business.Analytics\Services" },
    @{ File = "ConfigurationBroadcastService.cs"; Destination = "Common\GA.Business.Configuration\Services" },
    @{ File = "ConfigurationWatcherService.cs"; Destination = "Common\GA.Business.Configuration\Services" },
    @{ File = "MusicalKnowledgeCacheService.cs"; Destination = "Common\GA.Business.Configuration\Services" },
    @{ File = "CachedInvariantValidationService.cs"; Destination = "Common\GA.Business.Validation\Services" },
    @{ File = "InvariantValidationService.cs"; Destination = "Common\GA.Business.Validation\Services" },
    @{ File = "RealtimeInvariantMonitoringService.cs"; Destination = "Common\GA.Business.Validation\Services" },
    @{ File = "EnhancedUserPersonalizationService.cs"; Destination = "Common\GA.Business.Personalization\Services" },
    @{ File = "UserPersonalizationService.cs"; Destination = "Common\GA.Business.Personalization\Services" }
)

$movedServices = @()

foreach ($move in $serviceMoves) {
    $sourceFile = Join-Path $sourcePath $move.File
    
    if (Test-Path $sourceFile) {
        # Create destination directory
        if (-not (Test-Path $move.Destination)) {
            New-Item -Path $move.Destination -ItemType Directory -Force | Out-Null
        }
        
        $destFile = Join-Path $move.Destination $move.File
        
        try {
            Move-Item -Path $sourceFile -Destination $destFile -Force
            Write-Host "✅ Moved: $($move.File) -> $(Split-Path $move.Destination -Leaf)" -ForegroundColor Green
            $movedServices += $move
        } catch {
            Write-Host "❌ Failed to move $($move.File): $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "⚠️ File not found: $($move.File)" -ForegroundColor Yellow
    }
}

# Remove empty Services directory if all files were moved
if ((Get-ChildItem -Path $sourcePath -Force | Measure-Object).Count -eq 0) {
    Remove-Item -Path $sourcePath -Force
    Write-Host "Removed empty Services directory from GA.Business.Core" -ForegroundColor Green
}

Write-Host ""
Write-Host "SERVICE MOVE SUMMARY" -ForegroundColor Yellow
Write-Host "===================" -ForegroundColor Yellow
Write-Host ""

Write-Host "✅ Successfully moved $($movedServices.Count) services:" -ForegroundColor Green
$groupedMoves = $movedServices | Group-Object { Split-Path $_.Destination -Leaf }
foreach ($group in $groupedMoves) {
    Write-Host "  $($group.Name): $($group.Count) services" -ForegroundColor Green
}

Write-Host ""
Write-Host "GA.Business.Core is now focused on core domain logic only!" -ForegroundColor Green
Write-Host "High-level services have been moved to appropriate specialized projects." -ForegroundColor Green

Write-Host ""
Write-Host "Service move complete!" -ForegroundColor Green
