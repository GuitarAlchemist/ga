#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix internal namespace references in GA.Business.Core

.DESCRIPTION
    This script updates using statements to reference the correct internal namespaces
    within GA.Business.Core project structure
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING INTERNAL NAMESPACES IN GA.BUSINESS.CORE" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN - Showing what would be updated:" -ForegroundColor Yellow
    Write-Host ""
}

# Define the correct internal namespace mappings for GA.Business.Core
$internalNamespaceMappings = @{
    # Core music theory namespaces within GA.Business.Core
    "using Atonal;" = "using GA.Business.Core.Atonal;"
    "using Notes;" = "using GA.Business.Core.Notes;"
    "using Tonal;" = "using GA.Business.Core.Tonal;"
    "using Intervals;" = "using GA.Business.Core.Intervals;"
    "using Chords;" = "using GA.Business.Core.Chords;"
    "using Scales;" = "using GA.Business.Core.Scales;"
    
    # Analytics and configuration within GA.Business.Core
    "using Analytics;" = "using GA.Business.Core.Analytics;"
    "using Configuration;" = "using GA.Business.Core.Configuration;"
    "using Extensions;" = "using GA.Business.Core.Extensions;"
    
    # Specific subdirectories
    "using Invariants;" = "using GA.Business.Core.Invariants;"
    "using Fretboard;" = "using GA.Business.Core.Fretboard;"
    "using Spatial;" = "using GA.Business.Core.Spatial;"
    "using BSP;" = "using GA.Business.Core.BSP;"
    "using AI;" = "using GA.Business.Core.AI;"
    "using Data;" = "using GA.Business.Core.Data;"
    "using Services;" = "using GA.Business.Core.Services;"
    "using Performance;" = "using GA.Business.Core.Performance;"
    "using Diagnostics;" = "using GA.Business.Core.Diagnostics;"
    "using Microservices;" = "using GA.Business.Core.Microservices;"
    
    # Fix specific namespace references that were incorrectly updated
    "using GA.Core.Atonal;" = "using GA.Business.Core.Atonal;"
    "using GA.Core.Notes;" = "using GA.Business.Core.Notes;"
    "using GA.Core.Tonal;" = "using GA.Business.Core.Tonal;"
    "using GA.Core.Intervals;" = "using GA.Business.Core.Intervals;"
    "using GA.Core.Chords;" = "using GA.Business.Core.Chords;"
    "using GA.Core.Scales;" = "using GA.Business.Core.Scales;"
    "using GA.Core.Extensions;" = "using GA.Business.Core.Extensions;"
    
    # Fix business logic references
    "using GA.Business.Analysis;" = "using GA.Business.Core.Analytics;"
    "using GA.Business.Analysis.Invariants;" = "using GA.Business.Core.Invariants;"
    "using GA.Business.Config;" = "using GA.Business.Core.Configuration;"
}

function Update-InternalNamespaces {
    param(
        [string]$FilePath,
        [hashtable]$Mappings
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    $changed = $false
    
    foreach ($mapping in $Mappings.GetEnumerator()) {
        $oldUsing = $mapping.Key
        $newUsing = $mapping.Value
        
        if ($content.Contains($oldUsing)) {
            $content = $content.Replace($oldUsing, $newUsing)
            $changed = $true
            Write-Host "  - $oldUsing -> $newUsing" -ForegroundColor Gray
        }
    }
    
    if ($changed) {
        Write-Host "Updated: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

# Get all C# files in GA.Business.Core
$coreProjectPath = "Common/GA.Business.Core"
if (-not (Test-Path $coreProjectPath)) {
    Write-Host "GA.Business.Core project not found at: $coreProjectPath" -ForegroundColor Red
    exit 1
}

$csFiles = Get-ChildItem -Path $coreProjectPath -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Found $($csFiles.Count) C# files in GA.Business.Core" -ForegroundColor Cyan
Write-Host ""

$updatedFiles = 0
foreach ($csFile in $csFiles) {
    if (Update-InternalNamespaces -FilePath $csFile.FullName -Mappings $internalNamespaceMappings) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Updated $updatedFiles files with corrected internal namespaces" -ForegroundColor Green

if ($DryRun) {
    Write-Host ""
    Write-Host "Run without -DryRun to apply the changes" -ForegroundColor Cyan
} else {
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
Write-Host "Internal namespace fixes complete!" -ForegroundColor Green
