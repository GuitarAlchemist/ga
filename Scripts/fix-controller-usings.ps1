#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix using statements in all migrated controllers
#>

$ErrorActionPreference = "Stop"

$services = @(
    "GA.MusicTheory.Service",
    "GA.BSP.Service",
    "GA.AI.Service",
    "GA.Knowledge.Service",
    "GA.Fretboard.Service",
    "GA.Analytics.Service"
)

$requiredUsings = @(
    "using Microsoft.AspNetCore.Mvc;",
    "using Microsoft.AspNetCore.RateLimiting;"
)

Write-Host "🔧 Fixing controller using statements..." -ForegroundColor Cyan
Write-Host ""

foreach ($service in $services) {
    $controllersPath = Join-Path $PSScriptRoot "..\Apps\ga-server\$service\Controllers"
    
    if (-not (Test-Path $controllersPath)) {
        continue
    }
    
    Write-Host "📦 Processing $service..." -ForegroundColor Yellow
    
    $controllers = Get-ChildItem -Path $controllersPath -Filter "*.cs"
    
    foreach ($controller in $controllers) {
        $content = Get-Content $controller.FullName -Raw
        
        # Fix namespace references
        $content = $content.Replace('using Models;', "using $service.Models;")
        $content = $content.Replace('using Services;', "using $service.Services;")
        
        # Add missing using statements if not present
        foreach ($using in $requiredUsings) {
            if ($content -notmatch [regex]::Escape($using)) {
                # Insert after namespace declaration
                $content = $content -replace '(namespace [^;]+;)', "`$1`n$using"
            }
        }
        
        Set-Content $controller.FullName -Value $content -NoNewline
        Write-Host "  ✅ Fixed $($controller.Name)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "✨ Controller using statements fixed!" -ForegroundColor Green

