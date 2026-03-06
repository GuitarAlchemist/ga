#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [switch]$ConnectGemini,
    [switch]$ConnectCodex
)

$ErrorActionPreference = "Stop"

Write-Host "Applying Docker MCP canonical profile: ga-mcp-core-v1" -ForegroundColor Cyan

$requiredServers = @("mongodb", "redis", "context7", "memory", "playwright", "jetbrains")

Write-Host "Enabling required servers: $($requiredServers -join ', ')" -ForegroundColor Gray
docker mcp server enable @requiredServers | Out-Host

Write-Host ""
docker mcp server ls | Out-Host

if ($ConnectGemini)
{
    Write-Host ""
    Write-Host "Connecting gemini client globally..." -ForegroundColor Gray
    docker mcp client connect gemini -g -q | Out-Host
}

if ($ConnectCodex)
{
    Write-Host ""
    Write-Host "Connecting codex client globally..." -ForegroundColor Gray
    docker mcp client connect codex -g -q | Out-Host
}

Write-Host ""
Write-Host "Next recommended steps:" -ForegroundColor Cyan
Write-Host "  docker mcp secret set MDB_MCP_CONNECTION_STRING=mongodb://host.docker.internal:27017" -ForegroundColor Gray
Write-Host "  docker mcp secret set REDIS_HOST=host.docker.internal" -ForegroundColor Gray
Write-Host "  docker mcp secret set REDIS_PORT=6379" -ForegroundColor Gray
Write-Host "  pwsh Scripts/check-docker-mcp-profile.ps1" -ForegroundColor Gray
