#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Add missing Primitives namespace imports to GA.Business.Core files

.DESCRIPTION
    This script adds the correct using statements for Primitives subdirectories
    based on the types that files are trying to use
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "ADDING MISSING PRIMITIVES IMPORTS" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""

# Define type to namespace mappings
$typeToNamespace = @{
    # Fretboard Primitives
    "Position" = "using GA.Business.Core.Fretboard.Primitives;"
    "Str" = "using GA.Business.Core.Fretboard.Primitives;"
    "Fret" = "using GA.Business.Core.Fretboard.Primitives;"
    "IStr" = "using GA.Business.Core.Fretboard.Primitives;"
    "IFret" = "using GA.Business.Core.Fretboard.Primitives;"
    "RelativeFret" = "using GA.Business.Core.Fretboard.Primitives;"
    "RelativeFretVector" = "using GA.Business.Core.Fretboard.Primitives;"
    "PositionLocation" = "using GA.Business.Core.Fretboard.Positions;"
    
    # Notes Primitives
    "NaturalNote" = "using GA.Business.Core.Notes.Primitives;"
    "MidiNote" = "using GA.Business.Core.Notes.Primitives;"
    "SharpAccidental" = "using GA.Business.Core.Notes.Primitives;"
    "FlatAccidental" = "using GA.Business.Core.Notes.Primitives;"
    
    # Intervals Primitives
    "IIntervalSize" = "using GA.Business.Core.Intervals.Primitives;"
    "IntervalQuality" = "using GA.Business.Core.Intervals.Primitives;"
    "SimpleIntervalSize" = "using GA.Business.Core.Intervals.Primitives;"
    "CompoundIntervalSize" = "using GA.Business.Core.Intervals.Primitives;"
    "Semitones" = "using GA.Business.Core.Intervals.Primitives;"
    "IntervalConsonance" = "using GA.Business.Core.Intervals.Primitives;"
    "Accidental" = "using GA.Business.Core.Intervals.Primitives;"
    
    # Atonal Primitives
    "IntervalClass" = "using GA.Business.Core.Atonal.Primitives;"
    "Cardinality" = "using GA.Business.Core.Atonal.Primitives;"
    "PitchClassSetId" = "using GA.Business.Core.Atonal.Primitives;"
    
    # Other missing types
    "GenericScaleDegree" = "using GA.Business.Core.Tonal.Primitives;"
    "ChordInvariant" = "using GA.Business.Core.Fretboard.Invariants;"
    "CagedAnalysis" = "using GA.Business.Core.Fretboard.Analysis;"
    "FingeringAnalysis" = "using GA.Business.Core.Fretboard.Analysis;"
    "FretboardChordAnalyzer" = "using GA.Business.Core.Fretboard.Analysis;"
    "PhysicalFretboardCalculator" = "using GA.Business.Core.Fretboard.Analysis;"
    "UltraFastDocument" = "using GA.Business.Core.Fretboard.SemanticIndexing;"
    "UltraIndexingProgress" = "using GA.Business.Core.Fretboard.SemanticIndexing;"
    "GrothendieckDelta" = "using GA.Business.Core.Fretboard.Shapes.CategoryTheory;"
    "IGrothendieckService" = "using GA.Business.Core.Fretboard.Shapes.CategoryTheory;"
}

function Add-MissingImports {
    param(
        [string]$FilePath,
        [hashtable]$TypeMappings
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    $changed = $false
    $addedImports = @()
    
    # Check which types are used in the file and add corresponding imports
    foreach ($type in $TypeMappings.Keys) {
        $usingStatement = $TypeMappings[$type]
        
        # Check if the type is used in the file and the using statement is not already present
        if ($content -match "\b$type\b" -and -not $content.Contains($usingStatement)) {
            # Don't add if it's in the same namespace (avoid circular references)
            $shouldAdd = $true
            
            # Skip if file is in the same namespace as the import
            if ($usingStatement -like "*Fretboard.Primitives*" -and $FilePath -like "*\Fretboard\Primitives\*") {
                $shouldAdd = $false
            }
            elseif ($usingStatement -like "*Notes.Primitives*" -and $FilePath -like "*\Notes\Primitives\*") {
                $shouldAdd = $false
            }
            elseif ($usingStatement -like "*Intervals.Primitives*" -and $FilePath -like "*\Intervals\Primitives\*") {
                $shouldAdd = $false
            }
            elseif ($usingStatement -like "*Atonal.Primitives*" -and $FilePath -like "*\Atonal\Primitives\*") {
                $shouldAdd = $false
            }
            
            if ($shouldAdd) {
                $addedImports += $usingStatement
            }
        }
    }
    
    if ($addedImports.Count -gt 0) {
        # Find the position to insert using statements (after existing using statements)
        $lines = $content -split "`r?`n"
        $insertIndex = 0
        
        # Find the last using statement
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "^using\s+") {
                $insertIndex = $i + 1
            }
        }
        
        # Insert the new using statements
        $newLines = @()
        $newLines += $lines[0..($insertIndex-1)]
        $newLines += $addedImports
        $newLines += $lines[$insertIndex..($lines.Count-1)]
        
        $content = $newLines -join "`r`n"
        $changed = $true
        
        Write-Host "Added imports to: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        foreach ($import in $addedImports) {
            Write-Host "  + $import" -ForegroundColor Gray
        }
    }
    
    if ($changed) {
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
    if (Add-MissingImports -FilePath $csFile.FullName -TypeMappings $typeToNamespace) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Added missing imports to $updatedFiles files" -ForegroundColor Green

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
Write-Host "Primitives import fixes complete!" -ForegroundColor Green
