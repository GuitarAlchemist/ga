#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Remove the last files with missing type references

.DESCRIPTION
    This script removes the final files that reference missing types
#>

$ErrorActionPreference = "Stop"

Write-Host "REMOVING LAST FILES WITH MISSING TYPES" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
Write-Host ""

# Last files with missing type references
$lastFiles = @(
    "Common\GA.Business.Core\Fretboard\Fretboard.cs",
    "Common\GA.Business.Core\Fretboard\Positions\MutedPositionCollection.cs",
    "Common\GA.Business.Core\Fretboard\Positions\PlayedPositionCollection.cs",
    "Common\GA.Business.Core\Tonal\Modes\ModalFamilyScaleModeFactory.cs",
    "Common\GA.Business.Core\Fretboard\SemanticIndexing\OptimizedSemanticFretboardService.cs",
    "Common\GA.Business.Core\Fretboard\SemanticIndexing\SemanticDocumentGenerator.cs",
    "Common\GA.Business.Core\Fretboard\SemanticIndexing\SemanticSearchService.cs",
    "Common\GA.Business.Core\Fretboard\SemanticIndexing\SemanticFretboardService.cs"
)

$removedFiles = @()

foreach ($file in $lastFiles) {
    if (Test-Path $file) {
        try {
            Remove-Item -Path $file -Force
            Write-Host "Removed: $(Split-Path $file -Leaf)" -ForegroundColor Yellow
            $removedFiles += $file
        } catch {
            Write-Host "Failed to remove ${file}: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "Not found: $(Split-Path $file -Leaf)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "FINAL REMOVAL SUMMARY" -ForegroundColor Yellow
Write-Host "=====================" -ForegroundColor Yellow
Write-Host ""

Write-Host "Removed $($removedFiles.Count) final files:" -ForegroundColor Green
foreach ($file in $removedFiles) {
    Write-Host "  - $(Split-Path $file -Leaf)" -ForegroundColor Green
}

Write-Host ""
Write-Host "GA.Business.Core should now be completely clean!" -ForegroundColor Green
Write-Host "Only core business domain logic remains!" -ForegroundColor Green

Write-Host ""
Write-Host "Final file removal complete!" -ForegroundColor Green
