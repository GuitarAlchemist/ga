# Verify MCP Server Registrations Across All Platforms
# This script checks if MCP servers are properly registered in WebStorm, Rider, Augment, and Codex CLI

param(
    [switch]$Detailed
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MCP Registration Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Expected MCP servers
$ExpectedServers = @("mongodb", "redis", "blender", "meshy-ai")

# Configuration paths
$RiderConfigPath = "$env:APPDATA\JetBrains\Rider2025.2\options\llm.mcpServers.xml"
$WebStormConfigPath = "$env:APPDATA\JetBrains\WebStorm2025.2\options\McpToolsStoreService.xml"
$AugmentConfigPath = "$env:USERPROFILE\.augment\settings.json"
$CodexConfigPath = "$env:USERPROFILE\.codex\config.toml"

function Test-McpServersInXml {
    param($FilePath, $ComponentName)

    if (-not (Test-Path $FilePath)) {
        return @{
            Status = "Missing"
            Servers = @()
            Message = "Configuration file not found"
        }
    }

    try {
        [xml]$config = Get-Content $FilePath
        $mcpServers = $config.application.component | Where-Object { $_.name -eq $ComponentName }

        if ($mcpServers -and $mcpServers.mcpServers.mcpServer) {
            $serverNames = @()
            foreach ($server in $mcpServers.mcpServers.mcpServer) {
                $serverNames += $server.option | Where-Object { $_.name -eq "name" } | Select-Object -ExpandProperty value
            }

            return @{
                Status = "Found"
                Servers = $serverNames
                Message = "Configuration loaded successfully"
            }
        } else {
            return @{
                Status = "Empty"
                Servers = @()
                Message = "No MCP servers configured"
            }
        }
    } catch {
        return @{
            Status = "Error"
            Servers = @()
            Message = "Failed to parse configuration: $($_.Exception.Message)"
        }
    }
}

function Test-McpServersInJson {
    param($FilePath)

    if (-not (Test-Path $FilePath)) {
        return @{
            Status = "Missing"
            Servers = @()
            Message = "Configuration file not found"
        }
    }

    try {
        $config = Get-Content $FilePath | ConvertFrom-Json

        if ($config.mcpServers) {
            $serverNames = $config.mcpServers.PSObject.Properties.Name

            return @{
                Status = "Found"
                Servers = $serverNames
                Message = "Configuration loaded successfully"
            }
        } else {
            return @{
                Status = "Empty"
                Servers = @()
                Message = "No MCP servers configured"
            }
        }
    } catch {
        return @{
            Status = "Error"
            Servers = @()
            Message = "Failed to parse configuration: $($_.Exception.Message)"
        }
    }
}

function Test-McpServersInToml {
    param($FilePath)

    if (-not (Test-Path $FilePath)) {
        return @{
            Status = "Missing"
            Servers = @()
            Message = "Configuration file not found"
        }
    }

    try {
        $content = Get-Content $FilePath -Raw
        $pattern = '^\s*\[mcp_servers\.(?:"([^"]+)"|([^\]]+))\]'
        $serverNames = @()

        foreach ($line in $content -split "`n") {
            if ($line -match $pattern) {
                $name = if ($matches[1]) { $matches[1] } else { $matches[2].Trim() }
                if ($name) { $serverNames += $name }
            }
        }

        if ($serverNames.Count -gt 0) {
            return @{
                Status = "Found"
                Servers = $serverNames
                Message = "Configuration loaded successfully"
            }
        } else {
            return @{
                Status = "Empty"
                Servers = @()
                Message = "No MCP servers configured"
            }
        }
    } catch {
        return @{
            Status = "Error"
            Servers = @()
            Message = "Failed to parse configuration: $($_.Exception.Message)"
        }
    }
}

function Show-ServerStatus {
    param(
        $Platform,
        $Result,
        $ExpectedServers,
        [bool]$ShowExtras = $true
    )

    Write-Host "[INFO] $Platform" -ForegroundColor Yellow
    Write-Host "   Status: " -NoNewline

    switch ($Result.Status) {
        "Found" {
            Write-Host "ACTIVE" -ForegroundColor Green

            $matching = $Result.Servers | Where-Object { $_ -in $ExpectedServers }
            if ($ShowExtras) {
                Write-Host "   Servers: $($Result.Servers.Count)/$($ExpectedServers.Count)" -ForegroundColor White
            } else {
                Write-Host "   Matching servers: $($matching.Count)/$($ExpectedServers.Count)" -ForegroundColor White
            }

            $missing = $ExpectedServers | Where-Object { $_ -notin $Result.Servers }
            $extra = $Result.Servers | Where-Object { $_ -notin $ExpectedServers }

            if ($missing.Count -eq 0 -and $extra.Count -eq 0) {
                Write-Host "   All servers present and synchronized" -ForegroundColor Green
            } else {
                if ($missing.Count -gt 0) {
                    Write-Host "   Missing: $($missing -join ', ')" -ForegroundColor Red
                }
                if ($ShowExtras -and $extra.Count -gt 0) {
                    Write-Host "   Extra: $($extra -join ', ')" -ForegroundColor Yellow
                }
            }

            if ($Detailed) {
                Write-Host "   Configured servers:" -ForegroundColor Gray
                foreach ($server in $Result.Servers) {
                    $status = if ($server -in $ExpectedServers) { "OK" } else { if ($ShowExtras) { "WARN" } else { "INFO" } }
                    Write-Host "     [$status] $server" -ForegroundColor Gray
                }
            }
        }
        "Missing" {
            Write-Host "NOT FOUND" -ForegroundColor Red
            Write-Host "   Message: $($Result.Message)" -ForegroundColor Gray
        }
        "Empty" {
            Write-Host "EMPTY" -ForegroundColor Yellow
            Write-Host "   Message: $($Result.Message)" -ForegroundColor Gray
        }
        "Error" {
            Write-Host "ERROR" -ForegroundColor Red
            Write-Host "   Message: $($Result.Message)" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

# Check each platform
Write-Host "Checking MCP server registrations..." -ForegroundColor White
Write-Host ""

# Rider
$RiderResult = Test-McpServersInXml -FilePath $RiderConfigPath -ComponentName "McpServersComponent"
Show-ServerStatus -Platform "Rider 2025.2" -Result $RiderResult -ExpectedServers $ExpectedServers

# WebStorm
$WebStormResult = Test-McpServersInXml -FilePath $WebStormConfigPath -ComponentName "McpToolsStoreService"
Show-ServerStatus -Platform "WebStorm 2025.2" -Result $WebStormResult -ExpectedServers $ExpectedServers

# Augment
$AugmentResult = Test-McpServersInJson -FilePath $AugmentConfigPath
Show-ServerStatus -Platform "Augment Code" -Result $AugmentResult -ExpectedServers $ExpectedServers

# Codex CLI
$CodexResult = Test-McpServersInToml -FilePath $CodexConfigPath
Show-ServerStatus -Platform "Codex CLI" -Result $CodexResult -ExpectedServers $ExpectedServers -ShowExtras:$false

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$AllResults = @($RiderResult, $WebStormResult, $AugmentResult, $CodexResult)
$ActivePlatforms = ($AllResults | Where-Object { $_.Status -eq "Found" }).Count
$TotalPlatforms = $AllResults.Count

Write-Host "Active Platforms: $ActivePlatforms/$TotalPlatforms" -ForegroundColor White

if ($ActivePlatforms -eq $TotalPlatforms) {
    $AllSynced = $true
    foreach ($result in $AllResults) {
        if ($result.Status -eq "Found") {
            $missing = $ExpectedServers | Where-Object { $_ -notin $result.Servers }
            if ($missing.Count -gt 0) {
                $AllSynced = $false
                break
            }
        } else {
            $AllSynced = $false
            break
        }
    }

    if ($AllSynced) {
        Write-Host "✅ All platforms are synchronized with all MCP servers!" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Platforms are active but not fully synchronized" -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ Some platforms are missing or have errors" -ForegroundColor Red
}

Write-Host ""
Write-Host "Expected servers: $($ExpectedServers -join ', ')" -ForegroundColor Gray
Write-Host ""

if ($ActivePlatforms -lt $TotalPlatforms -or -not $AllSynced) {
    Write-Host "To fix issues, run:" -ForegroundColor Yellow
    Write-Host "  .\install-jetbrains-mcp.ps1 -All" -ForegroundColor White
    Write-Host ""
}
