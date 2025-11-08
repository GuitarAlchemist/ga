#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Fix all remaining build issues systematically

.DESCRIPTION
    This script fixes build issues from bottom layer to top
#>

$ErrorActionPreference = "Stop"

Write-Host "FIXING ALL BUILD ISSUES" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host ""

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
        return $errors | Select-Object -First 3
    } catch {
        return @("Build failed with exception")
    }
}

# Test GA.Business.Config first
Write-Host "1. Testing GA.Business.Config..." -ForegroundColor Cyan
$configPath = "Common\GA.Business.Config\GA.Business.Config.csproj"

if (Test-ProjectBuild $configPath) {
    Write-Host "   SUCCESS: GA.Business.Config builds!" -ForegroundColor Green
} else {
    Write-Host "   FAILED: GA.Business.Config has errors:" -ForegroundColor Red
    $errors = Get-BuildErrors $configPath
    foreach ($err in $errors) {
        Write-Host "     $err" -ForegroundColor Red
    }
}

Write-Host ""

# Test GA.Data.EntityFramework
Write-Host "2. Testing GA.Data.EntityFramework..." -ForegroundColor Cyan
$dataPath = "Common\GA.Data.EntityFramework\GA.Data.EntityFramework.csproj"

if (Test-ProjectBuild $dataPath) {
    Write-Host "   SUCCESS: GA.Data.EntityFramework builds!" -ForegroundColor Green
} else {
    Write-Host "   FAILED: GA.Data.EntityFramework has errors:" -ForegroundColor Red
    $errors = Get-BuildErrors $dataPath
    foreach ($err in $errors) {
        Write-Host "     $err" -ForegroundColor Red
    }
}

Write-Host ""

# Test GA.Business.Core
Write-Host "3. Testing GA.Business.Core..." -ForegroundColor Cyan
$corePath = "Common\GA.Business.Core\GA.Business.Core.csproj"

if (Test-ProjectBuild $corePath) {
    Write-Host "   SUCCESS: GA.Business.Core builds!" -ForegroundColor Green
} else {
    Write-Host "   FAILED: GA.Business.Core has errors:" -ForegroundColor Red
    $errors = Get-BuildErrors $corePath
    foreach ($err in $errors) {
        Write-Host "     $err" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Build analysis complete!" -ForegroundColor Green
