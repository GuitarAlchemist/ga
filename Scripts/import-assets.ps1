#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Import 3D assets (GLB files) into the Guitar Alchemist asset library

.DESCRIPTION
    This script helps import GLB files from the Assets/Downloaded directory
    into MongoDB using the GaCLI asset-import command.

.PARAMETER Category
    Asset category (Decorative, Gems, Lighting, Architecture, Furniture)

.PARAMETER License
    License type (e.g., "CC0", "CC BY 4.0", "CC BY-SA 4.0")

.PARAMETER Source
    Source of the assets (e.g., "Sketchfab", "CGTrader", "Quaternius")

.PARAMETER Author
    Author/creator of the assets

.PARAMETER DryRun
    Show what would be imported without actually importing

.PARAMETER Verbose
    Show detailed output

.EXAMPLE
    .\Scripts\import-assets.ps1 -Category Gems -License "CC0" -Source "Sketchfab"

.EXAMPLE
    .\Scripts\import-assets.ps1 -Category Decorative -License "CC BY 4.0" -Source "Sketchfab" -Author "ArtistName" -Verbose

.EXAMPLE
    .\Scripts\import-assets.ps1 -DryRun
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Decorative", "Gems", "Lighting", "Architecture", "Furniture", "Nature", "Interactive", "Collectible")]
    [string]$Category,

    [Parameter(Mandatory=$false)]
    [string]$License = "Unknown",

    [Parameter(Mandatory=$false)]
    [string]$Source = "Local Import",

    [Parameter(Mandatory=$false)]
    [string]$Author,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun,

    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Colors
$ColorReset = "`e[0m"
$ColorGreen = "`e[32m"
$ColorYellow = "`e[33m"
$ColorRed = "`e[31m"
$ColorBlue = "`e[34m"
$ColorCyan = "`e[36m"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = $ColorReset
    )
    Write-Host "${Color}${Message}${ColorReset}"
}

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-ColorOutput "═══════════════════════════════════════════════════════════════" $ColorCyan
    Write-ColorOutput "  $Title" $ColorCyan
    Write-ColorOutput "═══════════════════════════════════════════════════════════════" $ColorCyan
    Write-Host ""
}

function Get-AssetFiles {
    param([string]$BasePath)
    
    $files = @()
    
    if ($Category) {
        $categoryPath = Join-Path $BasePath $Category
        if (Test-Path $categoryPath) {
            $files = Get-ChildItem -Path $categoryPath -Filter "*.glb" -Recurse
        }
    } else {
        $files = Get-ChildItem -Path $BasePath -Filter "*.glb" -Recurse
    }
    
    return $files
}

function Format-FileSize {
    param([long]$Bytes)
    
    if ($Bytes -ge 1GB) {
        return "{0:N2} GB" -f ($Bytes / 1GB)
    } elseif ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    } elseif ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    } else {
        return "$Bytes bytes"
    }
}

function Import-Asset {
    param(
        [System.IO.FileInfo]$File,
        [string]$AssetCategory,
        [string]$AssetLicense,
        [string]$AssetSource,
        [string]$AssetAuthor
    )
    
    $name = [System.IO.Path]::GetFileNameWithoutExtension($File.Name)
    $size = Format-FileSize $File.Length
    
    Write-ColorOutput "  📦 $name ($size)" $ColorBlue
    
    if ($DryRun) {
        Write-ColorOutput "     [DRY RUN] Would import with:" $ColorYellow
        Write-ColorOutput "       Category: $AssetCategory" $ColorYellow
        Write-ColorOutput "       License: $AssetLicense" $ColorYellow
        Write-ColorOutput "       Source: $AssetSource" $ColorYellow
        if ($AssetAuthor) {
            Write-ColorOutput "       Author: $AssetAuthor" $ColorYellow
        }
        return $true
    }
    
    # Build command arguments
    $args = @(
        "asset-import",
        $File.FullName,
        "--category", $AssetCategory,
        "--license", $AssetLicense,
        "--source", $AssetSource
    )
    
    if ($AssetAuthor) {
        $args += "--author"
        $args += $AssetAuthor
    }
    
    if ($Verbose) {
        $args += "--verbose"
    }
    
    # Run import command
    try {
        Push-Location (Join-Path $PSScriptRoot ".." "GaCLI")
        $output = & dotnet run -- @args 2>&1
        Pop-Location
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "     ✅ Imported successfully" $ColorGreen
            if ($Verbose) {
                Write-Host $output
            }
            return $true
        } else {
            Write-ColorOutput "     ❌ Import failed: $output" $ColorRed
            return $false
        }
    } catch {
        Pop-Location
        Write-ColorOutput "     ❌ Import failed: $_" $ColorRed
        return $false
    }
}

# Main script
Write-Header "3D Asset Import Tool"

# Check if Assets/Downloaded directory exists
$assetsPath = Join-Path $PSScriptRoot ".." "Assets" "Downloaded"
if (-not (Test-Path $assetsPath)) {
    Write-ColorOutput "❌ Assets/Downloaded directory not found!" $ColorRed
    Write-ColorOutput "   Creating directory structure..." $ColorYellow
    
    $categories = @("Decorative", "Gems", "Lighting", "Architecture", "Furniture")
    foreach ($cat in $categories) {
        $catPath = Join-Path $assetsPath $cat
        New-Item -ItemType Directory -Force -Path $catPath | Out-Null
    }
    
    Write-ColorOutput "✅ Directory structure created" $ColorGreen
    Write-ColorOutput "   Please download GLB files to Assets/Downloaded/<category>/" $ColorYellow
    exit 0
}

# Get asset files
Write-ColorOutput "🔍 Scanning for GLB files..." $ColorCyan
$files = Get-AssetFiles -BasePath $assetsPath

if ($files.Count -eq 0) {
    Write-ColorOutput "❌ No GLB files found!" $ColorRed
    if ($Category) {
        Write-ColorOutput "   Searched in: Assets/Downloaded/$Category" $ColorYellow
    } else {
        Write-ColorOutput "   Searched in: Assets/Downloaded" $ColorYellow
    }
    Write-ColorOutput "   Please download GLB files first (see Docs/ASSET_DOWNLOAD_GUIDE.md)" $ColorYellow
    exit 0
}

Write-ColorOutput "✅ Found $($files.Count) GLB file(s)" $ColorGreen
Write-Host ""

# Group files by category
$filesByCategory = $files | Group-Object { 
    $relativePath = $_.FullName.Replace($assetsPath, "").TrimStart("\", "/")
    $parts = $relativePath.Split([IO.Path]::DirectorySeparatorChar)
    if ($parts.Length -gt 1) { $parts[0] } else { "Uncategorized" }
}

# Import files
$totalFiles = 0
$successCount = 0
$failureCount = 0

foreach ($group in $filesByCategory) {
    $categoryName = $group.Name
    $categoryFiles = $group.Group
    
    Write-ColorOutput "📁 Category: $categoryName ($($categoryFiles.Count) file(s))" $ColorCyan
    Write-Host ""
    
    # Determine category for import
    $importCategory = if ($Category) { $Category } else { $categoryName }
    
    foreach ($file in $categoryFiles) {
        $totalFiles++
        $success = Import-Asset -File $file `
                                -AssetCategory $importCategory `
                                -AssetLicense $License `
                                -AssetSource $Source `
                                -AssetAuthor $Author
        
        if ($success) {
            $successCount++
        } else {
            $failureCount++
        }
    }
    
    Write-Host ""
}

# Summary
Write-Header "Import Summary"

Write-ColorOutput "Total files: $totalFiles" $ColorBlue
Write-ColorOutput "✅ Successful: $successCount" $ColorGreen
if ($failureCount -gt 0) {
    Write-ColorOutput "❌ Failed: $failureCount" $ColorRed
}

if ($DryRun) {
    Write-Host ""
    Write-ColorOutput "This was a dry run. No assets were actually imported." $ColorYellow
    Write-ColorOutput "Run without -DryRun to perform the import." $ColorYellow
}

Write-Host ""

# List imported assets
if (-not $DryRun -and $successCount -gt 0) {
    Write-ColorOutput "To view imported assets, run:" $ColorCyan
    Write-ColorOutput "  cd GaCLI && dotnet run -- asset-list --verbose" $ColorBlue
}

exit 0

