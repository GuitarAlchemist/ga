#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Complete cleanup and upgrade to .NET 10

.DESCRIPTION
    This script performs comprehensive cleanup after project reorganization:
    1. Updates all .csproj ProjectReference paths
    2. Updates namespace declarations in source files
    3. Updates using statements throughout codebase
    4. Upgrades to .NET 10
    5. Fixes package version conflicts
    6. Verifies solution builds

.PARAMETER SkipUpgrade
    Skip .NET 10 upgrade and just fix references

.EXAMPLE
    ./complete-cleanup-and-upgrade.ps1
    ./complete-cleanup-and-upgrade.ps1 -SkipUpgrade
#>

param(
    [switch]$SkipUpgrade,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "🧹 COMPLETE CLEANUP AND UPGRADE" -ForegroundColor Green
Write-Host "===============================" -ForegroundColor Green
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
        return $false
    }
    
    $content = Get-Content $FilePath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    
    $originalContent = $content
    $changed = $false
    
    foreach ($replacement in $Replacements.GetEnumerator()) {
        $oldValue = $replacement.Key
        $newValue = $replacement.Value
        
        if ($content.Contains($oldValue)) {
            $content = $content.Replace($oldValue, $newValue)
            $changed = $true
        }
    }
    
    if ($changed) {
        Write-Host "📝 Updating $Description`: $(Split-Path $FilePath -Leaf)" -ForegroundColor Green
        
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline -Encoding UTF8
        }
        return $true
    }
    
    return $false
}

if ($DryRun) {
    Write-Host "🔍 DRY RUN - Showing what would be updated:" -ForegroundColor Yellow
    Write-Host ""
}

# Step 1: Update .csproj ProjectReference paths
Write-Host "1️⃣ Updating .csproj ProjectReference paths..." -ForegroundColor Cyan

$csprojFiles = Get-ChildItem -Path . -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

$projectRefReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    # Handle different relative path patterns
    $patterns = @(
        "..\$($mapping.Key)\$($mapping.Key).csproj",
        "..\..\Common\$($mapping.Key)\$($mapping.Key).csproj",
        "Common\$($mapping.Key)\$($mapping.Key).csproj"
    )
    
    foreach ($pattern in $patterns) {
        $newPattern = $pattern.Replace($mapping.Key, $mapping.Value)
        $projectRefReplacements[$pattern] = $newPattern
    }
}

$updatedProjects = 0
foreach ($csprojFile in $csprojFiles) {
    if (Update-FileContent -FilePath $csprojFile.FullName -Replacements $projectRefReplacements -Description "project references") {
        $updatedProjects++
    }
}

Write-Host "  ✅ Updated $updatedProjects project files" -ForegroundColor Green

# Step 2: Update namespace declarations
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

$updatedNamespaces = 0
foreach ($csFile in $csFiles) {
    if (Update-FileContent -FilePath $csFile.FullName -Replacements $namespaceReplacements -Description "namespace") {
        $updatedNamespaces++
    }
}

Write-Host "  ✅ Updated $updatedNamespaces source files" -ForegroundColor Green

# Step 3: Update using statements
Write-Host ""
Write-Host "3️⃣ Updating using statements..." -ForegroundColor Cyan

$usingReplacements = @{}
foreach ($mapping in $referenceMappings.GetEnumerator()) {
    $oldUsing = "using $($mapping.Key)"
    $newUsing = "using $($mapping.Value)"
    $usingReplacements[$oldUsing] = $newUsing
}

$updatedUsings = 0
foreach ($csFile in $csFiles) {
    if (Update-FileContent -FilePath $csFile.FullName -Replacements $usingReplacements -Description "using statements") {
        $updatedUsings++
    }
}

Write-Host "  ✅ Updated $updatedUsings files with using statements" -ForegroundColor Green

# Step 4: Upgrade to .NET 10 (if not skipped)
if (-not $SkipUpgrade) {
    Write-Host ""
    Write-Host "4️⃣ Upgrading to .NET 10..." -ForegroundColor Cyan

    $targetFrameworkReplacements = @{
        "<TargetFramework>net9.0</TargetFramework>" = "<TargetFramework>net10.0</TargetFramework>"
        "<TargetFramework>net8.0</TargetFramework>" = "<TargetFramework>net10.0</TargetFramework>"
    }

    $upgradedProjects = 0
    foreach ($csprojFile in $csprojFiles) {
        if (Update-FileContent -FilePath $csprojFile.FullName -Replacements $targetFrameworkReplacements -Description ".NET version") {
            $upgradedProjects++
        }
    }

    Write-Host "  ✅ Upgraded $upgradedProjects projects to .NET 10" -ForegroundColor Green

    # Update package versions to latest
    Write-Host ""
    Write-Host "5️⃣ Updating package versions..." -ForegroundColor Cyan

    $packageReplacements = @{
        'Version="9.0.0"' = 'Version="10.0.0"'
        'Version="9.0.9"' = 'Version="10.0.0"'
        'Version="9.0.10"' = 'Version="10.0.0"'
    }

    $upgradedPackages = 0
    foreach ($csprojFile in $csprojFiles) {
        if (Update-FileContent -FilePath $csprojFile.FullName -Replacements $packageReplacements -Description "package versions") {
            $upgradedPackages++
        }
    }

    Write-Host "  ✅ Updated packages in $upgradedPackages projects" -ForegroundColor Green
}

# Step 6: Test build
if (-not $DryRun) {
    Write-Host ""
    Write-Host "6️⃣ Testing solution build..." -ForegroundColor Cyan

    # Clean first
    Write-Host "  🧹 Cleaning solution..." -ForegroundColor Gray
    dotnet clean AllProjects.sln --verbosity minimal | Out-Null

    # Restore packages
    Write-Host "  📦 Restoring packages..." -ForegroundColor Gray
    dotnet restore AllProjects.sln --verbosity minimal

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ Package restore successful" -ForegroundColor Green

        # Build
        Write-Host "  🔨 Building solution..." -ForegroundColor Gray
        dotnet build AllProjects.sln --verbosity minimal --no-restore

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ BUILD SUCCESSFUL!" -ForegroundColor Green
        } else {
            Write-Host "  ❌ Build failed - check output above" -ForegroundColor Red
        }
    } else {
        Write-Host "  ❌ Package restore failed" -ForegroundColor Red
    }
} else {
    Write-Host ""
    Write-Host "💡 Run without -DryRun to perform the actual updates and test build" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "CLEANUP AND UPGRADE COMPLETE!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Summary:" -ForegroundColor Cyan
Write-Host "  • Updated $updatedProjects project references" -ForegroundColor White
Write-Host "  • Updated $updatedNamespaces namespace declarations" -ForegroundColor White
Write-Host "  • Updated $updatedUsings using statements" -ForegroundColor White
if (-not $SkipUpgrade) {
    Write-Host "  • Upgraded $upgradedProjects projects to .NET 10" -ForegroundColor White
    Write-Host "  • Updated packages in $upgradedPackages projects" -ForegroundColor White
}
Write-Host ""
Write-Host "Project reorganization is now COMPLETE!" -ForegroundColor Green
