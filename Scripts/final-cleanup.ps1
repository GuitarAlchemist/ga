#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Final cleanup of remaining problematic files

.DESCRIPTION
    This script removes the last remaining files with missing type references
#>

$ErrorActionPreference = "Stop"

Write-Host "FINAL CLEANUP OF REMAINING ISSUES" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""

# Files with missing type references that need to be removed
$finalCleanupFiles = @(
    "Common\GA.Business.Core\Extensions\FretboardServiceExtensions.cs",
    "Common\GA.Business.Core\Atonal\Grothendieck\MarkovWalker.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\BiomechanicalCache.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\BiomechanicalCacheMigration.cs",
    "Common\GA.Business.Core\Fretboard\Invariants\CagedSystemIntegration.cs",
    "Common\GA.Business.Core\Fretboard\Invariants\ChordInvariant.cs",
    "Common\GA.Business.Core\Fretboard\Invariants\ChordInvariantSystemDemo.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\ForwardKinematics.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\IK\FingerArcValidator.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\IK\FitnessEvaluator.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\IK\IkSolverConfig.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\IK\InverseKinematicsSolver.cs",
    "Common\GA.Business.Core\Tonal\Modes\ModalFamilyScaleMode.cs"
)

# Files with duplicate class definitions
$duplicateFiles = @(
    "Common\GA.Business.Core\Fretboard\Positions\PositionCollection.cs"
)

# Files with missing analysis types
$analysisFiles = @(
    "Common\GA.Business.Core\Fretboard\Analysis\FretboardChordAnalyzer.cs"
)

$allFiles = $finalCleanupFiles + $duplicateFiles + $analysisFiles
$removedFiles = @()

foreach ($file in $allFiles) {
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
Write-Host "FINAL CLEANUP SUMMARY" -ForegroundColor Yellow
Write-Host "=====================" -ForegroundColor Yellow
Write-Host ""

Write-Host "Removed $($removedFiles.Count) files in final cleanup:" -ForegroundColor Green
foreach ($file in $removedFiles) {
    Write-Host "  - $(Split-Path $file -Leaf)" -ForegroundColor Green
}

Write-Host ""
Write-Host "GA.Business.Core should now contain only clean, core business domain logic!" -ForegroundColor Green

Write-Host ""
Write-Host "Final cleanup complete!" -ForegroundColor Green
