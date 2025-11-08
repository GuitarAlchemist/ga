#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Update all project references after GA.Business.Core.* renaming

.DESCRIPTION
    This script updates all references to the renamed GA.Business.Core.* projects:
    - Updates AllProjects.sln
    - Updates all .csproj ProjectReference paths
    - Updates namespace declarations
    - Updates using statements

.PARAMETER DryRun
    Show what would be updated without actually doing it

.EXAMPLE
    ./update-project-references.ps1 -DryRun
    ./update-project-references.ps1
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "🔧 Updating Project References After Renaming" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
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
    "GA.Business.Core.Intelligence" = "GA.Business.Intelligence"
}

function Update-FileContent {
    param(
        [string]$FilePath,
        [hashtable]$Replacements,
        [string]$Description
    )
    
    if (-not (Test-Path $FilePath)) {
        Write-Host "⚠️  $FilePath not found" -ForegroundColor Yellow
        return
    }
    
    $content = Get-Content $FilePath -Raw
    $originalContent = $content
    
    foreach ($replacement in $Replacements.GetEnumerator()) {
        $oldValue = $replacement.Key
        $newValue = $replacement.Value
        $content = $content -replace [regex]::Escape($oldValue), $newValue
    }
    
    if ($content -ne $originalContent) {
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

# 1. Update AllProjects.sln
Write-Host "1️⃣ Updating AllProjects.sln..." -ForegroundColor Cyan

$slnReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    $oldPath = "Common\$($mapping.Key)"
    $newPath = "Common\$($mapping.Value)"
    $slnReplacements[$oldPath] = $newPath
}

Update-FileContent -FilePath "AllProjects.sln" -Replacements $slnReplacements -Description "solution file"

# 2. Update .csproj files
Write-Host ""
Write-Host "2️⃣ Updating .csproj ProjectReference paths..." -ForegroundColor Cyan

$csprojFiles = Get-ChildItem -Path . -Recurse -Filter "*.csproj" | Where-Object { $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" }

$projectRefReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    $oldRef = "..\$($mapping.Key)\"
    $newRef = "..\$($mapping.Value)\"
    $projectRefReplacements[$oldRef] = $newRef
    
    $oldRef2 = "..\..\Common\$($mapping.Key)\"
    $newRef2 = "..\..\Common\$($mapping.Value)\"
    $projectRefReplacements[$oldRef2] = $newRef2
}

foreach ($csprojFile in $csprojFiles) {
    Update-FileContent -FilePath $csprojFile.FullName -Replacements $projectRefReplacements -Description "project file"
}

# 3. Update namespace declarations
Write-Host ""
Write-Host "3️⃣ Updating namespace declarations..." -ForegroundColor Cyan

$csFiles = Get-ChildItem -Path . -Recurse -Filter "*.cs" | Where-Object { $_.FullName -notlike "*\bin\*" -and $_.FullName -notlike "*\obj\*" }

$namespaceReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    $oldNamespace = "namespace $($mapping.Key)"
    $newNamespace = "namespace $($mapping.Value)"
    $namespaceReplacements[$oldNamespace] = $newNamespace
}

foreach ($csFile in $csFiles) {
    Update-FileContent -FilePath $csFile.FullName -Replacements $namespaceReplacements -Description "C# file"
}

# 4. Update using statements
Write-Host ""
Write-Host "4️⃣ Updating using statements..." -ForegroundColor Cyan

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
    Write-Host "🔨 Building solution to verify changes..." -ForegroundColor Cyan
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
Write-Host "🎉 Project structure cleanup complete!" -ForegroundColor Green
