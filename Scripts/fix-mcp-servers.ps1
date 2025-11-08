#!/usr/bin/env pwsh
# Fix MCP Server Issues

Write-Host "🔧 Fixing MCP Server Issues..." -ForegroundColor Cyan

$configPath = "$env:USERPROFILE\.codex\config.toml"

if (-not (Test-Path $configPath))
{
    Write-Host "❌ Config file not found at: $configPath" -ForegroundColor Red
    exit 1
}

Write-Host "`n📋 Diagnosing MCP Server Issues..." -ForegroundColor Yellow

# Check MongoDB
Write-Host "`n1️⃣ Checking MongoDB..." -ForegroundColor Cyan
try
{
    $mongoProcess = Get-Process -Name mongod -ErrorAction SilentlyContinue
    if ($mongoProcess)
    {
        Write-Host "✅ MongoDB is running (PID: $( $mongoProcess.Id ))" -ForegroundColor Green
    }
    else
    {
        Write-Host "❌ MongoDB is NOT running" -ForegroundColor Red
        Write-Host "   Start MongoDB with: mongod --dbpath C:\data\db" -ForegroundColor Yellow
    }
}
catch
{
    Write-Host "❌ MongoDB check failed: $_" -ForegroundColor Red
}

# Check Redis
Write-Host "`n2️⃣ Checking Redis..." -ForegroundColor Cyan
try
{
    $redisProcess = Get-Process -Name redis-server -ErrorAction SilentlyContinue
    if ($redisProcess)
    {
        Write-Host "✅ Redis is running (PID: $( $redisProcess.Id ))" -ForegroundColor Green
    }
    else
    {
        Write-Host "❌ Redis is NOT running" -ForegroundColor Red
        Write-Host "   Install Redis: https://redis.io/download" -ForegroundColor Yellow
        Write-Host "   Or use WSL: wsl redis-server" -ForegroundColor Yellow
    }
}
catch
{
    Write-Host "❌ Redis check failed: $_" -ForegroundColor Red
}

# Check HTTP servers
Write-Host "`n3️⃣ Checking HTTP MCP Servers..." -ForegroundColor Cyan

$httpServers = @(
    @{ Name = "tars-default"; Port = 8999 },
    @{ Name = "augment-local"; Port = 9000 }
)

foreach ($server in $httpServers)
{
    try
    {
        $response = Invoke-WebRequest -Uri "http://localhost:$( $server.Port )" -TimeoutSec 2 -ErrorAction Stop
        Write-Host "✅ $( $server.Name ) is running on port $( $server.Port )" -ForegroundColor Green
    }
    catch
    {
        Write-Host "❌ $( $server.Name ) is NOT running on port $( $server.Port )" -ForegroundColor Red
    }
}

# Check Python/uvx for sequential_thinking and blender
Write-Host "`n4️⃣ Checking Python/uvx..." -ForegroundColor Cyan
try
{
    $uvxVersion = uvx --version 2>&1
    Write-Host "✅ uvx is installed: $uvxVersion" -ForegroundColor Green
}
catch
{
    Write-Host "❌ uvx is NOT installed" -ForegroundColor Red
    Write-Host "   Install with: pip install uvx" -ForegroundColor Yellow
}

# Backup config
Write-Host "`n💾 Creating backup..." -ForegroundColor Cyan
$backupPath = "$configPath.backup-$( Get-Date -Format 'yyyyMMdd-HHmmss' )"
Copy-Item $configPath $backupPath
Write-Host "✅ Backup created: $backupPath" -ForegroundColor Green

# Read config
$config = Get-Content $configPath -Raw

# Fix 1: Increase Blender timeout
Write-Host "`n🔧 Fix 1: Adding Blender startup timeout..." -ForegroundColor Cyan
if ($config -match '\[mcp_servers\.blender\]')
{
    if ($config -notmatch 'startup_timeout_sec\s*=')
    {
        $config = $config -replace '(\[mcp_servers\.blender\])', "`$1`nstartup_timeout_sec = 30"
        Write-Host "✅ Added startup_timeout_sec = 30 to blender" -ForegroundColor Green
    }
    else
    {
        Write-Host "⏭️  Blender timeout already configured" -ForegroundColor Yellow
    }
}

# Fix 2: Comment out failing servers
Write-Host "`n🔧 Fix 2: Commenting out servers that require external services..." -ForegroundColor Cyan

$serversToComment = @(
    'mongodb', # Requires MongoDB running
    'redis', # Requires Redis running
    'tars-default', # Requires TARS server
    'augment-local', # Requires Augment local server
    'tars_mcp'      # Requires TARS MCP server
)

foreach ($serverName in $serversToComment)
{
    $pattern = "(\[mcp_servers\.$serverName\](?:(?!\[mcp_servers\.).)*)"
    if ($config -match $pattern)
    {
        $serverBlock = $Matches[1]
        if ($serverBlock -notmatch '^#')
        {
            $commentedBlock = ($serverBlock -split "`n" | ForEach-Object { "# $_" }) -join "`n"
            $config = $config -replace [regex]::Escape($serverBlock), $commentedBlock
            Write-Host "✅ Commented out: $serverName" -ForegroundColor Green
        }
        else
        {
            Write-Host "⏭️  Already commented: $serverName" -ForegroundColor Yellow
        }
    }
}

# Fix 3: Comment out sequential_thinking if uvx not available
Write-Host "`n🔧 Fix 3: Checking sequential_thinking..." -ForegroundColor Cyan
try
{
    $null = uvx --version 2>&1
}
catch
{
    $pattern = "(\[mcp_servers\.sequential_thinking\](?:(?!\[mcp_servers\.).)*)"
    if ($config -match $pattern)
    {
        $serverBlock = $Matches[1]
        if ($serverBlock -notmatch '^#')
        {
            $commentedBlock = ($serverBlock -split "`n" | ForEach-Object { "# $_" }) -join "`n"
            $config = $config -replace [regex]::Escape($serverBlock), $commentedBlock
            Write-Host "✅ Commented out: sequential_thinking (uvx not available)" -ForegroundColor Green
        }
    }
}

# Save fixed config
Write-Host "`n💾 Saving fixed configuration..." -ForegroundColor Cyan
$config | Set-Content $configPath -NoNewline
Write-Host "✅ Configuration saved" -ForegroundColor Green

# Summary
Write-Host "`n📊 Summary:" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host "✅ Blender timeout increased to 30 seconds" -ForegroundColor Green
Write-Host "✅ Commented out servers requiring external services:" -ForegroundColor Green
foreach ($server in $serversToComment)
{
    Write-Host "   - $server" -ForegroundColor Gray
}
Write-Host "`n📝 To enable these servers:" -ForegroundColor Yellow
Write-Host "   1. Start MongoDB: mongod --dbpath C:\data\db" -ForegroundColor Gray
Write-Host "   2. Start Redis: redis-server (or wsl redis-server)" -ForegroundColor Gray
Write-Host "   3. Uncomment the server in $configPath" -ForegroundColor Gray
Write-Host "`n🔄 Restart Codex/Augment to apply changes" -ForegroundColor Cyan

