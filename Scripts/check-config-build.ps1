#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Check GA.Business.Config build issues

.DESCRIPTION
    This script checks what's preventing GA.Business.Config from building
#>

$ErrorActionPreference = "Stop"

Write-Host "CHECKING GA.BUSINESS.CONFIG BUILD" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green
Write-Host ""

Write-Host "Building GA.Business.Config with detailed output..." -ForegroundColor Cyan

try {
    $output = dotnet build "Common\GA.Business.Config\GA.Business.Config.csproj" --verbosity normal --no-restore 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ GA.Business.Config builds successfully!" -ForegroundColor Green
    } else {
        Write-Host "❌ GA.Business.Config build failed" -ForegroundColor Red
        Write-Host ""
        Write-Host "Build errors:" -ForegroundColor Yellow
        
        # Show only error lines
        $errorLines = $output | Where-Object { $_ -match "error" -or $_ -match "Error" }
        foreach ($line in $errorLines | Select-Object -First 10) {
            Write-Host "  $line" -ForegroundColor Red
        }
        
        Write-Host ""
        Write-Host "Full output (last 20 lines):" -ForegroundColor Yellow
        $output | Select-Object -Last 20 | ForEach-Object {
            Write-Host "  $_" -ForegroundColor Gray
        }
    }
    
} catch {
    Write-Host "Exception during build: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Config build check complete!" -ForegroundColor Green
