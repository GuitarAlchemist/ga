#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Monitor all Guitar Alchemist services
.DESCRIPTION
    Displays status of all running services with health checks and URLs
#>

param(
    [switch]$Watch,
    [int]$RefreshSeconds = 5
)

function Test-ServiceHealth
{
    param([string]$Url)

    try
    {
        $response = Invoke-WebRequest -Uri $Url -TimeoutSec 2 -UseBasicParsing -ErrorAction Stop
        return @{
            Status = "✅ Healthy"
            StatusCode = $response.StatusCode
            ResponseTime = $response.Headers['X-Response-Time']
        }
    }
    catch
    {
        return @{
            Status = "❌ Unhealthy"
            StatusCode = $_.Exception.Message
            ResponseTime = "N/A"
        }
    }
}

function Get-ServiceStatus
{
    Write-Host "`n╔════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║          Guitar Alchemist - Service Monitor                       ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host "Last Updated: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )`n" -ForegroundColor Gray

    # Docker Services
    Write-Host "━━━ Docker Services ━━━" -ForegroundColor Yellow
    $dockerServices = docker-compose ps --format json 2> $null | ConvertFrom-Json
    if ($dockerServices)
    {
        foreach ($service in $dockerServices)
        {
            $status = if ($service.State -eq "running")
            {
                "✅"
            }
            else
            {
                "❌"
            }
            Write-Host "  $status $($service.Service.PadRight(20) ) | $($service.State.PadRight(10) ) | Ports: $( $service.Publishers.PublishedPort -join ', ' )" -ForegroundColor $( if ($service.State -eq "running")
            {
                "Green"
            }
            else
            {
                "Red"
            } )
        }
    }
    else
    {
        Write-Host "  ⚠️  No Docker services running" -ForegroundColor Yellow
    }

    # .NET Services
    Write-Host "`n━━━ .NET Services ━━━" -ForegroundColor Yellow

    # GaApi
    $gaApiHealth = Test-ServiceHealth -Url "http://localhost:5232/health"
    Write-Host "  $( $gaApiHealth.Status ) GaApi (Backend)      | http://localhost:5232" -ForegroundColor $( if ($gaApiHealth.Status -like "*Healthy")
    {
        "Green"
    }
    else
    {
        "Red"
    } )
    if ($gaApiHealth.ResponseTime)
    {
        Write-Host "     └─ Response Time: $( $gaApiHealth.ResponseTime )" -ForegroundColor Gray
    }
    Write-Host "     └─ Swagger: http://localhost:5232/swagger" -ForegroundColor Gray

    # React Frontend
    Write-Host "`n━━━ Frontend Services ━━━" -ForegroundColor Yellow
    try
    {
        $reactResponse = Invoke-WebRequest -Uri "http://localhost:5173" -TimeoutSec 2 -UseBasicParsing -ErrorAction Stop
        Write-Host "  ✅ Healthy React Frontend    | http://localhost:5173" -ForegroundColor Green
    }
    catch
    {
        Write-Host "  ❌ Unhealthy React Frontend  | http://localhost:5173" -ForegroundColor Red
    }

    # Quick Links
    Write-Host "`n━━━ Quick Links ━━━" -ForegroundColor Yellow
    Write-Host "  🌐 React App:        http://localhost:5173" -ForegroundColor Cyan
    Write-Host "  📚 API Docs:         http://localhost:5232/swagger" -ForegroundColor Cyan
    Write-Host "  🗄️  MongoDB UI:       http://localhost:8081" -ForegroundColor Cyan
    Write-Host "  ❤️  Health Check:    http://localhost:5232/health" -ForegroundColor Cyan

    # API Endpoints
    Write-Host "`n━━━ New Modulation API ━━━" -ForegroundColor Yellow
    Write-Host "  GET /api/contextual-chords/modulation?sourceKey=C%20Major&targetKey=G%20Major" -ForegroundColor Gray
    Write-Host "  GET /api/contextual-chords/modulation/common?sourceKey=C%20Major" -ForegroundColor Gray

    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan
}

# Main execution
if ($Watch)
{
    Write-Host "Watching services (refresh every $RefreshSeconds seconds). Press Ctrl+C to stop..." -ForegroundColor Yellow
    while ($true)
    {
        Clear-Host
        Get-ServiceStatus
        Start-Sleep -Seconds $RefreshSeconds
    }
}
else
{
    Get-ServiceStatus
}

