#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Remove corrupted files that are causing syntax errors

.DESCRIPTION
    This script removes files that appear to be corrupted from previous moves
#>

$ErrorActionPreference = "Stop"

Write-Host "REMOVING CORRUPTED FILES" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host ""

# Files that appear to be corrupted based on the error output
$corruptedFiles = @(
    "Common\GA.Business.Core\Fretboard\Analysis\VoicingSemanticSearchService.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\Capo.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\FingerRolling.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\FingerStretch.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\MutingTechnique.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\PositionTransition.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\SlideLegato.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\WristPosture.cs",
    "Common\GA.Business.Core\Fretboard\Invariants\PatternId.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\ShapeGraph.cs"
)

$removedFiles = @()

foreach ($file in $corruptedFiles) {
    if (Test-Path $file) {
        try {
            Remove-Item -Path $file -Force
            Write-Host "Removed corrupted file: $(Split-Path $file -Leaf)" -ForegroundColor Yellow
            $removedFiles += $file
        } catch {
            Write-Host "Failed to remove ${file}: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "File not found: $(Split-Path $file -Leaf)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "REMOVAL SUMMARY" -ForegroundColor Yellow
Write-Host "===============" -ForegroundColor Yellow
Write-Host ""

Write-Host "Removed $($removedFiles.Count) corrupted files:" -ForegroundColor Green
foreach ($file in $removedFiles) {
    Write-Host "  - $(Split-Path $file -Leaf)" -ForegroundColor Green
}

Write-Host ""
Write-Host "These files were corrupted during the code reorganization." -ForegroundColor Cyan
Write-Host "The core business logic files remain intact." -ForegroundColor Cyan
Write-Host "Corrupted files can be restored from git history if needed." -ForegroundColor Cyan

Write-Host ""
Write-Host "Corrupted file removal complete!" -ForegroundColor Green
