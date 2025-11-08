#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Health check for all Guitar Alchemist services

.DESCRIPTION
    Verifies that all services are running and healthy:
    - GaApi (main API server)
    - GuitarAlchemistChatbot (Blazor chatbot)
    - ga-client (React frontend)
    - MongoDB (database)
    - MongoExpress (database UI)

.PARAMETER Timeout
    Timeout in seconds for each health check (default: 30)

.PARAMETER Verbose
    Show detailed output

.EXAMPLE
    .\health-check.ps1
    Check all services with default timeout

.EXAMPLE
    .\health-check.ps1 -Timeout 60 -Verbose
    Check all services with 60 second timeout and verbose output
#>

param(
    [int]$Timeout = 30,
    [switch]$Verbose
)

# Color functions
function Write-Header
{
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success
{
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Failure
{
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info
{
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor Yellow
}

function Write-Step
{
    param([string]$Message)
    Write-Host "`n▶ $Message" -ForegroundColor Blue
}

# Health check results
$script:HealthResults = @{
    GaApi = @{ Healthy = $false; ResponseTime = 0; Message = "" }
    Chatbot = @{ Healthy = $false; ResponseTime = 0; Message = "" }
    ReactFrontend = @{ Healthy = $false; ResponseTime = 0; Message = "" }
    MongoDB = @{ Healthy = $false; ResponseTime = 0; Message = "" }
    MongoExpress = @{ Healthy = $false; ResponseTime = 0; Message = "" }
    AspireDashboard = @{ Healthy = $false; ResponseTime = 0; Message = "" }
}

# Service URLs (default ports)
$ServiceUrls = @{
    GaApi = "https://localhost:7001/health"
    GaApiSwagger = "https://localhost:7001/swagger"
    Chatbot = "https://localhost:7002"
    ReactFrontend = "http://localhost:5173"
    MongoDB = "mongodb://localhost:27017"
    MongoExpress = "http://localhost:8081"
    AspireDashboard = "https://localhost:15001"
}

Write-Header "Guitar Alchemist - Health Check"
Write-Info "Timeout: $Timeout seconds"
Write-Info "Started: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )"

# ============================================
# CHECK GAAPI
# ============================================
Write-Step "Checking GaApi..."

try
{
    $startTime = Get-Date
    $response = Invoke-WebRequest -Uri $ServiceUrls.GaApi -Method Get -TimeoutSec $Timeout -SkipCertificateCheck -ErrorAction Stop
    $responseTime = ((Get-Date) - $startTime).TotalMilliseconds

    if ($response.StatusCode -eq 200)
    {
        $script:HealthResults.GaApi.Healthy = $true
        $script:HealthResults.GaApi.ResponseTime = $responseTime
        $script:HealthResults.GaApi.Message = "Healthy"
        Write-Success "GaApi is healthy (${responseTime}ms)"

        if ($Verbose)
        {
            Write-Info "Response: $( $response.Content )"
        }
    }
    else
    {
        $script:HealthResults.GaApi.Message = "Unexpected status code: $( $response.StatusCode )"
        Write-Failure "GaApi returned status code $( $response.StatusCode )"
    }
}
catch
{
    $script:HealthResults.GaApi.Message = $_.Exception.Message
    Write-Failure "GaApi is not accessible: $( $_.Exception.Message )"
    Write-Info "Make sure GaApi is running at $( $ServiceUrls.GaApi )"
}

# ============================================
# CHECK CHATBOT
# ============================================
Write-Step "Checking Chatbot..."

try
{
    $startTime = Get-Date
    $response = Invoke-WebRequest -Uri $ServiceUrls.Chatbot -Method Get -TimeoutSec $Timeout -SkipCertificateCheck -ErrorAction Stop
    $responseTime = ((Get-Date) - $startTime).TotalMilliseconds

    if ($response.StatusCode -eq 200)
    {
        $script:HealthResults.Chatbot.Healthy = $true
        $script:HealthResults.Chatbot.ResponseTime = $responseTime
        $script:HealthResults.Chatbot.Message = "Healthy"
        Write-Success "Chatbot is healthy (${responseTime}ms)"
    }
    else
    {
        $script:HealthResults.Chatbot.Message = "Unexpected status code: $( $response.StatusCode )"
        Write-Failure "Chatbot returned status code $( $response.StatusCode )"
    }
}
catch
{
    $script:HealthResults.Chatbot.Message = $_.Exception.Message
    Write-Failure "Chatbot is not accessible: $( $_.Exception.Message )"
    Write-Info "Make sure Chatbot is running at $( $ServiceUrls.Chatbot )"
}

# ============================================
# CHECK REACT FRONTEND
# ============================================
Write-Step "Checking React Frontend..."

try
{
    $startTime = Get-Date
    $response = Invoke-WebRequest -Uri $ServiceUrls.ReactFrontend -Method Get -TimeoutSec $Timeout -ErrorAction Stop
    $responseTime = ((Get-Date) - $startTime).TotalMilliseconds

    if ($response.StatusCode -eq 200)
    {
        $script:HealthResults.ReactFrontend.Healthy = $true
        $script:HealthResults.ReactFrontend.ResponseTime = $responseTime
        $script:HealthResults.ReactFrontend.Message = "Healthy"
        Write-Success "React Frontend is healthy (${responseTime}ms)"
    }
    else
    {
        $script:HealthResults.ReactFrontend.Message = "Unexpected status code: $( $response.StatusCode )"
        Write-Failure "React Frontend returned status code $( $response.StatusCode )"
    }
}
catch
{
    $script:HealthResults.ReactFrontend.Message = $_.Exception.Message
    Write-Failure "React Frontend is not accessible: $( $_.Exception.Message )"
    Write-Info "Make sure React Frontend is running at $( $ServiceUrls.ReactFrontend )"
}

# ============================================
# CHECK MONGODB
# ============================================
Write-Step "Checking MongoDB..."

try
{
    # Try to connect using mongosh or mongo CLI
    $mongoTest = docker exec -it $( docker ps -q -f "ancestor=mongo" ) mongosh --eval "db.adminCommand('ping')" 2>&1

    if ($LASTEXITCODE -eq 0)
    {
        $script:HealthResults.MongoDB.Healthy = $true
        $script:HealthResults.MongoDB.Message = "Healthy"
        Write-Success "MongoDB is healthy"
    }
    else
    {
        $script:HealthResults.MongoDB.Message = "MongoDB ping failed"
        Write-Failure "MongoDB is not responding"
    }
}
catch
{
    # Fallback: Check if MongoDB container is running
    $mongoContainer = docker ps -q -f "ancestor=mongo" 2> $null

    if ($mongoContainer)
    {
        $script:HealthResults.MongoDB.Healthy = $true
        $script:HealthResults.MongoDB.Message = "Container running"
        Write-Success "MongoDB container is running"
    }
    else
    {
        $script:HealthResults.MongoDB.Message = "Container not found"
        Write-Failure "MongoDB container is not running"
        Write-Info "Start services with: .\Scripts\start-all.ps1"
    }
}

# ============================================
# CHECK MONGOEXPRESS
# ============================================
Write-Step "Checking MongoExpress..."

try
{
    $startTime = Get-Date
    $response = Invoke-WebRequest -Uri $ServiceUrls.MongoExpress -Method Get -TimeoutSec $Timeout -ErrorAction Stop
    $responseTime = ((Get-Date) - $startTime).TotalMilliseconds

    if ($response.StatusCode -eq 200)
    {
        $script:HealthResults.MongoExpress.Healthy = $true
        $script:HealthResults.MongoExpress.ResponseTime = $responseTime
        $script:HealthResults.MongoExpress.Message = "Healthy"
        Write-Success "MongoExpress is healthy (${responseTime}ms)"
    }
    else
    {
        $script:HealthResults.MongoExpress.Message = "Unexpected status code: $( $response.StatusCode )"
        Write-Failure "MongoExpress returned status code $( $response.StatusCode )"
    }
}
catch
{
    $script:HealthResults.MongoExpress.Message = $_.Exception.Message
    Write-Failure "MongoExpress is not accessible: $( $_.Exception.Message )"
    Write-Info "MongoExpress may not be running"
}

# ============================================
# CHECK ASPIRE DASHBOARD
# ============================================
Write-Step "Checking Aspire Dashboard..."

try
{
    $startTime = Get-Date
    $response = Invoke-WebRequest -Uri $ServiceUrls.AspireDashboard -Method Get -TimeoutSec $Timeout -SkipCertificateCheck -ErrorAction Stop
    $responseTime = ((Get-Date) - $startTime).TotalMilliseconds

    if ($response.StatusCode -eq 200)
    {
        $script:HealthResults.AspireDashboard.Healthy = $true
        $script:HealthResults.AspireDashboard.ResponseTime = $responseTime
        $script:HealthResults.AspireDashboard.Message = "Healthy"
        Write-Success "Aspire Dashboard is healthy (${responseTime}ms)"
    }
    else
    {
        $script:HealthResults.AspireDashboard.Message = "Unexpected status code: $( $response.StatusCode )"
        Write-Failure "Aspire Dashboard returned status code $( $response.StatusCode )"
    }
}
catch
{
    $script:HealthResults.AspireDashboard.Message = $_.Exception.Message
    Write-Failure "Aspire Dashboard is not accessible: $( $_.Exception.Message )"
    Write-Info "Make sure Aspire AppHost is running"
}

# ============================================
# SUMMARY
# ============================================
Write-Header "Health Check Summary"

$healthyCount = ($script:HealthResults.Values | Where-Object { $_.Healthy }).Count
$totalCount = $script:HealthResults.Count

Write-Host "Service Status:" -ForegroundColor Cyan
Write-Host ""

foreach ($service in $script:HealthResults.Keys | Sort-Object)
{
    $result = $script:HealthResults[$service]
    $status = if ($result.Healthy)
    {
        "✓ HEALTHY"
    }
    else
    {
        "✗ UNHEALTHY"
    }
    $color = if ($result.Healthy)
    {
        "Green"
    }
    else
    {
        "Red"
    }

    Write-Host "  $service" -NoNewline -ForegroundColor White
    Write-Host " → " -NoNewline -ForegroundColor DarkGray
    Write-Host $status -ForegroundColor $color

    if ($result.ResponseTime -gt 0)
    {
        Write-Host "    Response Time: $($result.ResponseTime.ToString('F0') )ms" -ForegroundColor DarkGray
    }

    if ($Verbose -and $result.Message)
    {
        Write-Host "    Message: $( $result.Message )" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "----------------------------------------" -ForegroundColor Cyan
Write-Host "Total: $healthyCount / $totalCount services healthy" -ForegroundColor $( if ($healthyCount -eq $totalCount)
{
    "Green"
}
else
{
    "Yellow"
} )
Write-Host "Completed: $( Get-Date -Format 'yyyy-MM-dd HH:mm:ss' )" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Service URLs
if ($healthyCount -gt 0)
{
    Write-Info "Service URLs:"
    if ($script:HealthResults.AspireDashboard.Healthy)
    {
        Write-Host "  Aspire Dashboard: $( $ServiceUrls.AspireDashboard )" -ForegroundColor Cyan
    }
    if ($script:HealthResults.GaApi.Healthy)
    {
        Write-Host "  GaApi (Swagger): $( $ServiceUrls.GaApiSwagger )" -ForegroundColor Cyan
    }
    if ($script:HealthResults.Chatbot.Healthy)
    {
        Write-Host "  Chatbot: $( $ServiceUrls.Chatbot )" -ForegroundColor Cyan
    }
    if ($script:HealthResults.ReactFrontend.Healthy)
    {
        Write-Host "  React Frontend: $( $ServiceUrls.ReactFrontend )" -ForegroundColor Cyan
    }
    if ($script:HealthResults.MongoExpress.Healthy)
    {
        Write-Host "  MongoExpress: $( $ServiceUrls.MongoExpress )" -ForegroundColor Cyan
    }
    Write-Host ""
}

# Exit with appropriate code
if ($healthyCount -eq $totalCount)
{
    Write-Success "All services are healthy!"
    exit 0
}
else
{
    Write-Failure "Some services are unhealthy. Start services with: .\Scripts\start-all.ps1"
    exit 1
}

