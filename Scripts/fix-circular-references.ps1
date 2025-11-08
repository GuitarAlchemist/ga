#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix circular reference issues in GA.Business.Core

.DESCRIPTION
    This script removes circular using statements where files are trying to import
    their own namespace or parent namespaces
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "FIXING CIRCULAR REFERENCE ISSUES" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""

# Define circular references to remove
$circularReferences = @{
    # Files in Fretboard namespace shouldn't import GA.Business.Core.Fretboard.Primitives
    "using GA.Business.Core.Fretboard.Primitives;" = ""
    
    # Files in Atonal namespace shouldn't import GA.Business.Core.Atonal
    "using GA.Business.Core.Atonal;" = ""
    
    # Files in Notes namespace shouldn't import GA.Business.Core.Notes
    "using GA.Business.Core.Notes;" = ""
    
    # Files in Intervals namespace shouldn't import GA.Business.Core.Intervals
    "using GA.Business.Core.Intervals;" = ""
    
    # Files in Chords namespace shouldn't import GA.Business.Core.Chords
    "using GA.Business.Core.Chords;" = ""
    
    # Files in Scales namespace shouldn't import GA.Business.Core.Scales
    "using GA.Business.Core.Scales;" = ""
    
    # Files in Tonal namespace shouldn't import GA.Business.Core.Tonal
    "using GA.Business.Core.Tonal;" = ""
}

function Remove-CircularReferences {
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
    
    # Get the file's namespace to determine which circular references to remove
    $fileNamespace = ""
    if ($FilePath -like "*\Fretboard\*") { $fileNamespace = "Fretboard" }
    elseif ($FilePath -like "*\Atonal\*") { $fileNamespace = "Atonal" }
    elseif ($FilePath -like "*\Notes\*") { $fileNamespace = "Notes" }
    elseif ($FilePath -like "*\Intervals\*") { $fileNamespace = "Intervals" }
    elseif ($FilePath -like "*\Chords\*") { $fileNamespace = "Chords" }
    elseif ($FilePath -like "*\Scales\*") { $fileNamespace = "Scales" }
    elseif ($FilePath -like "*\Tonal\*") { $fileNamespace = "Tonal" }
    
    foreach ($mapping in $Mappings.GetEnumerator()) {
        $circularUsing = $mapping.Key
        $replacement = $mapping.Value
        
        # Only remove if this is actually a circular reference for this file
        $shouldRemove = $false
        
        if ($circularUsing -eq "using GA.Business.Core.Fretboard.Primitives;" -and $fileNamespace -eq "Fretboard") {
            $shouldRemove = $true
        }
        elseif ($circularUsing -eq "using GA.Business.Core.Atonal;" -and $fileNamespace -eq "Atonal") {
            $shouldRemove = $true
        }
        elseif ($circularUsing -eq "using GA.Business.Core.Notes;" -and $fileNamespace -eq "Notes") {
            $shouldRemove = $true
        }
        elseif ($circularUsing -eq "using GA.Business.Core.Intervals;" -and $fileNamespace -eq "Intervals") {
            $shouldRemove = $true
        }
        elseif ($circularUsing -eq "using GA.Business.Core.Chords;" -and $fileNamespace -eq "Chords") {
            $shouldRemove = $true
        }
        elseif ($circularUsing -eq "using GA.Business.Core.Scales;" -and $fileNamespace -eq "Scales") {
            $shouldRemove = $true
        }
        elseif ($circularUsing -eq "using GA.Business.Core.Tonal;" -and $fileNamespace -eq "Tonal") {
            $shouldRemove = $true
        }
        
        if ($shouldRemove -and $content.Contains($circularUsing)) {
            $content = $content.Replace($circularUsing, $replacement)
            $changed = $true
            Write-Host "  - Removed circular reference: $circularUsing" -ForegroundColor Gray
        }
    }
    
    if ($changed) {
        Write-Host "Fixed circular references in: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
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

$updatedFiles = 0
foreach ($csFile in $csFiles) {
    if (Remove-CircularReferences -FilePath $csFile.FullName -Mappings $circularReferences) {
        $updatedFiles++
    }
}

Write-Host ""
Write-Host "Fixed circular references in $updatedFiles files" -ForegroundColor Green

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
Write-Host "Circular reference fixes complete!" -ForegroundColor Green
