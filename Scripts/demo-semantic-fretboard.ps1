#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Demo script for semantic fretboard indexing and natural language querying

.DESCRIPTION
    This script demonstrates the complete end-to-end functionality of the semantic fretboard system:
    1. Indexes guitar voicings with real embeddings
    2. Processes natural language queries with real LLM responses
    3. Shows practical examples of how musicians would use the system

.EXAMPLE
    ./demo-semantic-fretboard.ps1
#>

param(
    [string]$OllamaUrl = "http://localhost:11434",
    [int]$MaxFret = 7,
    [switch]$SkipIndexing
)

$ErrorActionPreference = "Stop"

Write-Host "🎸 Guitar Alchemist Semantic Fretboard Demo" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

# Check if Ollama is running
function Test-OllamaRunning {
    try {
        Invoke-RestMethod -Uri "$OllamaUrl/api/tags" -Method Get -TimeoutSec 5 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

if (-not (Test-OllamaRunning)) {
    Write-Host "❌ Ollama is not running at $OllamaUrl" -ForegroundColor Red
    Write-Host "Please start Ollama first: ollama serve" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Ollama is running" -ForegroundColor Green

# Build the CLI
Write-Host "🔨 Building CLI..." -ForegroundColor Cyan
dotnet build GaCLI/GaCLI.csproj -c Debug --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed" -ForegroundColor Red
    exit 1
}

$cliPath = "GaCLI/bin/Debug/net9.0/GaCLI.exe"
if (-not (Test-Path $cliPath)) {
    $cliPath = "GaCLI/bin/Debug/net9.0/GaCLI"
}

# Demo queries that showcase different use cases
$demoQueries = @(
    @{
        Query = "What are some easy open chords for a complete beginner?"
        Description = "Beginner-friendly query"
    },
    @{
        Query = "I need a bright sounding C major chord for fingerpicking"
        Description = "Specific tonal quality request"
    },
    @{
        Query = "Show me jazz voicings for Dm7 that don't require a barre"
        Description = "Genre-specific with physical constraints"
    },
    @{
        Query = "Find me some power chords for rock music in drop D tuning"
        Description = "Genre and tuning specific"
    },
    @{
        Query = "I want warm, mellow chords for folk music that use open strings"
        Description = "Mood and technique specific"
    },
    @{
        Query = "What are some sus chords that create tension for ambient music?"
        Description = "Advanced harmonic concept"
    }
)

Write-Host "🎯 Demo Overview:" -ForegroundColor Cyan
Write-Host "This demo will:" -ForegroundColor White
Write-Host "  1. Index guitar voicings (if not skipped)" -ForegroundColor White
Write-Host "  2. Process realistic musician queries" -ForegroundColor White
Write-Host "  3. Show LLM-powered responses with chord recommendations" -ForegroundColor White
Write-Host ""

# Index voicings if not skipped
if (-not $SkipIndexing) {
    Write-Host "📚 Indexing guitar voicings..." -ForegroundColor Cyan
    Write-Host "This will take a few minutes as we generate embeddings for all chord voicings..." -ForegroundColor Yellow

    & $cliPath semantic-fretboard --index --tuning standard --max-fret $MaxFret

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Indexing failed" -ForegroundColor Red
        exit 1
    }

    Write-Host "✅ Indexing completed!" -ForegroundColor Green
    Write-Host ""
}

# Process demo queries
Write-Host "🤖 Processing Natural Language Queries" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

foreach ($demo in $demoQueries) {
    Write-Host "📝 Scenario: $($demo.Description)" -ForegroundColor Yellow
    Write-Host "❓ Query: '$($demo.Query)'" -ForegroundColor White
    Write-Host ""

    # Process the query
    & $cliPath semantic-fretboard --query $demo.Query

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Query failed" -ForegroundColor Red
        continue
    }

    Write-Host ""
    Write-Host "Press any key to continue to the next demo..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    Write-Host ""
    Write-Host "---" -ForegroundColor DarkGray
    Write-Host ""
}

# Interactive mode option
Write-Host "🎮 Interactive Mode Available" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can now try your own queries in interactive mode!" -ForegroundColor White
Write-Host "Run: $cliPath semantic-fretboard --interactive" -ForegroundColor Yellow
Write-Host ""

$response = Read-Host "Would you like to start interactive mode now? (y/N)"
if ($response -eq "y" -or $response -eq "Y") {
    Write-Host ""
    Write-Host "🚀 Starting interactive mode..." -ForegroundColor Green
    & $cliPath semantic-fretboard --interactive
}

Write-Host ""
Write-Host "🎉 Demo completed!" -ForegroundColor Green
Write-Host ""
Write-Host "What you just saw:" -ForegroundColor Cyan
Write-Host "  ✅ Real-time indexing of guitar voicings with vector embeddings" -ForegroundColor White
Write-Host "  ✅ Natural language processing with Llama 3.2" -ForegroundColor White
Write-Host "  ✅ Semantic search across musical characteristics" -ForegroundColor White
Write-Host "  ✅ Contextual responses with practical playing advice" -ForegroundColor White
Write-Host ""
Write-Host "Try more queries like:" -ForegroundColor Yellow
Write-Host "  • 'Find me chord progressions for sad songs'" -ForegroundColor Gray
Write-Host "  • 'What are some fingerpicking patterns for beginners?'" -ForegroundColor Gray
Write-Host "  • 'Show me metal chord voicings with low tunings'" -ForegroundColor Gray
Write-Host "  • 'I need jazz chords that work well with a small hand span'" -ForegroundColor Gray