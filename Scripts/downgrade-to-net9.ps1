#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Downgrades all C# and F# projects back to .NET 9
.DESCRIPTION
    This script updates all .csproj and .fsproj files in the repository to target .NET 9.
    It replaces <TargetFramework>net10.0</TargetFramework> with <TargetFramework>net9.0</TargetFramework>
    and updates package references back to .NET 9 versions.
.EXAMPLE
    .\Scripts/downgrade-to-net9.ps1
#>

param(
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "🔄 Downgrading all projects back to .NET 9..." -ForegroundColor Cyan
Write-Host ""

# Find all .csproj and .fsproj files
$projectFiles = Get-ChildItem -Path $repoRoot -Include *.csproj,*.fsproj -Recurse -ErrorAction SilentlyContinue

$updatedCount = 0
$skippedCount = 0
$errorCount = 0

foreach ($projectFile in $projectFiles) {
    try {
        $relativePath = $projectFile.FullName.Replace($repoRoot, "").TrimStart('\', '/')
        
        # Read the project file
        $content = Get-Content -Path $projectFile.FullName -Raw
        $originalContent = $content
        
        # Replace net10.0 with net9.0
        $content = $content -replace '<TargetFramework>net10\.0</TargetFramework>', '<TargetFramework>net9.0</TargetFramework>'
        $content = $content -replace '<TargetFrameworks>net10\.0</TargetFrameworks>', '<TargetFrameworks>net9.0</TargetFrameworks>'
        
        # Handle multi-target frameworks (e.g., net8.0;net10.0)
        $content = $content -replace '<TargetFrameworks>([^<]*);net10\.0([^<]*)</TargetFrameworks>', '<TargetFrameworks>$1;net9.0$2</TargetFrameworks>'
        $content = $content -replace '<TargetFrameworks>net10\.0;([^<]*)</TargetFrameworks>', '<TargetFrameworks>net9.0;$1</TargetFrameworks>'
        
        # Downgrade Aspire package versions (10.x.x -> 9.x.x)
        $content = $content -replace 'Aspire\.([^"]+)" Version="10\.(\d+)\.(\d+)"', 'Aspire.$1" Version="9.$2.$3"'
        
        # Downgrade Microsoft.AspNetCore packages (10.0.x -> 9.0.x)
        $content = $content -replace 'Microsoft\.AspNetCore\.([^"]+)" Version="10\.0\.(\d+)"', 'Microsoft.AspNetCore.$1" Version="9.0.$2"'
        $content = $content -replace 'Microsoft\.AspNetCore\.([^"]+)" Version="10\.0\.0-rc\.2\.\d+"', 'Microsoft.AspNetCore.$1" Version="9.0.9"'
        
        # Downgrade Microsoft.Extensions packages (10.0.x -> 9.0.x)
        $content = $content -replace 'Microsoft\.Extensions\.([^"]+)" Version="10\.0\.(\d+)"', 'Microsoft.Extensions.$1" Version="9.0.$2"'
        $content = $content -replace 'Microsoft\.Extensions\.([^"]+)" Version="10\.0\.0-rc\.2\.\d+"', 'Microsoft.Extensions.$1" Version="9.0.9"'
        
        # Downgrade Microsoft.EntityFrameworkCore packages (10.0.x -> 9.0.x)
        $content = $content -replace 'Microsoft\.EntityFrameworkCore\.([^"]+)" Version="10\.0\.(\d+)"', 'Microsoft.EntityFrameworkCore.$1" Version="9.0.$2"'
        $content = $content -replace 'Microsoft\.EntityFrameworkCore\.([^"]+)" Version="10\.0\.0-rc\.2\.\d+"', 'Microsoft.EntityFrameworkCore.$1" Version="9.0.0"'
        
        if ($content -ne $originalContent) {
            if ($WhatIf) {
                Write-Host "  [WHATIF] Would update: $relativePath" -ForegroundColor Yellow
            } else {
                Set-Content -Path $projectFile.FullName -Value $content -NoNewline
                Write-Host "  ✅ Updated: $relativePath" -ForegroundColor Green
            }
            $updatedCount++
        } else {
            Write-Host "  ⏭️  Skipped (no changes): $relativePath" -ForegroundColor Gray
            $skippedCount++
        }
    }
    catch {
        Write-Host "  ❌ Error updating $relativePath : $_" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host ""
Write-Host "📊 Summary:" -ForegroundColor Cyan
Write-Host "  Updated: $updatedCount" -ForegroundColor Green
Write-Host "  Skipped: $skippedCount" -ForegroundColor Gray
Write-Host "  Errors:  $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Gray" })
Write-Host ""

if ($WhatIf) {
    Write-Host "⚠️  This was a dry run. Use without -WhatIf to apply changes." -ForegroundColor Yellow
} else {
    Write-Host "✅ Downgrade complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Run: dotnet restore AllProjects.sln" -ForegroundColor White
    Write-Host "  2. Run: dotnet build AllProjects.sln" -ForegroundColor White
    Write-Host "  3. Run: dotnet test AllProjects.sln" -ForegroundColor White
}

