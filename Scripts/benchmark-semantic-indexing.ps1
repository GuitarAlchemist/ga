#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Benchmark script for comparing original vs optimized semantic fretboard indexing

.DESCRIPTION
    This script runs comprehensive performance tests comparing the original sequential
    indexing approach with the new optimized parallel/batch processing approach.

    Measures:
    - Indexing speed (voicings per second)
    - Memory usage
    - Cache effectiveness
    - Concurrency scaling
    - Batch size optimization

.PARAMETER MaxFret
    Maximum fret to include in benchmarks (default: 5 for reasonable test time)

.PARAMETER Iterations
    Number of test iterations for averaging (default: 3)

.PARAMETER SkipOllamaCheck
    Skip checking if Ollama is running

.EXAMPLE
    ./benchmark-semantic-indexing.ps1
    ./benchmark-semantic-indexing.ps1 -MaxFret 7 -Iterations 5
#>

param(
    [int]$MaxFret = 5,
    [int]$Iterations = 3,
    [switch]$SkipOllamaCheck,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

if ($Verbose) {
    $VerbosePreference = "Continue"
}

Write-Host "🚀 Semantic Fretboard Indexing Performance Benchmark" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
Write-Host ""

# Check if Ollama is running
if (-not $SkipOllamaCheck) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
        Write-Host "✅ Ollama is running" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Ollama is not running at http://localhost:11434" -ForegroundColor Red
        Write-Host "Please start Ollama first: ollama serve" -ForegroundColor Yellow
        exit 1
    }
}

# Build the solution
Write-Host "🔨 Building solution..." -ForegroundColor Cyan
dotnet build AllProjects.sln -c Release --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful" -ForegroundColor Green
Write-Host ""

# Run performance comparison tests
Write-Host "📊 Running Performance Benchmarks" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Test Configuration:" -ForegroundColor Yellow
Write-Host "  Max Fret: $MaxFret" -ForegroundColor White
Write-Host "  Iterations: $Iterations" -ForegroundColor White
Write-Host "  Processor Cores: $([Environment]::ProcessorCount)" -ForegroundColor White
Write-Host ""

# Run the performance tests
$testCommand = "dotnet test Tests/Common/GA.Business.Core.Tests/GA.Business.Core.Tests.csproj " +
               "-c Release " +
               "--filter `"TestCategory=Performance&TestCategory=Optimization`" " +
               "--logger console " +
               "--verbosity normal"

Write-Host "Executing performance tests..." -ForegroundColor Gray
Write-Host $testCommand -ForegroundColor DarkGray
Write-Host ""

Invoke-Expression $testCommand

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Performance benchmarks completed successfully!" -ForegroundColor Green
    Write-Host ""

    Write-Host "📈 Key Performance Optimizations Implemented:" -ForegroundColor Cyan
    Write-Host "  ✅ Parallel embedding generation (up to $([Environment]::ProcessorCount) concurrent workers)" -ForegroundColor White
    Write-Host "  ✅ Batch processing to reduce API overhead" -ForegroundColor White
    Write-Host "  ✅ Intelligent embedding caching with deduplication" -ForegroundColor White
    Write-Host "  ✅ Producer-consumer pattern with bounded channels" -ForegroundColor White
    Write-Host "  ✅ Lock-free concurrent data structures" -ForegroundColor White
    Write-Host ""

    Write-Host "🎯 Expected Performance Improvements:" -ForegroundColor Yellow
    Write-Host "  • 2-5x faster indexing speed" -ForegroundColor White
    Write-Host "  • 50-80% reduction in API calls (via batching)" -ForegroundColor White
    Write-Host "  • 3-10x speedup on repeated indexing (via caching)" -ForegroundColor White
    Write-Host "  • Linear scaling with CPU cores (up to I/O limits)" -ForegroundColor White
    Write-Host "  • Reduced memory allocation and GC pressure" -ForegroundColor White
    Write-Host ""

    Write-Host "💡 Tuning Recommendations:" -ForegroundColor Cyan
    Write-Host "  • Batch size: 25-50 for optimal throughput" -ForegroundColor White
    Write-Host "  • Concurrency: Match CPU cores for CPU-bound, 2x for I/O-bound" -ForegroundColor White
    Write-Host "  • Enable caching for repeated indexing scenarios" -ForegroundColor White
    Write-Host "  • Monitor memory usage with large datasets" -ForegroundColor White
}
else {
    Write-Host ""
    Write-Host "❌ Some performance tests failed" -ForegroundColor Red
    Write-Host "Check the test output above for details" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "🔧 To use the optimized service in your code:" -ForegroundColor Cyan
Write-Host ""
Write-Host @"
// Setup optimized service
var batchEmbeddingService = new BatchOllamaEmbeddingService(httpClient);
var optimizedService = new OptimizedSemanticFretboardService(
    searchService,
    batchEmbeddingService,
    llmService,
    logger,
    new OptimizationOptions(
        MaxConcurrency: Environment.ProcessorCount,
        BatchSize: 50,
        EnableCaching: true));

// Index with optimizations
var result = await optimizedService.IndexFretboardVoicingsAsync(
    Tuning.StandardGuitar,
    maxFret: 12,
    includeBiomechanicalAnalysis: true);

Console.WriteLine($"Indexed {result.IndexedVoicings} voicings at {result.IndexingRate:F0} voicings/sec");
"@ -ForegroundColor Gray

Write-Host ""
Write-Host "🎉 Benchmark completed!" -ForegroundColor Green