#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Rename GA.Business.Core.* projects to GA.Business.* for cleaner structure

.DESCRIPTION
    This script systematically renames the GA.Business.Core.* projects to remove
    the unnecessary "Core" nesting and create a cleaner project structure.
    
    Renames:
    - GA.Business.Core.AI → GA.Business.AI
    - GA.Business.Core.Analysis → GA.Business.Analysis
    - GA.Business.Core.Fretboard → GA.Business.Fretboard
    - GA.Business.Core.Harmony → GA.Business.Harmony
    - GA.Business.Core.Orchestration → GA.Business.Orchestration
    - GA.Business.Core.UI → GA.Business.UI
    - GA.Business.Core.Web → GA.Business.Web
    - GA.Business.Core.Mapping → GA.Business.Mapping
    - GA.Business.Core.Intelligence → GA.Business.Intelligence

.PARAMETER DryRun
    Show what would be renamed without actually doing it

.EXAMPLE
    ./rename-business-core-projects.ps1 -DryRun
    ./rename-business-core-projects.ps1
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "🔄 GA.Business.Core.* Project Renaming" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Define the renaming mappings
$renameMappings = @{
    "Common/GA.Business.Core.AI" = "Common/GA.Business.AI"
    "Common/GA.Business.Core.Analysis" = "Common/GA.Business.Analysis"
    "Common/GA.Business.Core.Fretboard" = "Common/GA.Business.Fretboard"
    "Common/GA.Business.Core.Harmony" = "Common/GA.Business.Harmony"
    "Common/GA.Business.Core.Orchestration" = "Common/GA.Business.Orchestration"
    "Common/GA.Business.Core.UI" = "Common/GA.Business.UI"
    "Common/GA.Business.Core.Web" = "Common/GA.Business.Web"
    "Common/GA.Business.Core.Mapping" = "Common/GA.Business.Mapping"
    "Common/GA.Business.Core.Intelligence" = "Common/GA.Business.Intelligence"
}

if ($DryRun) {
    Write-Host "🔍 DRY RUN - Showing what would be renamed:" -ForegroundColor Yellow
    Write-Host ""
}

foreach ($mapping in $renameMappings.GetEnumerator()) {
    $oldPath = $mapping.Key
    $newPath = $mapping.Value
    
    if (Test-Path $oldPath) {
        Write-Host "📁 $oldPath → $newPath" -ForegroundColor Green
        
        if (-not $DryRun) {
            # Create parent directory if it doesn't exist
            $parentDir = Split-Path $newPath -Parent
            if (-not (Test-Path $parentDir)) {
                New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
            }
            
            # Rename the directory
            Move-Item -Path $oldPath -Destination $newPath -Force
            Write-Host "  ✅ Renamed successfully" -ForegroundColor Green
        }
    } else {
        Write-Host "⚠️  $oldPath (not found)" -ForegroundColor Yellow
    }
}

if (-not $DryRun) {
    Write-Host ""
    Write-Host "📝 Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Update AllProjects.sln with new project paths" -ForegroundColor White
    Write-Host "  2. Update all ProjectReference paths in .csproj files" -ForegroundColor White
    Write-Host "  3. Update namespace declarations in source files" -ForegroundColor White
    Write-Host "  4. Update using statements throughout the codebase" -ForegroundColor White
    Write-Host ""
    Write-Host "🚀 Run the update script to complete the renaming:" -ForegroundColor Green
    Write-Host "  ./Scripts/update-project-references.ps1" -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "💡 Run without -DryRun to perform the actual renaming" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "✨ Project structure will be much cleaner!" -ForegroundColor Green
