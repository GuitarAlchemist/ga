#!/usr/bin/env pwsh
# Start Redis Server in WSL

Write-Host "🚀 Starting Redis Server..." -ForegroundColor Cyan

# Check if WSL is available
try
{
    $wslStatus = wsl --status 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "❌ WSL is not available" -ForegroundColor Red
        exit 1
    }
}
catch
{
    Write-Host "❌ WSL is not installed" -ForegroundColor Red
    Write-Host "   Install WSL: wsl --install" -ForegroundColor Yellow
    exit 1
}

# Check if Redis is already running
try
{
    $pingResult = wsl bash -c "redis-cli ping 2>&1"
    if ($pingResult -eq "PONG")
    {
        Write-Host "✅ Redis is already running" -ForegroundColor Green

        # Test connection
        Write-Host "`n📊 Redis Info:" -ForegroundColor Cyan
        wsl bash -c "redis-cli INFO server | grep redis_version"
        wsl bash -c "redis-cli INFO clients | grep connected_clients"

        exit 0
    }
}
catch
{
    # Redis not running, continue to start it
}

# Start Redis
Write-Host "🔧 Starting Redis server in WSL..." -ForegroundColor Yellow
try
{
    wsl bash -c "redis-server --daemonize yes --bind 0.0.0.0 --protected-mode no" 2>&1 | Out-Null

    # Wait a moment for Redis to start
    Start-Sleep -Seconds 2

    # Verify it started
    $pingResult = wsl bash -c "redis-cli ping 2>&1"
    if ($pingResult -eq "PONG")
    {
        Write-Host "✅ Redis started successfully!" -ForegroundColor Green

        # Show info
        Write-Host "`n📊 Redis Info:" -ForegroundColor Cyan
        wsl bash -c "redis-cli INFO server | grep redis_version"
        wsl bash -c "redis-cli INFO clients | grep connected_clients"

        Write-Host "`n🌐 Redis is accessible at:" -ForegroundColor Cyan
        Write-Host "   - localhost:6379" -ForegroundColor Gray
        Write-Host "   - 127.0.0.1:6379" -ForegroundColor Gray

        Write-Host "`n💡 To stop Redis:" -ForegroundColor Yellow
        Write-Host "   wsl bash -c 'redis-cli shutdown'" -ForegroundColor Gray

    }
    else
    {
        Write-Host "❌ Redis failed to start" -ForegroundColor Red
        Write-Host "   Response: $pingResult" -ForegroundColor Gray
        exit 1
    }
}
catch
{
    Write-Host "❌ Error starting Redis: $_" -ForegroundColor Red
    exit 1
}

