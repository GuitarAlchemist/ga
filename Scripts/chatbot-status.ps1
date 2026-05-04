#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check local thin-chatbot API and optional frontend availability.
.PARAMETER ApiUrl
    Base URL of the chatbot API.
.PARAMETER FrontendUrl
    Optional frontend URL to test.
#>

param(
    [string]$ApiUrl = "http://localhost:5252",
    [string]$FrontendUrl = "http://localhost:5173"
)

$ErrorActionPreference = "Continue"

Write-Host "Chatbot status" -ForegroundColor Cyan
Write-Host "  API:      $ApiUrl" -ForegroundColor White
Write-Host "  Frontend: $FrontendUrl" -ForegroundColor White
Write-Host ""

$apiStatus = try { Invoke-RestMethod "$ApiUrl/api/chatbot/status" } catch { $null }
if ($apiStatus) {
    $availability = if ($apiStatus.isAvailable) { "AVAILABLE" } else { "UNAVAILABLE" }
    Write-Host "API: $availability" -ForegroundColor $(if ($apiStatus.isAvailable) { "Green" } else { "Yellow" })
    Write-Host "  Message: $($apiStatus.message)" -ForegroundColor Gray
} else {
    Write-Host "API: DOWN" -ForegroundColor Red
}

$frontendStatus = try { (Invoke-WebRequest $FrontendUrl -TimeoutSec 5).StatusCode } catch { $null }
if ($frontendStatus -eq 200) {
    Write-Host "Frontend: UP" -ForegroundColor Green
} elseif ($frontendStatus) {
    Write-Host "Frontend: HTTP $frontendStatus" -ForegroundColor Yellow
} else {
    Write-Host "Frontend: DOWN" -ForegroundColor Red
}
