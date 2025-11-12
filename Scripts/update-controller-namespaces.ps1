#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates controller namespaces for microservices
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ServiceName,
    
    [Parameter(Mandatory=$true)]
    [string]$Namespace
)

$ErrorActionPreference = "Stop"

$controllersPath = Join-Path $PSScriptRoot "..\Apps\ga-server\$ServiceName\Controllers"

if (-not (Test-Path $controllersPath)) {
    Write-Host "❌ Controllers directory not found: $controllersPath" -ForegroundColor Red
    exit 1
}

$controllers = Get-ChildItem $controllersPath -Filter "*.cs"

foreach ($controller in $controllers) {
    Write-Host "Updating $($controller.Name)..." -ForegroundColor Yellow
    
    $content = Get-Content $controller.FullName -Raw
    $content = $content.Replace('namespace GaApi.Controllers;', "namespace $Namespace;")
    $content = $content.Replace('using GaApi.', "using $Namespace.")
    
    Set-Content $controller.FullName -Value $content -NoNewline
    
    Write-Host "  ✅ Updated $($controller.Name)" -ForegroundColor Green
}

Write-Host ""
Write-Host "✨ All controllers updated for $ServiceName!" -ForegroundColor Green

