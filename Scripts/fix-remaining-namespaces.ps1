#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix remaining namespace issues in GA.Business.Core

.DESCRIPTION
    This script fixes the remaining namespace issues by updating incomplete using statements
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING REMAINING NAMESPACE ISSUES" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""

# Define the remaining namespace fixes
$remainingFixes = @{
    # Fix incomplete namespace references
    "using Notes.Primitives;" = "using GA.Business.Core.Notes.Primitives;"
    "using Primitives;" = "using GA.Business.Core.Fretboard.Primitives;"
    "using Atonal.Abstractions;" = "using GA.Business.Core.Atonal.Abstractions;"
    "using Positions;" = "using GA.Business.Core.Fretboard.Positions;"
    "using Invariants;" = "using GA.Business.Core.Invariants;"
    "using Tonal;" = "using GA.Business.Core.Tonal;"
    "using Notes;" = "using GA.Business.Core.Notes;"
    "using Atonal;" = "using GA.Business.Core.Atonal;"
    "using Chords;" = "using GA.Business.Core.Chords;"
    "using Scales;" = "using GA.Business.Core.Scales;"
    "using Intervals;" = "using GA.Business.Core.Intervals;"
    "using Extensions;" = "using GA.Business.Core.Extensions;"
    "using Analytics;" = "using GA.Business.Core.Analytics;"
    "using Configuration;" = "using GA.Business.Core.Configuration;"
    "using AI;" = "using GA.Business.Core.AI;"
    "using Data;" = "using GA.Business.Core.Data;"
    "using Services;" = "using GA.Business.Core.Services;"
    "using Performance;" = "using GA.Business.Core.Performance;"
    "using Diagnostics;" = "using GA.Business.Core.Diagnostics;"
    "using Microservices;" = "using GA.Business.Core.Microservices;"
    "using Spatial;" = "using GA.Business.Core.Spatial;"
    "using BSP;" = "using GA.Business.Core.BSP;"
    "using Fretboard;" = "using GA.Business.Core.Fretboard;"
    
    # Fix specific namespace issues from errors
    "using GA.Business.Core.AI;" = "using GA.Business.Core.AI;"  # This should exist
    "using Tonal.Modes;" = "using GA.Business.Core.Tonal.Modes;"
    "using Scales.Modes;" = "using GA.Business.Core.Scales.Modes;"
    "using Chords.Extensions;" = "using GA.Business.Core.Chords.Extensions;"
    "using Atonal.Extensions;" = "using GA.Business.Core.Atonal.Extensions;"
    "using Notes.Extensions;" = "using GA.Business.Core.Notes.Extensions;"
    "using Intervals.Extensions;" = "using GA.Business.Core.Intervals.Extensions;"
    
    # Fix specific type references that might be missing namespace qualifiers
    "using SemanticIndexing;" = "using GA.Business.Core.Fretboard.SemanticIndexing;"
    "using CategoryTheory;" = "using GA.Business.Core.Fretboard.Shapes.CategoryTheory;"
    "using Shapes;" = "using GA.Business.Core.Fretboard.Shapes;"
    "using Analysis;" = "using GA.Business.Core.Fretboard.Analysis;"
    "using Engine;" = "using GA.Business.Core.Fretboard.Engine;"
}

function Update-RemainingNamespaces {
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
        
        # Only replace if it's not already the correct full namespace
        if ($content.Contains($oldUsing) -and -not $content.Contains($newUsing)) {
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
$csFiles = Get-ChildItem -Path $coreProjectPath -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Processing $($csFiles.Count) C# files in GA.Business.Core" -ForegroundColor Cyan
Write-Host ""

$updatedFiles = 0
foreach ($csFile in $csFiles) {
    if (Update-RemainingNamespaces -FilePath $csFile.FullName -Mappings $remainingFixes) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Updated $updatedFiles files with remaining namespace fixes" -ForegroundColor Green

if (-not $DryRun) {
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
Write-Host "Remaining namespace fixes complete!" -ForegroundColor Green
