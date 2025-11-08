#!/usr/bin/env pwsh
# Check MCP Server Status

Write-Host "🔍 Checking MCP Server Status..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray

# Check MongoDB
Write-Host "`n🍃 MongoDB:" -ForegroundColor Cyan
try
{
    $mongoProcess = Get-Process -Name mongod -ErrorAction SilentlyContinue
    if ($mongoProcess)
    {
        Write-Host "   ✅ Running (PID: $( $mongoProcess.Id ))" -ForegroundColor Green
        Write-Host "   📍 Port: 27017" -ForegroundColor Gray
    }
    else
    {
        Write-Host "   ❌ Not running" -ForegroundColor Red
    }
}
catch
{
    Write-Host "   ❌ Error: $_" -ForegroundColor Red
}

# Check Redis
Write-Host "`n🔴 Redis:" -ForegroundColor Cyan
try
{
    $pingResult = wsl bash -c "redis-cli ping 2>&1"
    if ($pingResult -eq "PONG")
    {
        Write-Host "   ✅ Running" -ForegroundColor Green
        $version = wsl bash -c "redis-cli INFO server | grep redis_version | cut -d: -f2"
        Write-Host "   📍 Port: 6379" -ForegroundColor Gray
        Write-Host "   📦 Version: $version" -ForegroundColor Gray
    }
    else
    {
        Write-Host "   ❌ Not running" -ForegroundColor Red
        Write-Host "   💡 Start with: pwsh Scripts/start-redis.ps1" -ForegroundColor Yellow
    }
}
catch
{
    Write-Host "   ❌ Error: $_" -ForegroundColor Red
}

# Check HTTP MCP Servers
Write-Host "`n🌐 HTTP MCP Servers:" -ForegroundColor Cyan

$httpServers = @(
    @{ Name = "tars-default"; Port = 8999; Status = "Commented" },
    @{ Name = "augment-local"; Port = 9000; Status = "Commented" }
)

foreach ($server in $httpServers)
{
    Write-Host "   $( $server.Name ) (port $( $server.Port )):" -ForegroundColor Gray
    try
    {
        $response = Invoke-WebRequest -Uri "http://localhost:$( $server.Port )" -TimeoutSec 2 -ErrorAction Stop
        Write-Host "      ✅ Running" -ForegroundColor Green
    }
    catch
    {
        Write-Host "      ⚠️  $( $server.Status )" -ForegroundColor Yellow
    }
}

# Check Config
Write-Host "`n📝 Config Status:" -ForegroundColor Cyan
$configPath = "$env:USERPROFILE\.codex\config.toml"
if (Test-Path $configPath)
{
    Write-Host "   ✅ Config found: $configPath" -ForegroundColor Green

    $config = Get-Content $configPath -Raw

    # Count enabled servers
    $enabledServers = @()
    if ($config -match '(?m)^\[mcp_servers\.mongodb\]')
    {
        $enabledServers += "mongodb"
    }
    if ($config -match '(?m)^\[mcp_servers\.redis\]')
    {
        $enabledServers += "redis"
    }
    if ($config -match '(?m)^\[mcp_servers\.blender\]')
    {
        $enabledServers += "blender"
    }
    if ($config -match '(?m)^\[mcp_servers\.sequential_thinking\]')
    {
        $enabledServers += "sequential_thinking"
    }
    if ($config -match '(?m)^\[mcp_servers\.tars_mcp\]')
    {
        $enabledServers += "tars_mcp"
    }

    Write-Host "   📊 Enabled MCP servers: $( $enabledServers.Count )" -ForegroundColor Gray
    foreach ($server in $enabledServers)
    {
        Write-Host "      - $server" -ForegroundColor Gray
    }
}
else
{
    Write-Host "   ❌ Config not found" -ForegroundColor Red
}

# Check TARS MCP
Write-Host "`n🤖 TARS MCP:" -ForegroundColor Cyan
if (Test-Path "C:/Users/spare/source/repos/tars/mcp-server/dist/index.js")
{
    Write-Host "   ✅ Built and ready" -ForegroundColor Green
    $size = (Get-Item "C:/Users/spare/source/repos/tars/mcp-server/dist/index.js").Length
    Write-Host "   📦 Size: $([math]::Round($size/1KB, 2) ) KB" -ForegroundColor Gray
}
else
{
    Write-Host "   ❌ Not built" -ForegroundColor Red
    Write-Host "   💡 Build with: cd C:/Users/spare/source/repos/tars/mcp-server && npm run build" -ForegroundColor Yellow
}

# Summary
Write-Host "`n📊 Summary:" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray

$mongoRunning = (Get-Process -Name mongod -ErrorAction SilentlyContinue) -ne $null
$redisRunning = (wsl bash -c "redis-cli ping 2>&1") -eq "PONG"

if ($mongoRunning -and $redisRunning)
{
    Write-Host "✅ All required services are running!" -ForegroundColor Green
    Write-Host "`n🔄 Next step: Restart Codex/Augment to apply MCP changes" -ForegroundColor Cyan
}
else
{
    Write-Host "⚠️  Some services are not running:" -ForegroundColor Yellow
    if (-not $mongoRunning)
    {
        Write-Host "   - MongoDB: Start with 'mongod --dbpath C:\data\db'" -ForegroundColor Gray
    }
    if (-not $redisRunning)
    {
        Write-Host "   - Redis: Start with 'pwsh Scripts/start-redis.ps1'" -ForegroundColor Gray
    }
}

