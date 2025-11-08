#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix syntax errors caused by incorrect using statement placement

.DESCRIPTION
    This script fixes files where using statements were inserted in the wrong place
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING SYNTAX ERRORS" -ForegroundColor Green
Write-Host "====================" -ForegroundColor Green
Write-Host ""

# List of files that have syntax errors based on the build output
$problematicFiles = @(
    "Common/GA.Business.Core/Notes/Extensions/NaturalNoteExtensions.cs",
    "Common/GA.Business.Core/Fretboard/FretboardTextWriterRenderer.cs",
    "Common/GA.Business.Core/Fretboard/Fingering/FingerCount.cs",
    "Common/GA.Business.Core/Fretboard/Positions/MutedPositionCollection.cs",
    "Common/GA.Business.Core/Fretboard/FretboardConsoleRenderer.cs",
    "Common/GA.Business.Core/Fretboard/Positions/PlayedPositionCollection.cs",
    "Common/GA.Business.Core/Fretboard/Positions/PositionCollection.cs",
    "Common/GA.Business.Core/Fretboard/Positions/PositionLocation.cs",
    "Common/GA.Business.Core/Fretboard/Positions/RelativePositionLocation.cs"
)

function Fix-SyntaxErrors {
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
    
    # Extract all using statements
    $usingStatements = @()
    $lines = $content -split "`r?`n"
    
    foreach ($line in $lines) {
        if ($line -match "^\s*using\s+[^;]+;") {
            $cleanUsing = $line.Trim()
            if ($usingStatements -notcontains $cleanUsing) {
                $usingStatements += $cleanUsing
            }
        }
    }
    
    # Remove all using statements and namespace duplicates from content
    $cleanedLines = @()
    $namespaceFound = $false
    $namespaceDeclaration = ""
    
    foreach ($line in $lines) {
        # Skip using statements
        if ($line -match "^\s*using\s+[^;]+;") {
            continue
        }
        
        # Capture the first namespace declaration
        if ($line -match "^\s*namespace\s+([^;{]+)") {
            if (-not $namespaceFound) {
                $namespaceDeclaration = $line.Trim()
                $namespaceFound = $true
                continue
            } else {
                # Skip duplicate namespace declarations
                continue
            }
        }
        
        # Skip empty lines at the beginning
        if (-not $namespaceFound -and $line.Trim() -eq "") {
            continue
        }
        
        # Skip orphaned closing braces
        if ($line.Trim() -eq "}" -and -not $namespaceFound) {
            continue
        }
        
        $cleanedLines += $line
    }
    
    # Reconstruct the file
    $newContent = @()
    
    # Add BOM if original had it
    if ($content.StartsWith([char]0xFEFF)) {
        $newContent += [char]0xFEFF
    }
    
    # Add using statements
    foreach ($using in $usingStatements) {
        $newContent += $using
    }
    
    # Add empty line
    if ($usingStatements.Count -gt 0) {
        $newContent += ""
    }
    
    # Add namespace declaration
    if ($namespaceDeclaration) {
        $newContent += $namespaceDeclaration
        $newContent += "{"
    }
    
    # Add the rest of the content
    $newContent += $cleanedLines
    
    # Ensure proper closing
    if ($namespaceDeclaration -and $newContent[-1] -ne "}") {
        $newContent += "}"
    }
    
    $finalContent = $newContent -join "`r`n"
    
    if ($finalContent -ne $originalContent) {
        Write-Host "Fixed syntax errors in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $finalContent -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

$fixedFiles = 0
foreach ($file in $problematicFiles) {
    if (Fix-SyntaxErrors -FilePath $file) {
        $fixedFiles++
    }
}

Write-Host ""
Write-Host "Fixed syntax errors in $fixedFiles files" -ForegroundColor Green

if (-not $DryRun) {
    Write-Host ""
    Write-Host "Testing build..." -ForegroundColor Cyan
    
    # Test build the GA.Business.Core project
    dotnet build "Common/GA.Business.Core/GA.Business.Core.csproj" --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GA.Business.Core builds successfully!" -ForegroundColor Green
    } else {
        Write-Host "GA.Business.Core still has build issues - may need additional fixes" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Syntax error fixes complete!" -ForegroundColor Green
