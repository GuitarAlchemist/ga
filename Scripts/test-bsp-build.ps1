#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test if GA.BSP.Core builds successfully after moving BSP code

.DESCRIPTION
    This script tests if GA.BSP.Core builds with the moved BSP code
#>

$ErrorActionPreference = "Stop"

Write-Host "TESTING GA.BSP.CORE BUILD" -ForegroundColor Green
Write-Host "==========================" -ForegroundColor Green
Write-Host ""

Write-Host "Building GA.BSP.Core project..." -ForegroundColor Cyan

try {
    dotnet build "Common\GA.BSP.Core\GA.BSP.Core.csproj" --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ GA.BSP.Core builds successfully!" -ForegroundColor Green
        Write-Host "✅ BSP code separation was successful!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "❌ GA.BSP.Core has build issues" -ForegroundColor Red
        Write-Host "The moved BSP code may need namespace or reference fixes" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "Error during build: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "BSP build test complete!" -ForegroundColor Green
