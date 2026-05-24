#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start the thin chatbot API and ga-client together for local development.
.PARAMETER SkipBuild
    Skip the initial chatbot API build step.
.PARAMETER ApiPort
    Local HTTP port for the chatbot API.
.PARAMETER FrontendPort
    Local port for the Vite frontend.
#>

param(
    [switch]$SkipBuild = $false,
    [int]$ApiPort = 5252,
    [int]$FrontendPort = 5173
)

$ErrorActionPreference = "Continue"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$apiProject = Join-Path $repoRoot "Apps/GaChatbot.Api/GaChatbot.Api.csproj"
$frontendDir = Join-Path $repoRoot "Apps/ga-client"
$apiUrl = "http://localhost:$ApiPort"
$frontendUrl = "http://localhost:$FrontendPort"

Write-Host "Starting chatbot dev stack..." -ForegroundColor Cyan
Write-Host "  API:      $apiUrl" -ForegroundColor White
Write-Host "  Frontend: $frontendUrl" -ForegroundColor White

if (-not $SkipBuild) {
    Write-Host "  Building chatbot API..." -ForegroundColor Yellow
    Push-Location $repoRoot
    dotnet build $apiProject -c Debug | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Host "  Build failed." -ForegroundColor Red
        exit 1
    }
    Pop-Location
}

$apiJob = Start-Job -ScriptBlock {
    Set-Location $using:repoRoot
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = $using:apiUrl
    dotnet run --project $using:apiProject --no-build --no-launch-profile 2>&1
}
Write-Host "  Chatbot API starting (Job $($apiJob.Id))..." -ForegroundColor Green

$frontendJob = Start-Job -ScriptBlock {
    Set-Location $using:frontendDir
    $env:VITE_GA_API_URL = $using:apiUrl
    # `dev` is intentionally sabotaged with process.exit(1) — ga-client is
    # the legacy demo client; the `:legacy` alias runs vite without the
    # port-5176 guard. We pass --host/--port through so this script can
    # still drive the FrontendPort parameter.
    npm run dev:legacy -- --host --port $using:FrontendPort 2>&1
}
Write-Host "  Frontend starting (Job $($frontendJob.Id))..." -ForegroundColor Green

Start-Sleep 8
$apiHealthy = try { (Invoke-RestMethod "$apiUrl/api/chatbot/status").isAvailable } catch { $false }
if ($apiHealthy) {
    Write-Host "  Chatbot API: READY" -ForegroundColor Green
} else {
    Write-Host "  Chatbot API: Still starting..." -ForegroundColor Yellow
}

$frontendHealthy = try { (Invoke-WebRequest $frontendUrl -TimeoutSec 5).StatusCode -eq 200 } catch { $false }
if ($frontendHealthy) {
    Write-Host "  Frontend: READY" -ForegroundColor Green
} else {
    Write-Host "  Frontend: Still starting..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Dev stack running. Press Ctrl+C to stop both jobs." -ForegroundColor Cyan
Write-Host "  API status: $apiUrl/api/chatbot/status" -ForegroundColor White
Write-Host "  Frontend:   $frontendUrl" -ForegroundColor White
Write-Host ""

try {
    while ($true) {
        Start-Sleep 30

        if ($apiJob.State -ne "Running") {
            Write-Host "  [!] Chatbot API stopped, restarting..." -ForegroundColor Red
            $apiJob = Start-Job -ScriptBlock {
                Set-Location $using:repoRoot
                $env:ASPNETCORE_ENVIRONMENT = "Development"
                $env:ASPNETCORE_URLS = $using:apiUrl
                dotnet run --project $using:apiProject --no-build --no-launch-profile 2>&1
            }
        }

        if ($frontendJob.State -ne "Running") {
            Write-Host "  [!] Frontend stopped, restarting..." -ForegroundColor Red
            $frontendJob = Start-Job -ScriptBlock {
                Set-Location $using:frontendDir
                $env:VITE_GA_API_URL = $using:apiUrl
                # `dev` is intentionally sabotaged with process.exit(1) — ga-client is
    # the legacy demo client; the `:legacy` alias runs vite without the
    # port-5176 guard. We pass --host/--port through so this script can
    # still drive the FrontendPort parameter.
    npm run dev:legacy -- --host --port $using:FrontendPort 2>&1
            }
        }
    }
}
finally {
    Write-Host "Stopping chatbot dev stack..." -ForegroundColor Yellow
    Stop-Job $apiJob -ErrorAction SilentlyContinue
    Stop-Job $frontendJob -ErrorAction SilentlyContinue
    Remove-Job $apiJob -ErrorAction SilentlyContinue
    Remove-Job $frontendJob -ErrorAction SilentlyContinue
}
