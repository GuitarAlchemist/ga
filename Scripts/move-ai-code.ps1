#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Move AI code from GA.Business.Core to GA.Business.AI

.DESCRIPTION
    This script moves the AI directory and all its contents from GA.Business.Core to GA.Business.AI
    where it belongs according to proper separation of concerns.
#>

$ErrorActionPreference = "Stop"

Write-Host "MOVING AI CODE TO PROPER PROJECT" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""

$sourcePath = "Common\GA.Business.Core\AI"
$destinationPath = "Common\GA.Business.AI\AI"

if (-not (Test-Path $sourcePath)) {
    Write-Host "Source AI directory not found: $sourcePath" -ForegroundColor Yellow
    exit 0
}

if (Test-Path $destinationPath) {
    Write-Host "Destination AI directory already exists: $destinationPath" -ForegroundColor Yellow
    Write-Host "Removing existing directory..." -ForegroundColor Yellow
    Remove-Item -Path $destinationPath -Recurse -Force
}

Write-Host "Moving AI directory from GA.Business.Core to GA.Business.AI..." -ForegroundColor Cyan

try {
    # Move the entire AI directory
    Move-Item -Path $sourcePath -Destination $destinationPath -Force
    Write-Host "Successfully moved AI directory!" -ForegroundColor Green
    
    # List what was moved
    Write-Host ""
    Write-Host "Moved files:" -ForegroundColor Cyan
    Get-ChildItem -Path $destinationPath -Recurse -File | ForEach-Object {
        Write-Host "  $($_.FullName.Replace((Get-Location).Path + '\', ''))" -ForegroundColor Gray
    }
    
    Write-Host ""
    Write-Host "AI code has been successfully moved to GA.Business.AI project!" -ForegroundColor Green
    Write-Host "This follows proper separation of concerns - AI code should be in the AI project." -ForegroundColor Green
    
} catch {
    Write-Host "Error moving AI directory: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "AI code move complete!" -ForegroundColor Green
