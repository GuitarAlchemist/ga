#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix specific broken files from the namespace script

.DESCRIPTION
    This script fixes files that were corrupted by the namespace fixing script
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING BROKEN FILES" -ForegroundColor Green
Write-Host "===================" -ForegroundColor Green
Write-Host ""

# List of files that need manual fixes based on the error output
$brokenFiles = @(
    "Common/GA.Business.Core/AI/SoundBank/SoundBankModels.cs",
    "Common/GA.Business.Core/Atonal/Grothendieck/IGrothendieckService.cs",
    "Common/GA.Business.Core/Fretboard/Invariants/PatternId.cs",
    "Common/GA.Business.Core/Fretboard/Biomechanics/MutingTechnique.cs",
    "Common/GA.Business.Core/Fretboard/Positions/PositionLocationSet.cs",
    "Common/GA.Business.Core/Fretboard/Biomechanics/Capo.cs",
    "Common/GA.Business.Core/Fretboard/Biomechanics/FingerStretch.cs",
    "Common/GA.Business.Core/Fretboard/Analysis/VoicingSemanticSearchService.cs",
    "Common/GA.Business.Core/Fretboard/Biomechanics/FingerRolling.cs",
    "Common/GA.Business.Core/Fretboard/Shapes/ShapeGraph.cs",
    "Common/GA.Business.Core/Fretboard/Biomechanics/PositionTransition.cs",
    "Common/GA.Business.Core/Tonal/Primitives/GenericScaleDegree.cs",
    "Common/GA.Business.Core/Fretboard/Biomechanics/SlideLegato.cs",
    "Common/GA.Business.Core/Fretboard/Biomechanics/WristPosture.cs",
    "Common/GA.Business.Core/Atonal/Primitives/Cardinality.cs"
)

function Fix-BrokenFile {
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
    $lines = $content -split "`r?`n"
    
    # Fix common issues
    $newLines = @()
    $usingStatements = @()
    $namespaceFound = $false
    $namespaceDeclaration = ""
    $inNamespace = $false
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        # Skip double BOM
        if ($line -match "^\uFEFF\uFEFF") {
            $line = $line -replace "^\uFEFF\uFEFF", [char]0xFEFF
        }
        
        # Collect using statements
        if ($line -match "^\s*using\s+[^;]+;") {
            $cleanUsing = $line.Trim()
            if ($usingStatements -notcontains $cleanUsing) {
                $usingStatements += $cleanUsing
            }
            continue
        }
        
        # Find namespace declaration
        if ($line -match "^\s*namespace\s+([^;{]+)") {
            if (-not $namespaceFound) {
                $namespaceDeclaration = $line.Trim()
                $namespaceFound = $true
            }
            continue
        }
        
        # Skip standalone opening/closing braces right after namespace
        if ($namespaceFound -and -not $inNamespace -and $line.Trim() -eq "{") {
            $inNamespace = $true
            continue
        }
        
        # Skip empty lines at the beginning
        if (-not $inNamespace -and $line.Trim() -eq "") {
            continue
        }
        
        # Start collecting content after namespace
        if ($namespaceFound) {
            $inNamespace = $true
            
            # Fix enum declarations missing opening brace
            if ($line -match "^\s*public\s+enum\s+\w+\s*$") {
                $newLines += $line
                $newLines += "{"
                continue
            }
            
            # Fix interface declarations missing opening brace
            if ($line -match "^\s*public\s+interface\s+\w+\s*$") {
                $newLines += $line
                $newLines += "{"
                continue
            }
            
            # Fix class/record declarations missing opening brace
            if ($line -match "^\s*public\s+(class|record|struct)\s+\w+.*\s*$" -and -not $line.Contains("{")) {
                $newLines += $line
                $newLines += "{"
                continue
            }
            
            $newLines += $line
        }
    }
    
    # Reconstruct the file
    $finalContent = @()
    
    # Add BOM if original had it
    if ($content.StartsWith([char]0xFEFF)) {
        $finalContent += [char]0xFEFF
    }
    
    # Add using statements
    foreach ($using in $usingStatements) {
        $finalContent += $using
    }
    
    # Add empty line
    if ($usingStatements.Count -gt 0) {
        $finalContent += ""
    }
    
    # Add namespace declaration
    if ($namespaceDeclaration) {
        $finalContent += $namespaceDeclaration
        $finalContent += "{"
    }
    
    # Add the content
    $finalContent += $newLines
    
    # Ensure proper closing
    if ($namespaceDeclaration) {
        $finalContent += "}"
    }
    
    $newContent = $finalContent -join "`r`n"
    
    if ($newContent -ne $originalContent) {
        Write-Host "Fixed broken syntax in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $newContent -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

$fixedFiles = 0
foreach ($file in $brokenFiles) {
    if (Fix-BrokenFile -FilePath $file) {
        $fixedFiles++
    }
}

Write-Host ""
Write-Host "Fixed broken syntax in $fixedFiles files" -ForegroundColor Green

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
Write-Host "Broken file fixes complete!" -ForegroundColor Green
