#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Comprehensive fix for all syntax issues in GA.Business.Core

.DESCRIPTION
    This script systematically fixes all the syntax issues that are preventing compilation
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "COMPREHENSIVE SYNTAX FIXES" -ForegroundColor Green
Write-Host "==========================" -ForegroundColor Green
Write-Host ""

function Fix-FileSyntax {
    param(
        [string]$FilePath
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    $lines = $content -split "`r?`n"
    $newLines = @()
    $changed = $false
    
    # Remove double BOM
    if ($content.StartsWith([char]0xFEFF + [char]0xFEFF)) {
        $content = [char]0xFEFF + $content.Substring(2)
        $changed = $true
    }
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $originalLine = $line
        
        # Fix class/record/enum/interface declarations missing opening braces
        if ($line -match "^\s*public\s+(class|record\s+struct|record|enum|interface)\s+\w+.*[^{]\s*$") {
            $newLines += $line
            $newLines += "{"
            $changed = $true
            continue
        }
        
        # Fix record declarations with missing opening brace before regions
        if ($line -match "^\s*public\s+readonly\s+record\s+struct\s+\w+.*:\s*\w+.*$" -and 
            $i + 1 -lt $lines.Count -and 
            $lines[$i + 1] -match "^\s*#region") {
            $newLines += $line
            $newLines += "{"
            $changed = $true
            continue
        }
        
        # Fix malformed record parameter lists (missing closing parenthesis)
        if ($line -match "^(.+record\s+\w+\([^)]*),\s*$") {
            # Look ahead to find the rest of the parameters
            $j = $i + 1
            $parameterLines = @($line)
            while ($j -lt $lines.Count -and $lines[$j] -notmatch "^\s*\)") {
                $parameterLines += $lines[$j]
                $j++
            }
            if ($j -lt $lines.Count -and $lines[$j] -match "^\s*\)") {
                $parameterLines += $lines[$j]
            }
            
            # Reconstruct the record declaration
            $recordDeclaration = ($parameterLines -join " ").Trim()
            $newLines += $recordDeclaration
            $i = $j # Skip the processed lines
            $changed = $true
            continue
        }
        
        $newLines += $line
    }
    
    if ($changed) {
        $newContent = $newLines -join "`r`n"
        
        Write-Host "Fixed syntax issues in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $newContent -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

# Get all C# files in GA.Business.Core
$coreProjectPath = "Common/GA.Business.Core"
$csFiles = Get-ChildItem -Path $coreProjectPath -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Processing $($csFiles.Count) C# files in GA.Business.Core" -ForegroundColor Cyan
Write-Host ""

$fixedFiles = 0
foreach ($csFile in $csFiles) {
    if (Fix-FileSyntax -FilePath $csFile.FullName) {
        $fixedFiles++
    }
}

Write-Host ""
Write-Host "Fixed syntax issues in $fixedFiles files" -ForegroundColor Green

if (-not $DryRun) {
    Write-Host ""
    Write-Host "Testing build..." -ForegroundColor Cyan
    
    # Test build the GA.Business.Core project
    dotnet build $coreProjectPath/GA.Business.Core.csproj --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GA.Business.Core builds successfully!" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "Testing full solution build..." -ForegroundColor Cyan
        dotnet build AllProjects.sln --verbosity minimal --no-restore
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "ENTIRE SOLUTION BUILDS SUCCESSFULLY!" -ForegroundColor Green
        }
    } else {
        Write-Host "GA.Business.Core still has build issues" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Comprehensive syntax fixes complete!" -ForegroundColor Green
