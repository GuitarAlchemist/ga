#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix file-scoped namespace issues in GA.Business.Core

.DESCRIPTION
    This script fixes CS8954 errors where files have multiple namespace declarations
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING FILE-SCOPED NAMESPACE ISSUES" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""

function Fix-FileNamespaces {
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
    
    # Find all namespace declarations
    $namespaceLines = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^\s*namespace\s+([^;{]+)") {
            $namespaceLines += @{
                Index = $i
                Line = $lines[$i].Trim()
                Namespace = $matches[1].Trim()
            }
        }
    }
    
    # If we have multiple namespace declarations, fix it
    if ($namespaceLines.Count -gt 1) {
        Write-Host "Fixing multiple namespaces in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        # Use the first namespace declaration
        $primaryNamespace = $namespaceLines[0].Namespace
        
        # Remove all namespace declarations
        $newLines = @()
        $usingStatements = @()
        $skipUntilBrace = $false
        $braceCount = 0
        
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            
            # Collect using statements
            if ($line -match "^\s*using\s+[^;]+;") {
                $cleanUsing = $line.Trim()
                if ($usingStatements -notcontains $cleanUsing) {
                    $usingStatements += $cleanUsing
                }
                continue
            }
            
            # Skip namespace declarations
            if ($line -match "^\s*namespace\s+") {
                $skipUntilBrace = $true
                continue
            }
            
            # Skip opening braces after namespace
            if ($skipUntilBrace -and $line.Trim() -eq "{") {
                $skipUntilBrace = $false
                continue
            }
            
            # Track braces to remove extra closing braces
            if ($line.Trim() -eq "{") {
                $braceCount++
            } elseif ($line.Trim() -eq "}") {
                $braceCount--
                # Skip extra closing braces
                if ($braceCount -lt 0) {
                    $braceCount = 0
                    continue
                }
            }
            
            $newLines += $line
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
        $finalContent += "namespace $primaryNamespace"
        $finalContent += "{"
        
        # Add the content
        $finalContent += $newLines
        
        # Ensure proper closing
        $finalContent += "}"
        
        $newContent = $finalContent -join "`r`n"
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $newContent -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

# Get all C# files in GA.Business.Core that might have namespace issues
$coreProjectPath = "Common/GA.Business.Core"
$csFiles = Get-ChildItem -Path $coreProjectPath -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Processing $($csFiles.Count) C# files in GA.Business.Core" -ForegroundColor Cyan
Write-Host ""

$fixedFiles = 0
foreach ($csFile in $csFiles) {
    if (Fix-FileNamespaces -FilePath $csFile.FullName) {
        $fixedFiles++
    }
}

Write-Host ""
Write-Host "Fixed file-scoped namespace issues in $fixedFiles files" -ForegroundColor Green

if (-not $DryRun) {
    Write-Host ""
    Write-Host "Testing build..." -ForegroundColor Cyan
    
    # Test build the GA.Business.Core project
    dotnet build $coreProjectPath/GA.Business.Core.csproj --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "GA.Business.Core builds successfully!" -ForegroundColor Green
    } else {
        Write-Host "GA.Business.Core still has build issues - continuing with fixes" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "File-scoped namespace fixes complete!" -ForegroundColor Green
