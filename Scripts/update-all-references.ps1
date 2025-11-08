#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Update all project references after renaming

.DESCRIPTION
    Updates all .csproj files to use the new project names and paths
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "🔧 Updating All Project References" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Define the reference mappings
$referenceMappings = @{
    "GA.Business.Core.AI" = "GA.Business.AI"
    "GA.Business.Core.Analysis" = "GA.Business.Analysis"
    "GA.Business.Core.Fretboard" = "GA.Business.Fretboard"
    "GA.Business.Core.Harmony" = "GA.Business.Harmony"
    "GA.Business.Core.Orchestration" = "GA.Business.Orchestration"
    "GA.Business.Core.UI" = "GA.Business.UI"
    "GA.Business.Core.Web" = "GA.Business.Web"
    "GA.Business.Core.Mapping" = "GA.Business.Mapping"
    "GA.Business.Core.Graphiti" = "GA.Business.Graphiti"
}

function Update-FileContent {
    param(
        [string]$FilePath,
        [hashtable]$Replacements,
        [string]$Description
    )
    
    if (-not (Test-Path $FilePath)) {
        return
    }
    
    $content = Get-Content $FilePath -Raw
    $originalContent = $content
    $changed = $false
    
    foreach ($replacement in $Replacements.GetEnumerator()) {
        $oldValue = $replacement.Key
        $newValue = $replacement.Value
        
        if ($content -match [regex]::Escape($oldValue)) {
            $content = $content -replace [regex]::Escape($oldValue), $newValue
            $changed = $true
        }
    }
    
    if ($changed) {
        Write-Host "📝 Updating $Description`: $FilePath" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline
            Write-Host "  ✅ Updated successfully" -ForegroundColor Green
        }
    }
}

if ($DryRun) {
    Write-Host "🔍 DRY RUN - Showing what would be updated:" -ForegroundColor Yellow
    Write-Host ""
}

# Update .csproj files
Write-Host "1️⃣ Updating .csproj ProjectReference paths..." -ForegroundColor Cyan

$csprojFiles = Get-ChildItem -Path . -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

$projectRefReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    # Handle different relative path patterns
    $patterns = @(
        "..\$($mapping.Key)\",
        "..\..\Common\$($mapping.Key)\",
        "Common\$($mapping.Key)\",
        "$($mapping.Key)\"
    )
    
    foreach ($pattern in $patterns) {
        $newPattern = $pattern -replace [regex]::Escape($mapping.Key), $mapping.Value
        $projectRefReplacements[$pattern] = $newPattern
    }
}

foreach ($csprojFile in $csprojFiles) {
    Update-FileContent -FilePath $csprojFile.FullName -Replacements $projectRefReplacements -Description "project file"
}

Write-Host ""
Write-Host "2️⃣ Updating namespace declarations..." -ForegroundColor Cyan

$csFiles = Get-ChildItem -Path . -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

$namespaceReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    $oldNamespace = "namespace $($mapping.Key)"
    $newNamespace = "namespace $($mapping.Value)"
    $namespaceReplacements[$oldNamespace] = $newNamespace
}

foreach ($csFile in $csFiles) {
    Update-FileContent -FilePath $csFile.FullName -Replacements $namespaceReplacements -Description "namespace"
}

Write-Host ""
Write-Host "3️⃣ Updating using statements..." -ForegroundColor Cyan

$usingReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    $oldUsing = "using $($mapping.Key)"
    $newUsing = "using $($mapping.Value)"
    $usingReplacements[$oldUsing] = $newUsing
}

foreach ($csFile in $csFiles) {
    Update-FileContent -FilePath $csFile.FullName -Replacements $usingReplacements -Description "using statements"
}

if (-not $DryRun) {
    Write-Host ""
    Write-Host "🔨 Testing build..." -ForegroundColor Cyan
    dotnet build AllProjects.sln --verbosity minimal
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build successful! All references updated correctly." -ForegroundColor Green
    } else {
        Write-Host "❌ Build failed. Some references may need manual fixing." -ForegroundColor Red
    }
} else {
    Write-Host ""
    Write-Host "💡 Run without -DryRun to perform the actual updates" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "🎉 Reference update complete!" -ForegroundColor Green
