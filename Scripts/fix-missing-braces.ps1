#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix missing closing braces in GA.Business.Core files

.DESCRIPTION
    This script adds missing closing braces to files that need them
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING MISSING CLOSING BRACES" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green
Write-Host ""

# List of files that need closing braces based on the build errors
$filesNeedingBraces = @(
    "Common/GA.Business.Core/Fretboard/Fingering/FingerCount.cs",
    "Common/GA.Business.Core/Fretboard/FretboardConsoleRenderer.cs",
    "Common/GA.Business.Core/Fretboard/FretboardTextWriterRenderer.cs",
    "Common/GA.Business.Core/Fretboard/Positions/MutedPositionCollection.cs",
    "Common/GA.Business.Core/Fretboard/Positions/PlayedPositionCollection.cs",
    "Common/GA.Business.Core/Fretboard/Positions/PositionLocation.cs",
    "Common/GA.Business.Core/Fretboard/Positions/RelativePositionLocation.cs"
)

function Fix-MissingBraces {
    param(
        [string]$FilePath
    )
    
    if (-not (Test-Path $FilePath)) {
        Write-Host "File not found: $FilePath" -ForegroundColor Yellow
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { 
        Write-Host "Could not read file: $FilePath" -ForegroundColor Yellow
        return $false 
    }
    
    $originalContent = $content
    
    # Count opening and closing braces
    $openBraces = ($content.ToCharArray() | Where-Object { $_ -eq '{' }).Count
    $closeBraces = ($content.ToCharArray() | Where-Object { $_ -eq '}' }).Count
    
    if ($openBraces -gt $closeBraces) {
        $missingBraces = $openBraces - $closeBraces
        Write-Host "Adding $missingBraces missing closing brace(s) to: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        # Add the missing closing braces at the end
        $content = $content.TrimEnd()
        for ($i = 0; $i -lt $missingBraces; $i++) {
            $content += "`r`n}"
        }
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

$fixedFiles = 0
foreach ($file in $filesNeedingBraces) {
    if (Fix-MissingBraces -FilePath $file) {
        $fixedFiles++
    }
}

Write-Host ""
Write-Host "Fixed missing braces in $fixedFiles files" -ForegroundColor Green

if (-not $DryRun) {
    Write-Host ""
    Write-Host "Testing build..." -ForegroundColor Cyan
    
    # Test build the GA.Business.Core project
    dotnet build "Common/GA.Business.Core/GA.Business.Core.csproj" --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GA.Business.Core builds successfully!" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "Testing full solution build..." -ForegroundColor Cyan
        dotnet build AllProjects.sln --verbosity minimal --no-restore
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "ENTIRE SOLUTION BUILDS SUCCESSFULLY!" -ForegroundColor Green
        } else {
            Write-Host "Solution has some remaining issues" -ForegroundColor Yellow
        }
    } else {
        Write-Host "GA.Business.Core still has build issues" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Missing brace fixes complete!" -ForegroundColor Green
