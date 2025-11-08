#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Update namespaces in moved AI files

.DESCRIPTION
    This script updates the namespaces in the AI files that were moved from GA.Business.Core to GA.Business.AI
#>

$ErrorActionPreference = "Stop"

Write-Host "UPDATING AI NAMESPACES" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host ""

$aiPath = "Common\GA.Business.AI\AI"

if (-not (Test-Path $aiPath)) {
    Write-Host "AI directory not found: $aiPath" -ForegroundColor Yellow
    exit 0
}

function Update-Namespaces {
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
    $content = $content -replace "namespace GA\.Business\.Core\.AI", "namespace GA.Business.AI"
    
    # Update using statements
    $content = $content -replace "using GA\.Business\.Core\.AI", "using GA.Business.AI"
    
    if ($content -ne $originalContent) {
        Write-Host "Updated namespaces in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        return $true
    }
    
    return $false
}

# Get all C# files in the AI directory
$csFiles = Get-ChildItem -Path $aiPath -Recurse -Filter "*.cs"

Write-Host "Processing $($csFiles.Count) C# files in AI directory" -ForegroundColor Cyan
Write-Host ""

$updatedFiles = 0
foreach ($csFile in $csFiles) {
    if (Update-Namespaces -FilePath $csFile.FullName) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Updated namespaces in $updatedFiles files" -ForegroundColor Green

# Now check if GA.Business.AI project needs to reference GA.Business.Core
$aiProjectFile = "Common\GA.Business.AI\GA.Business.AI.csproj"
if (Test-Path $aiProjectFile) {
    $projectContent = Get-Content $aiProjectFile -Raw
    if ($projectContent -notmatch "GA\.Business\.Core") {
        Write-Host ""
        Write-Host "Note: GA.Business.AI project may need a reference to GA.Business.Core" -ForegroundColor Yellow
        Write-Host "The AI code likely depends on core business types and primitives." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "AI namespace updates complete!" -ForegroundColor Green
