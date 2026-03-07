#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [switch]$GlobalClients = $true
)

$ErrorActionPreference = "Stop"

function Remove-Ansi([string]$text)
{
    return ($text -replace "`e\[[0-9;]*m", "")
}

function Get-EnabledServerNames
{
    $raw = docker mcp server ls 2>&1
    $names = @()
    foreach ($line in $raw)
    {
        $clean = Remove-Ansi $line
        if ($clean -match "^\s*([a-z0-9][a-z0-9._-]*)\s{2,}")
        {
            $name = $matches[1]
            if ($name -notin @("NAME", "MCP", "Tip"))
            {
                $names += $name
            }
        }
    }
    return $names | Sort-Object -Unique
}

Write-Host "Checking Docker MCP canonical profile: ga-mcp-core-v1" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor DarkGray

$requiredServers = @("mongodb", "redis", "context7", "memory", "playwright", "jetbrains")
$enabledServers = Get-EnabledServerNames
$missingServers = $requiredServers | Where-Object { $_ -notin $enabledServers }

Write-Host ""
Write-Host "Enabled servers:" -ForegroundColor Cyan
if ($enabledServers.Count -eq 0)
{
    Write-Host "  (none)" -ForegroundColor Yellow
}
else
{
    foreach ($server in $enabledServers)
    {
        $isRequired = $server -in $requiredServers
        $prefix = if ($isRequired) { "  [required]" } else { "  [extra]   " }
        Write-Host "$prefix $server" -ForegroundColor Gray
    }
}

Write-Host ""
if ($missingServers.Count -eq 0)
{
    Write-Host "Required server set: OK" -ForegroundColor Green
}
else
{
    Write-Host "Required server set: MISSING" -ForegroundColor Yellow
    foreach ($server in $missingServers)
    {
        Write-Host "  - $server" -ForegroundColor Yellow
    }
}

$catalog = docker mcp catalog show docker-mcp 2>&1
$meshyInCatalog = ($catalog | Where-Object { (Remove-Ansi $_) -match "\bmeshy-ai\b" }).Count -gt 0

Write-Host ""
if ($meshyInCatalog)
{
    Write-Host "Meshy catalog status: available in Docker MCP catalog" -ForegroundColor Green
}
else
{
    Write-Host "Meshy catalog status: NOT in Docker MCP catalog (use local/custom bridge)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Container health snapshot:" -ForegroundColor Cyan
$dockerPs = docker ps --format "{{.Names}}|{{.Status}}|{{.Ports}}" 2>$null
$interesting = @("ga-mongodb", "ga-falkordb", "ga-meshy-mcp")
foreach ($name in $interesting)
{
    $line = $dockerPs | Where-Object { $_ -like "$name|*" } | Select-Object -First 1
    if ($line)
    {
        $parts = $line -split "\|", 3
        Write-Host "  $($parts[0]): $($parts[1]) [$($parts[2])]" -ForegroundColor Gray
    }
    else
    {
        Write-Host "  ${name}: not running" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Port reachability:" -ForegroundColor Cyan
$mongoOk = Test-NetConnection -ComputerName localhost -Port 27017 -InformationLevel Quiet
$redisOk = Test-NetConnection -ComputerName localhost -Port 6379 -InformationLevel Quiet
Write-Host "  localhost:27017 (MongoDB): $mongoOk" -ForegroundColor Gray
Write-Host "  localhost:6379  (Redis):   $redisOk" -ForegroundColor Gray

$clientCmd = if ($GlobalClients) { "docker mcp client ls -g" } else { "docker mcp client ls" }
$clientRaw = Invoke-Expression $clientCmd 2>&1
$clientClean = $clientRaw | ForEach-Object { Remove-Ansi $_ }

$geminiStatus = ($clientClean | Where-Object { $_ -match "gemini:\s*(.+)$" } | Select-Object -First 1)
$codexStatus = ($clientClean | Where-Object { $_ -match "codex:\s*(.+)$" } | Select-Object -First 1)

Write-Host ""
Write-Host "Client connectivity snapshot ($($GlobalClients ? 'global' : 'project')):" -ForegroundColor Cyan
if ($geminiStatus)
{
    Write-Host "  $geminiStatus" -ForegroundColor Gray
}
if ($codexStatus)
{
    Write-Host "  $codexStatus" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Suggested remediation commands:" -ForegroundColor Cyan
if ($missingServers.Count -gt 0)
{
    Write-Host "  docker mcp server enable $($missingServers -join ' ')" -ForegroundColor Gray
}
if (-not $mongoOk)
{
    Write-Host "  Ensure MongoDB is running (container ga-mongodb or local mongod)" -ForegroundColor Gray
}
if (-not $redisOk)
{
    Write-Host "  Ensure Redis is running (container ga-falkordb or local redis)" -ForegroundColor Gray
}
if (-not $meshyInCatalog)
{
    Write-Host "  Use mcp-servers/meshy-ai as local/custom MCP bridge until catalog support exists" -ForegroundColor Gray
}

if ($missingServers.Count -gt 0 -or -not $mongoOk -or -not $redisOk)
{
    exit 1
}
