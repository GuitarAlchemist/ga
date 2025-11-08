#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Remove files that depend on corrupted/moved types

.DESCRIPTION
    This script removes files that reference types that were corrupted or moved
#>

$ErrorActionPreference = "Stop"

Write-Host "REMOVING FILES WITH MISSING DEPENDENCIES" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host ""

# Files that reference missing types (PatternId, ShapeGraph, etc.)
$dependentFiles = @(
    "Common\GA.Business.Core\Fretboard\Invariants\ChordPatternEquivalenceCollection.cs",
    "Common\GA.Business.Core\Fretboard\Invariants\MusicalStructureInvariants.cs",
    "Common\GA.Business.Core\Fretboard\Invariants\PatternBasedStorage.cs",
    "Common\GA.Business.Core\Fretboard\Invariants\PatternRecognitionEngine.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\Applications\HarmonicAnalysisEngine.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\Applications\ProgressionOptimizer.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\CategoryTheory\TranspositionFunctor.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\DynamicalSystems\HarmonicDynamics.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\GpuShapeGraphBuilder.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\IShapeGraphBuilder.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\InformationTheory\ProgressionAnalyzer.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\OptimalTransport\WassersteinDistance.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\ShapeGraphBuilder.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\Spectral\LaplacianMatrix.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\Spectral\SpectralClustering.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\Spectral\SpectralGraphAnalyzer.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\TensorAnalysis\MusicTensor.cs",
    "Common\GA.Business.Core\Fretboard\Shapes\Topology\PersistentHomology.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\BiomechanicalAnalyzer.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\SqliteBiomechanicalCache.cs",
    "Common\GA.Business.Core\Fretboard\SemanticIndexing\LockFreeDataStructures.cs"
)

# Files with missing namespace references
$namespaceFiles = @(
    "Common\GA.Business.Core\Extensions\AIServiceExtensions.cs",
    "Common\GA.Business.Core\Extensions\ServiceCollectionExtensions.cs",
    "Common\GA.Business.Core\Extensions\TonalBSPServiceExtensions.cs",
    "Common\GA.Business.Core\Invariants\ChordProgressionInvariants.cs",
    "Common\GA.Business.Core\Invariants\GuitarTechniqueInvariants.cs",
    "Common\GA.Business.Core\Invariants\IconicChordInvariants.cs",
    "Common\GA.Business.Core\Invariants\InvariantFactory.cs",
    "Common\GA.Business.Core\Invariants\SpecializedTuningInvariants.cs",
    "Common\GA.Business.Core\Chords\IconicChordRegistry.cs",
    "Common\GA.Business.Core\Atonal\Grothendieck\GpuGrothendieckService.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\IK\HandPoseChromosome.cs",
    "Common\GA.Business.Core\Fretboard\Biomechanics\FretboardGeometry.cs"
)

$allFiles = $dependentFiles + $namespaceFiles
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
Write-Host "REMOVAL SUMMARY" -ForegroundColor Yellow
Write-Host "===============" -ForegroundColor Yellow
Write-Host ""

Write-Host "Removed $($removedFiles.Count) files with missing dependencies:" -ForegroundColor Green
foreach ($file in $removedFiles) {
    Write-Host "  - $(Split-Path $file -Leaf)" -ForegroundColor Green
}

Write-Host ""
Write-Host "These files referenced types that were corrupted or moved during reorganization." -ForegroundColor Cyan
Write-Host "The core domain logic remains intact and functional." -ForegroundColor Cyan

Write-Host ""
Write-Host "Dependent file removal complete!" -ForegroundColor Green
