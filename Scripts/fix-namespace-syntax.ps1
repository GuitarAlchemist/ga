#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix namespace syntax issues in GA.Business.Core files

.DESCRIPTION
    This script fixes namespace declarations that have incorrect syntax
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING NAMESPACE SYNTAX ISSUES" -ForegroundColor Green
Write-Host "===============================" -ForegroundColor Green
Write-Host ""

# Get all C# files in GA.Business.Core that might have namespace issues
$coreProjectPath = "Common/GA.Business.Core"
$csFiles = Get-ChildItem -Path $coreProjectPath -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

function Fix-NamespaceSyntax {
    param(
        [string]$FilePath
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    $changed = $false
    
    # Fix namespace declarations with semicolons
    $content = $content -replace "namespace\s+([^;{]+);\s*\{", "namespace `$1`r`n{"
    
    # Fix double BOM
    $content = $content -replace "^\uFEFF\uFEFF", [char]0xFEFF
    
    # Fix orphaned closing braces after namespace
    $lines = $content -split "`r?`n"
    $newLines = @()
    $skipNext = $false
    
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        
        if ($skipNext) {
            $skipNext = $false
            continue
        }
        
        # If we find a namespace declaration followed by an orphaned brace block
        if ($line -match "^namespace\s+[^{]+$" -and $i + 1 -lt $lines.Count) {
            $nextLine = $lines[$i + 1]
            if ($nextLine.Trim() -eq "{" -and $i + 2 -lt $lines.Count) {
                $afterNext = $lines[$i + 2]
                if ($afterNext.Trim() -eq "}") {
                    # Skip the orphaned brace block
                    $newLines += $line
                    $newLines += "{"
                    $skipNext = $true # Skip the opening brace
                    $i++ # Skip the closing brace too
                    $changed = $true
                    continue
                }
            }
        }
        
        $newLines += $line
    }
    
    if ($changed) {
        $content = $newLines -join "`r`n"
    }
    
    # Ensure proper namespace format
    $content = $content -replace "namespace\s+GA\.Business\.Fretboard([^;{]*)", "namespace GA.Business.Core.Fretboard`$1"
    $content = $content -replace "namespace\s+GA\.Business\.Notes([^;{]*)", "namespace GA.Business.Core.Notes`$1"
    $content = $content -replace "namespace\s+GA\.Business\.Intervals([^;{]*)", "namespace GA.Business.Core.Intervals`$1"
    $content = $content -replace "namespace\s+GA\.Business\.Atonal([^;{]*)", "namespace GA.Business.Core.Atonal`$1"
    $content = $content -replace "namespace\s+GA\.Business\.Chords([^;{]*)", "namespace GA.Business.Core.Chords`$1"
    $content = $content -replace "namespace\s+GA\.Business\.Scales([^;{]*)", "namespace GA.Business.Core.Scales`$1"
    $content = $content -replace "namespace\s+GA\.Business\.Tonal([^;{]*)", "namespace GA.Business.Core.Tonal`$1"
    
    if ($content -ne $originalContent) {
        Write-Host "Fixed namespace syntax in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

$fixedFiles = 0
foreach ($csFile in $csFiles) {
    if (Fix-NamespaceSyntax -FilePath $csFile.FullName) {
        $fixedFiles++
    }
}

Write-Host ""
Write-Host "Fixed namespace syntax in $fixedFiles files" -ForegroundColor Green

if (-not $DryRun) {
    Write-Host ""
    Write-Host "Testing build..." -ForegroundColor Cyan
    
    # Test build the GA.Business.Core project
    dotnet build $coreProjectPath/GA.Business.Core.csproj --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GA.Business.Core builds successfully!" -ForegroundColor Green
    } else {
        Write-Host "GA.Business.Core still has build issues - may need additional fixes" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Namespace syntax fixes complete!" -ForegroundColor Green
