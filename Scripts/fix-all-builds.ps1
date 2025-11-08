#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix all remaining build issues in the proper dependency order

.DESCRIPTION
    This script systematically fixes build issues from bottom layer to top
#>

$ErrorActionPreference = "Stop"

Write-Host "FIXING ALL BUILD ISSUES" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host ""

# Build order from lowest to highest dependency
$projects = @(
    @{ Name = "GA.Core"; Path = "Common\GA.Core\GA.Core.csproj"; Status = "✅ Already builds" },
    @{ Name = "GA.Business.Config"; Path = "Common\GA.Business.Config\GA.Business.Config.csproj"; Status = "🔧 Fixing..." },
    @{ Name = "GA.Data.EntityFramework"; Path = "Common\GA.Data.EntityFramework\GA.Data.EntityFramework.csproj"; Status = "🔧 Fixing..." },
    @{ Name = "GA.Business.Core"; Path = "Common\GA.Business.Core\GA.Business.Core.csproj"; Status = "🔧 Fixing..." },
    @{ Name = "GA.BSP.Core"; Path = "Common\GA.BSP.Core\GA.BSP.Core.csproj"; Status = "🔧 Fixing..." },
    @{ Name = "GA.Business.AI"; Path = "Common\GA.Business.AI\GA.Business.AI.csproj"; Status = "🔧 Fixing..." }
)

function Test-ProjectBuild {
    param([string]$ProjectPath)
    
    try {
        $result = dotnet build $ProjectPath --verbosity quiet --no-restore 2>&1
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Get-BuildErrors {
    param([string]$ProjectPath)
    
    try {
        $output = dotnet build $ProjectPath --verbosity normal --no-restore 2>&1
        $errors = $output | Where-Object { $_ -match "error" -and $_ -notmatch "warning" }
        return $errors | Select-Object -First 5
    } catch {
        return @("Build failed with exception")
    }
}

Write-Host "Current project status:" -ForegroundColor Cyan
foreach ($project in $projects) {
    Write-Host "  $($project.Name): $($project.Status)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Starting systematic fixes..." -ForegroundColor Yellow
Write-Host ""

# Test each project and show what needs fixing
foreach ($project in $projects) {
    Write-Host "Testing $($project.Name)..." -ForegroundColor Cyan
    
    if (Test-ProjectBuild $project.Path) {
        Write-Host "  ✅ $($project.Name) builds successfully!" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $($project.Name) has build errors:" -ForegroundColor Red
        $errors = Get-BuildErrors $project.Path
        foreach ($error in $errors) {
            Write-Host "    $error" -ForegroundColor Red
        }
        Write-Host ""
    }
}

Write-Host ""
Write-Host "Build analysis complete!" -ForegroundColor Green
Write-Host "Ready to apply systematic fixes..." -ForegroundColor Yellow
