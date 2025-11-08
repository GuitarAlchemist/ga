#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix namespaces in BSP files to reference correct GA.Business.Core namespaces

.DESCRIPTION
    This script fixes the using statements in BSP files to properly reference GA.Business.Core namespaces
#>

$ErrorActionPreference = "Stop"

Write-Host "FIXING BSP NAMESPACE REFERENCES" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

$bspPath = "Common\GA.BSP.Core"

if (-not (Test-Path $bspPath)) {
    Write-Host "BSP directory not found: $bspPath" -ForegroundColor Yellow
    exit 0
}

function Fix-BSPNamespaceReferences {
    param(
        [string]$FilePath
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    
    # Fix using statements to reference GA.Business.Core namespaces
    $content = $content -replace "using Atonal\.Primitives;", "using GA.Business.Core.Atonal.Primitives;"
    $content = $content -replace "using Fretboard\.Shapes;", "using GA.Business.Core.Fretboard.Shapes;"
    $content = $content -replace "using Fretboard\.Shapes\.Applications;", "using GA.Business.Core.Fretboard.Shapes.Applications;"
    $content = $content -replace "using Fretboard\.Shapes\.DynamicalSystems;", "using GA.Business.Core.Fretboard.Shapes.DynamicalSystems;"
    $content = $content -replace "using Fretboard\.Shapes\.InformationTheory;", "using GA.Business.Core.Fretboard.Shapes.InformationTheory;"
    $content = $content -replace "using Fretboard\.Shapes\.Spectral;", "using GA.Business.Core.Fretboard.Shapes.Spectral;"
    $content = $content -replace "using Fretboard\.Shapes\.Topology;", "using GA.Business.Core.Fretboard.Shapes.Topology;"
    $content = $content -replace "using Fretboard\.Primitives;", "using GA.Business.Core.Fretboard.Primitives;"
    $content = $content -replace "using Notes\.Primitives;", "using GA.Business.Core.Notes.Primitives;"
    $content = $content -replace "using Intervals\.Primitives;", "using GA.Business.Core.Intervals.Primitives;"
    $content = $content -replace "using Chords\.Primitives;", "using GA.Business.Core.Chords.Primitives;"
    $content = $content -replace "using Scales\.Primitives;", "using GA.Business.Core.Scales.Primitives;"
    $content = $content -replace "using Tonal\.Primitives;", "using GA.Business.Core.Tonal.Primitives;"
    
    # Fix any remaining partial namespace references
    $content = $content -replace "using Spatial;", "using GA.BSP.Core.Spatial;"
    $content = $content -replace "using BSP;", "using GA.BSP.Core.BSP;"
    
    if ($content -ne $originalContent) {
        Write-Host "Fixed namespace references in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        return $true
    }
    
    return $false
}

# Get all C# files in the BSP directory
$csFiles = Get-ChildItem -Path $bspPath -Recurse -Filter "*.cs"

Write-Host "Processing $($csFiles.Count) C# files in BSP directory" -ForegroundColor Cyan
Write-Host ""

$updatedFiles = 0
foreach ($csFile in $csFiles) {
    if (Fix-BSPNamespaceReferences -FilePath $csFile.FullName) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Fixed namespace references in $updatedFiles files" -ForegroundColor Green

Write-Host ""
Write-Host "BSP namespace reference fixes complete!" -ForegroundColor Green
Write-Host "Note: GA.BSP.Core will build successfully once GA.Business.Core syntax issues are resolved." -ForegroundColor Yellow
