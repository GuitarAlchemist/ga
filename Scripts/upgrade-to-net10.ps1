#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Upgrades all C# and F# projects to .NET 10 RC1
.DESCRIPTION
    This script updates all .csproj and .fsproj files in the repository to target .NET 10.
    It replaces <TargetFramework>net9.0</TargetFramework> with <TargetFramework>net10.0</TargetFramework>
    and updates package references that are version-specific.
.EXAMPLE
    .\Scripts\upgrade-to-net10.ps1
#>

param(
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "🚀 Upgrading all projects to .NET 10 RC1..." -ForegroundColor Cyan
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
        
        # Replace net9.0 with net10.0
        $content = $content -replace '<TargetFramework>net9\.0</TargetFramework>', '<TargetFramework>net10.0</TargetFramework>'
        $content = $content -replace '<TargetFrameworks>net9\.0</TargetFrameworks>', '<TargetFrameworks>net10.0</TargetFrameworks>'
        
        # Handle multi-target frameworks (e.g., net8.0;net9.0)
        $content = $content -replace '<TargetFrameworks>([^<]*);net9\.0([^<]*)</TargetFrameworks>', '<TargetFrameworks>$1;net10.0$2</TargetFrameworks>'
        $content = $content -replace '<TargetFrameworks>net9\.0;([^<]*)</TargetFrameworks>', '<TargetFrameworks>net10.0;$1</TargetFrameworks>'
        
        # Update Aspire package versions (9.x.x -> latest available)
        # Note: Aspire packages are still at 9.x for .NET 10 RC1
        # $content = $content -replace 'Aspire\.([^"]+)" Version="9\.(\d+)\.(\d+)"', 'Aspire.$1" Version="9.$2.$3"'

        # Update Microsoft.AspNetCore packages (9.0.x -> 10.0.0-rc.2.x)
        $content = $content -replace 'Microsoft\.AspNetCore\.([^"]+)" Version="9\.0\.(\d+)"', 'Microsoft.AspNetCore.$1" Version="10.0.0-rc.2.25502.107"'

        # Update Microsoft.Extensions packages (9.0.x -> 10.0.0-rc.2.x)
        $content = $content -replace 'Microsoft\.Extensions\.([^"]+)" Version="9\.0\.(\d+)"', 'Microsoft.Extensions.$1" Version="10.0.0-rc.2.25502.107"'

        # Update Microsoft.EntityFrameworkCore packages (9.0.x -> 10.0.0-rc.2.x)
        $content = $content -replace 'Microsoft\.EntityFrameworkCore\.([^"]+)" Version="9\.0\.(\d+)"', 'Microsoft.EntityFrameworkCore.$1" Version="10.0.0-rc.2.25502.107"'
        
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
    Write-Host "✅ Upgrade complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Run: dotnet restore AllProjects.sln" -ForegroundColor White
    Write-Host "  2. Run: dotnet build AllProjects.sln" -ForegroundColor White
    Write-Host "  3. Run: dotnet test AllProjects.sln" -ForegroundColor White
}

