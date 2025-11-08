#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Update namespaces in moved BSP files

.DESCRIPTION
    This script updates the namespaces in the BSP files that were moved from GA.Business.Core to GA.BSP.Core
#>

$ErrorActionPreference = "Stop"

Write-Host "UPDATING BSP NAMESPACES" -ForegroundColor Green
Write-Host "=======================" -ForegroundColor Green
Write-Host ""

$bspPath = "Common\GA.BSP.Core"

if (-not (Test-Path $bspPath)) {
    Write-Host "BSP directory not found: $bspPath" -ForegroundColor Yellow
    exit 0
}

function Update-BSPNamespaces {
    param(
        [string]$FilePath
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    
    # Update namespace declarations
    $content = $content -replace "namespace GA\.Business\.Core\.BSP", "namespace GA.BSP.Core"
    $content = $content -replace "namespace GA\.Business\.Core\.Spatial", "namespace GA.BSP.Core.Spatial"
    
    # Update using statements
    $content = $content -replace "using GA\.Business\.Core\.BSP", "using GA.BSP.Core"
    $content = $content -replace "using GA\.Business\.Core\.Spatial", "using GA.BSP.Core.Spatial"
    
    if ($content -ne $originalContent) {
        Write-Host "Updated namespaces in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        return $true
    }
    
    return $false
}

# Get all C# files in the BSP directory
$csFiles = Get-ChildItem -Path $bspPath -Recurse -Filter "*.cs"

Write-Host "Processing $($csFiles.Count) C# files in BSP directory" -ForegroundColor Cyan
Write-Host ""

$updatedFiles = 0
foreach ($csFile in $csFiles) {
    if (Update-BSPNamespaces -FilePath $csFile.FullName) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Updated namespaces in $updatedFiles files" -ForegroundColor Green

Write-Host ""
Write-Host "BSP namespace updates complete!" -ForegroundColor Green
