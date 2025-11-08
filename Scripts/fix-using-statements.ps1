#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix all using statements in GA.Business.Core project

.DESCRIPTION
    This script updates all using statements to reference the correct namespaces
    after the project reorganization from GA.Business.Core.* to GA.Business.*
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING USING STATEMENTS IN GA.BUSINESS.CORE" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN - Showing what would be updated:" -ForegroundColor Yellow
    Write-Host ""
}

# Define the namespace mappings based on the errors we saw
$usingMappings = @{
    # Core music theory namespaces
    "using Atonal;" = "using GA.Core.Atonal;"
    "using Notes;" = "using GA.Core.Notes;"
    "using Tonal;" = "using GA.Core.Tonal;"
    "using Intervals;" = "using GA.Core.Intervals;"
    "using Chords;" = "using GA.Core.Chords;"
    "using Scales;" = "using GA.Core.Scales;"
    
    # Analytics and configuration
    "using Analytics;" = "using GA.Business.Analysis;"
    "using Invariants;" = "using GA.Business.Analysis.Invariants;"
    "using Configuration;" = "using GA.Business.Config;"
    
    # Extensions and utilities
    "using Extensions;" = "using GA.Core.Extensions;"
    
    # Specific type imports that might be missing
    "using PitchClass;" = "using GA.Core.Atonal;"
    "using PitchClassSet;" = "using GA.Core.Atonal;"
    "using IntervalClassVector;" = "using GA.Core.Atonal;"
    "using Note;" = "using GA.Core.Notes;"
    "using Pitch;" = "using GA.Core.Notes;"
    "using Key;" = "using GA.Core.Tonal;"
    "using ScaleMode;" = "using GA.Core.Scales;"
    "using ChordFunction;" = "using GA.Core.Chords;"
    "using ChordTemplate;" = "using GA.Core.Chords;"
    "using Interval;" = "using GA.Core.Intervals;"
    
    # Business logic namespaces
    "using InvariantAnalytics;" = "using GA.Business.Analysis;"
    "using ViolationEvent;" = "using GA.Business.Analysis;"
    "using PerformanceInsights;" = "using GA.Business.Analysis;"
    "using ViolationTrends;" = "using GA.Business.Analysis;"
    "using InvariantAnalyticsService;" = "using GA.Business.Analysis;"
    
    # Custom types that might need specific imports
    "using UltraFastDocument;" = "using GA.Business.Core.SemanticIndexing;"
    "using UltraIndexingProgress;" = "using GA.Business.Core.SemanticIndexing;"
    "using GrothendieckDelta;" = "using GA.Business.Core.CategoryTheory;"
    "using IGrothendieckService;" = "using GA.Business.Core.CategoryTheory;"
}

function Update-UsingStatements {
    param(
        [string]$FilePath,
        [hashtable]$Mappings
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    $changed = $false
    
    foreach ($mapping in $Mappings.GetEnumerator()) {
        $oldUsing = $mapping.Key
        $newUsing = $mapping.Value
        
        if ($content.Contains($oldUsing)) {
            $content = $content.Replace($oldUsing, $newUsing)
            $changed = $true
            Write-Host "  - $oldUsing -> $newUsing" -ForegroundColor Gray
        }
    }
    
    if ($changed) {
        Write-Host "Updated: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

# Get all C# files in GA.Business.Core
$coreProjectPath = "Common/GA.Business.Core"
if (-not (Test-Path $coreProjectPath)) {
    Write-Host "GA.Business.Core project not found at: $coreProjectPath" -ForegroundColor Red
    exit 1
}

$csFiles = Get-ChildItem -Path $coreProjectPath -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Found $($csFiles.Count) C# files in GA.Business.Core" -ForegroundColor Cyan
Write-Host ""

$updatedFiles = 0
foreach ($csFile in $csFiles) {
    if (Update-UsingStatements -FilePath $csFile.FullName -Mappings $usingMappings) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Updated $updatedFiles files with corrected using statements" -ForegroundColor Green

if ($DryRun) {
    Write-Host ""
    Write-Host "Run without -DryRun to apply the changes" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "Testing build..." -ForegroundColor Cyan
    
    # Test build the GA.Business.Core project
    dotnet build $coreProjectPath/GA.Business.Core.csproj --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GA.Business.Core builds successfully!" -ForegroundColor Green
    } else {
        Write-Host "GA.Business.Core still has build issues - may need additional fixes" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Using statement fixes complete!" -ForegroundColor Green
