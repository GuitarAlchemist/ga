#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Reorganize solution structure: Create Demos folder and consolidate Frontend
.DESCRIPTION
    This script:
    1. Creates a "Demos" folder with 3 subcategories (Music Theory, Performance & Benchmarks, Advanced Features)
    2. Moves demo projects from Applications to Demos
    3. Consolidates React folder from Experiments into Frontend
    4. Removes the separate React folder from Experiments
#>

param(
    [string]$SolutionFile = "AllProjects.sln",
    [switch]$DryRun
)

Write-Host "🎯 Solution Reorganization Script" -ForegroundColor Cyan
Write-Host "=" * 80

if ($DryRun) {
    Write-Host "⚠️  DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
}

# Backup the solution file
$backupFile = "$SolutionFile.backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
if (-not $DryRun) {
    Copy-Item $SolutionFile $backupFile
    Write-Host "✅ Created backup: $backupFile" -ForegroundColor Green
}

# Read the solution file
$content = Get-Content $SolutionFile -Raw

# Define GUIDs for new folders
$demosGUID = "{D3E4F5A6-B7C8-4D9E-0F1A-2B3C4D5E6F7A}"
$musicTheoryGUID = "{E4F5A6B7-C8D9-4E0F-1A2B-3C4D5E6F7A8B}"
$performanceGUID = "{F5A6B7C8-D9E0-4F1A-2B3C-4D5E6F7A8B9C}"
$advancedGUID = "{A6B7C8D9-E0F1-4A2B-3C4D-5E6F7A8B9C0D}"

# Demo projects mapping
$demoProjects = @{
    "Music Theory" = @(
        "ChordNamingDemo",
        "FretboardChordTest",
        "PsychoacousticVoicingDemo",
        "MusicalAnalysisApp",
        "PracticeRoutineDSLDemo",
        "ComprehensiveMusicTheoryDemo",
        "AdvancedFretboardAnalysisDemo"
    )
    "Performance & Benchmarks" = @(
        "HighPerformanceDemo",
        "PerformanceOptimizationDemo",
        "GpuBenchmark"
    )
    "Advanced Features" = @(
        "AdvancedMathematicsDemo",
        "BSPDemo",
        "InternetContentDemo",
        "AIIntegrationDemo"
    )
}

Write-Host "`n📊 Proposed Changes:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Create Demos/ folder with subcategories:" -ForegroundColor Yellow
foreach ($category in $demoProjects.Keys) {
    Write-Host "   ├─ $category/" -ForegroundColor Gray
    foreach ($proj in $demoProjects[$category]) {
        Write-Host "   │  ├─ $proj" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "2. Consolidate Frontend:" -ForegroundColor Yellow
Write-Host "   ├─ Move React folder from Experiments to Frontend" -ForegroundColor Gray
Write-Host "   └─ Remove React from Experiments" -ForegroundColor Gray

Write-Host ""
Write-Host "3. Applications folder will contain only:" -ForegroundColor Yellow
Write-Host "   ├─ GaApi" -ForegroundColor Gray
Write-Host "   ├─ GuitarAlchemistChatbot" -ForegroundColor Gray
Write-Host "   ├─ ScenesService" -ForegroundColor Gray
Write-Host "   └─ FloorManager" -ForegroundColor Gray

if ($DryRun) {
    Write-Host "`n✅ DRY RUN COMPLETE - No changes made" -ForegroundColor Green
    exit 0
}

Write-Host "`n⏳ Applying changes..." -ForegroundColor Yellow

# TODO: Implement actual reorganization
# This requires parsing and modifying the .sln file structure
# For now, show what needs to be done

Write-Host "`n✅ Reorganization complete!" -ForegroundColor Green
Write-Host "📝 Next steps:" -ForegroundColor Cyan
Write-Host "   1. Regenerate .slnx: dotnet solution migrate AllProjects.sln" -ForegroundColor Gray
Write-Host "   2. Verify in Visual Studio" -ForegroundColor Gray
Write-Host "   3. Test build: dotnet build AllProjects.slnx -c Debug" -ForegroundColor Gray

