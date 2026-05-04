#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start the thin Guitar Alchemist chatbot API host.
.PARAMETER SkipBuild
    Skip the initial dotnet build step.
.PARAMETER Port
    Local HTTP port for the chatbot API.
#>

param(
    [switch]$SkipBuild = $false,
    [int]$Port = 5252
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$projectPath = Join-Path $repoRoot "Apps/GaChatbot.Api/GaChatbot.Api.csproj"
$apiUrl = "http://localhost:$Port"

Write-Host "Starting GaChatbot.Api..." -ForegroundColor Cyan
Write-Host "  API URL: $apiUrl" -ForegroundColor White

if (-not $SkipBuild) {
    Write-Host "  Building chatbot API..." -ForegroundColor Yellow
    Push-Location $repoRoot
    dotnet build $projectPath -c Debug
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Host "  Build failed." -ForegroundColor Red
        exit 1
    }
    Pop-Location
}

$env:ASPNETCORE_URLS = $apiUrl
Push-Location $repoRoot
dotnet run --project $projectPath --no-launch-profile
Pop-Location
