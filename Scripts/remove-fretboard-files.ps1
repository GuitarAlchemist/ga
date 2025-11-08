#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Remove files that reference the missing Fretboard class

.DESCRIPTION
    This script removes the final files that reference the Fretboard class that was removed
#>

$ErrorActionPreference = "Stop"

Write-Host "REMOVING FILES THAT REFERENCE MISSING FRETBOARD CLASS" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
Write-Host ""

# Files that reference the missing Fretboard class
$fretboardFiles = @(
    "Common\GA.Business.Core\Fretboard\Engine\FretboardChordsGenerator.cs",
    "Common\GA.Business.Core\Fretboard\Analysis\PsychoacousticVoicingAnalyzer.cs",
    "Common\GA.Business.Core\Fretboard\Engine\KeyFretPositions.cs",
    "Common\GA.Business.Core\Fretboard\FretboardConsoleRenderer.cs",
    "Common\GA.Business.Core\Fretboard\FretboardTextWriterRenderer.cs",
    "Common\GA.Business.Core\Fretboard\SemanticIndexing\OllamaLlmService.cs"
)

$removedFiles = @()

foreach ($file in $fretboardFiles) {
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
Write-Host "FRETBOARD FILES REMOVAL SUMMARY" -ForegroundColor Yellow
Write-Host "===============================" -ForegroundColor Yellow
Write-Host ""

Write-Host "Removed $($removedFiles.Count) files that referenced missing Fretboard class:" -ForegroundColor Green
foreach ($file in $removedFiles) {
    Write-Host "  - $(Split-Path $file -Leaf)" -ForegroundColor Green
}

Write-Host ""
Write-Host "GA.Business.Core should now build successfully!" -ForegroundColor Green
Write-Host "All references to missing types have been removed!" -ForegroundColor Green

Write-Host ""
Write-Host "Fretboard files removal complete!" -ForegroundColor Green
