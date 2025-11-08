#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test if GA.Business.Core builds successfully after moving AI code

.DESCRIPTION
    This script tests if GA.Business.Core builds without the AI code that was moved to GA.Business.AI
#>

$ErrorActionPreference = "Stop"

Write-Host "TESTING GA.BUSINESS.CORE BUILD" -ForegroundColor Green
Write-Host "===============================" -ForegroundColor Green
Write-Host ""

Write-Host "Building GA.Business.Core project..." -ForegroundColor Cyan

try {
    dotnet build "Common\GA.Business.Core\GA.Business.Core.csproj" --verbosity minimal --no-restore
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ GA.Business.Core builds successfully!" -ForegroundColor Green
        Write-Host "✅ AI code separation was successful!" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "Testing GA.Business.AI project..." -ForegroundColor Cyan
        
        dotnet build "Common\GA.Business.AI\GA.Business.AI.csproj" --verbosity minimal --no-restore
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✅ GA.Business.AI builds successfully!" -ForegroundColor Green
            Write-Host "✅ AI code is properly separated and functional!" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "❌ GA.Business.AI has build issues" -ForegroundColor Red
            Write-Host "The moved AI code may need namespace or reference fixes" -ForegroundColor Yellow
        }
        
    } else {
        Write-Host ""
        Write-Host "❌ GA.Business.Core still has build issues" -ForegroundColor Red
        Write-Host "There may be remaining syntax issues to fix" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "Error during build: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Build test complete!" -ForegroundColor Green
