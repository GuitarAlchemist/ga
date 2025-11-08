#!/usr/bin/env pwsh
# Script to fix common test compilation errors

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "Fixing test compilation errors..." -ForegroundColor Cyan

# Only remove AddConsole() calls - IntervalClassVector fixes need manual review
# because GrothendieckDelta also has Ic1-Ic6 properties

# Pattern: Remove AddConsole() calls
$addConsolePattern = '\.AddConsole\(\)'

# Get all test files
$testFiles = Get-ChildItem -Path "Tests/Common/GA.Business.Core.Tests" -Filter "*.cs" -Recurse

$filesModified = 0
$totalReplacements = 0

foreach ($file in $testFiles)
{
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    $fileReplacements = 0

    # Remove AddConsole() calls
    if ($content -match $addConsolePattern)
    {
        $count = ([regex]::Matches($content, $addConsolePattern)).Count
        $content = $content -replace $addConsolePattern, ''
        $fileReplacements += $count
        Write-Host "  Removed $count occurrences of '.AddConsole()' in $( $file.Name )" -ForegroundColor Yellow
    }

    # Save if modified
    if ($content -ne $originalContent)
    {
        if (-not $DryRun)
        {
            Set-Content -Path $file.FullName -Value $content -NoNewline
        }
        $filesModified++
        $totalReplacements += $fileReplacements
        Write-Host "Modified $( $file.Name ) ($fileReplacements replacements)" -ForegroundColor Green
    }
}

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Files modified: $filesModified" -ForegroundColor Green
Write-Host "  Total replacements: $totalReplacements" -ForegroundColor Green

if ($DryRun)
{
    Write-Host "`n(Dry run - no files were actually modified)" -ForegroundColor Yellow
}

Write-Host "`nNote: IntervalClassVector indexer fixes need manual review" -ForegroundColor Yellow
Write-Host "  - GrothendieckDelta has Ic1-Ic6 properties (keep as is)" -ForegroundColor Yellow
Write-Host "  - IntervalClassVector uses indexer (needs fixing)" -ForegroundColor Yellow

