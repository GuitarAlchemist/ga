# Install Meshy AI MCP Server to JetBrains IDEs
# This script copies the MCP server configuration to Rider and WebStorm

param(
    [switch]$Rider,
    [switch]$WebStorm,
    [switch]$All
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  JetBrains MCP Server Installation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RiderConfigSource = Join-Path $ScriptDir "jetbrains-rider-mcp-config.xml"
$WebStormConfigSource = Join-Path $ScriptDir "jetbrains-webstorm-mcp-config.xml"

# JetBrains config paths
$AppData = $env:APPDATA
$RiderConfigPath = "$AppData\JetBrains\Rider2025.2\options\llm.mcpServers.xml"
$WebStormConfigPath = "$AppData\JetBrains\WebStorm2025.2\options\McpToolsStoreService.xml"

# Function to install MCP config
function Install-McpConfig {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$IdeName
    )

    Write-Host "Installing MCP config for $IdeName..." -ForegroundColor Yellow

    # Check if source file exists
    if (-not (Test-Path $Source)) {
        Write-Host "  [ERROR] Source file not found: $Source" -ForegroundColor Red
        return $false
    }

    # Create directory if it doesn't exist
    $DestDir = Split-Path -Parent $Destination
    if (-not (Test-Path $DestDir)) {
        Write-Host "  [WARNING] $IdeName not found at: $DestDir" -ForegroundColor Yellow
        Write-Host "  [INFO] Skipping $IdeName installation" -ForegroundColor Gray
        return $false
    }

    # Backup existing config if it exists
    if (Test-Path $Destination) {
        $BackupPath = "$Destination.backup"
        Copy-Item $Destination $BackupPath -Force
        Write-Host "  [OK] Backed up existing config to: $BackupPath" -ForegroundColor Green
    }

    # Copy new config
    try {
        Copy-Item $Source $Destination -Force
        Write-Host "  [OK] MCP config installed successfully" -ForegroundColor Green
        Write-Host "  [INFO] Location: $Destination" -ForegroundColor Gray
        return $true
    } catch {
        Write-Host "  [ERROR] Failed to copy config: $_" -ForegroundColor Red
        return $false
    }
}

# Determine which IDEs to install to
$InstallRider = $Rider -or $All
$InstallWebStorm = $WebStorm -or $All

# If no flags specified, ask user
if (-not $InstallRider -and -not $InstallWebStorm) {
    Write-Host "Which JetBrains IDE(s) do you want to configure?" -ForegroundColor Yellow
    Write-Host "  1. Rider only" -ForegroundColor White
    Write-Host "  2. WebStorm only" -ForegroundColor White
    Write-Host "  3. Both Rider and WebStorm" -ForegroundColor White
    Write-Host ""
    $choice = Read-Host "Enter your choice (1-3)"

    switch ($choice) {
        "1" { $InstallRider = $true }
        "2" { $InstallWebStorm = $true }
        "3" { $InstallRider = $true; $InstallWebStorm = $true }
        default {
            Write-Host "[ERROR] Invalid choice. Exiting." -ForegroundColor Red
            exit 1
        }
    }
}

Write-Host ""

# Install to selected IDEs
$SuccessCount = 0
$TotalCount = 0

if ($InstallRider) {
    $TotalCount++
    if (Install-McpConfig -Source $RiderConfigSource -Destination $RiderConfigPath -IdeName "Rider 2025.2") {
        $SuccessCount++
    }
    Write-Host ""
}

if ($InstallWebStorm) {
    $TotalCount++
    if (Install-McpConfig -Source $WebStormConfigSource -Destination $WebStormConfigPath -IdeName "WebStorm 2025.2") {
        $SuccessCount++
    }
    Write-Host ""
}

# Update Augment Code settings
$AugmentSettingsPath = "$env:USERPROFILE\.augment\settings.json"
Write-Host "Updating Augment Code settings..." -ForegroundColor Yellow

# Desired MCP servers for Augment
$DesiredAugmentServers = @{
    "mongodb" = @{ command = "npx"; args = @("-y", "@modelcontextprotocol/server-mongodb", "mongodb://localhost:27017"); disabled = $false; autoApprove = @(); alwaysAllow = @() }
    "redis"   = @{ command = "npx"; args = @("-y", "redis-mcp-server", "--url", "redis://127.0.0.1:6379"); disabled = $false; autoApprove = @(); alwaysAllow = @() }
    "blender" = @{ command = "uvx"; args = @("blender-mcp"); disabled = $false; autoApprove = @(); alwaysAllow = @() }
    "meshy-ai" = @{ command = "python"; args = @("C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py"); env = @{ MESHY_API_KEY = "msy_ntI4R9Qk4x4c9v7BDvH6wJ7cwcyUUvMAMr0S" }; disabled = $false; autoApprove = @(); alwaysAllow = @() }
}

# Load existing settings or initialize
$AugmentSettings = $null
if (Test-Path $AugmentSettingsPath) {
    try {
        $raw = Get-Content $AugmentSettingsPath -Raw
        if ($raw.Trim().Length -gt 0) {
            $AugmentSettings = $raw | ConvertFrom-Json
        }
    } catch {
        Write-Host "  [WARN] Failed to parse existing Augment settings, reinitializing: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
if (-not $AugmentSettings) { $AugmentSettings = [pscustomobject]@{ mcpServers = @{} } }
if (-not $AugmentSettings.mcpServers) { $AugmentSettings | Add-Member -NotePropertyName mcpServers -NotePropertyValue @{} }

# Merge desired servers
foreach ($name in $DesiredAugmentServers.Keys) {
    $hasServer = $false
    if ($AugmentSettings.mcpServers -is [hashtable]) {
        $hasServer = $AugmentSettings.mcpServers.ContainsKey($name)
    } else {
        $hasServer = $AugmentSettings.mcpServers.PSObject.Properties.Name -contains $name
    }
    if (-not $hasServer) {
        $AugmentSettings.mcpServers | Add-Member -NotePropertyName $name -NotePropertyValue $DesiredAugmentServers[$name]
        Write-Host "  [OK] Added $name to Augment settings" -ForegroundColor Green
    } else {
        Write-Host "  [SKIP] $name already present" -ForegroundColor Gray
    }
}

# Ensure directory exists
$AugmentDir = Split-Path -Parent $AugmentSettingsPath
if (-not (Test-Path $AugmentDir)) { New-Item -ItemType Directory -Path $AugmentDir -Force | Out-Null }

# Save settings
try {
    $AugmentSettings | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $AugmentSettingsPath
    Write-Host "  [INFO] Saved: $AugmentSettingsPath" -ForegroundColor Gray
} catch {
    Write-Host "  [ERROR] Failed to write Augment settings: $($_.Exception.Message)" -ForegroundColor Red
}

$CodexConfigPath = "$env:USERPROFILE\.codex\config.toml"

Write-Host "Updating Codex CLI settings..." -ForegroundColor Yellow
if (Test-Path $CodexConfigPath) {
    $codexContent = Get-Content $CodexConfigPath -Raw
    $originalCodexContent = $codexContent
    $codexServers = @(
        @{
            Name = "mongodb"
            Command = "npx"
            Args = @("-y", "@modelcontextprotocol/server-mongodb", "mongodb://localhost:27017")
            Env = @{}
        },
        @{
            Name = "redis"
            Command = "npx"
            Args = @("-y", "redis-mcp-server", "--url", "redis://127.0.0.1:6379")
            Env = @{}
        },
        @{
            Name = "blender"
            Command = "uvx"
            Args = @("blender-mcp")
            Env = @{}
        },
        @{
            Name = "meshy-ai"
            Command = "python"
            Args = @("C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py")
            Env = @{ MESHY_API_KEY = '${MESHY_API_KEY}' }
        }
    )

    foreach ($server in $codexServers) {
        $header = if ($server.Name -match "-") { "[mcp_servers.`"$($server.Name)`"]" } else { "[mcp_servers.$($server.Name)]" }
        if ($codexContent -notmatch [regex]::Escape($header)) {
            if (-not $codexContent.EndsWith("`n")) {
                $codexContent += "`n"
            }
            $argsList = $server.Args | ForEach-Object { "`"$($_)`"" } -join ", "
            $envString = if ($server.Env.Count -gt 0) {
                $pairs = @()
                foreach ($key in $server.Env.Keys) {
                    $pairs += "$key = `"$($server.Env[$key])`""
                }
                "{ " + ($pairs -join ", ") + " }"
            } else {
                "{}"
            }

            $codexContent += "`n$header`n"
            $codexContent += "command = `"$($server.Command)`"`n"
            $codexContent += "args = [$argsList]`n"
            $codexContent += "env = $envString`n"
        }
    }

    if ($codexContent -ne $originalCodexContent) {
        $codexContent | Set-Content -Path $CodexConfigPath -Encoding UTF8
        Write-Host "  [OK] Codex MCP entries updated" -ForegroundColor Green
    } else {
        Write-Host "  [SKIP] Codex already configured" -ForegroundColor Gray
    }
} else {
    Write-Host "  [WARN] Codex config not found at $CodexConfigPath" -ForegroundColor Yellow
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Installation Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installed: $SuccessCount / $TotalCount" -ForegroundColor $(if ($SuccessCount -eq $TotalCount) { "Green" } else { "Yellow" })
Write-Host ""

if ($SuccessCount -gt 0) {
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1. Restart your JetBrains IDE(s)" -ForegroundColor White
    Write-Host "2. The Meshy AI MCP server should now be available" -ForegroundColor White
    Write-Host "3. Test by asking: 'Using Meshy AI, create a golden Egyptian ankh'" -ForegroundColor White
    Write-Host ""
    Write-Host "MCP Servers Installed:" -ForegroundColor Yellow
    Write-Host "  1. meshy-ai (Python)" -ForegroundColor Gray
    Write-Host "     - Command: python" -ForegroundColor Gray
    Write-Host "     - Script: C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py" -ForegroundColor Gray
    Write-Host "     - API Key: msy_ntI4R9Qk4x4c9v7BDvH6wJ7cwcyUUvMAMr0S" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. mongodb (NPX)" -ForegroundColor Gray
    Write-Host "     - Command: npx -y @modelcontextprotocol/server-mongodb mongodb://localhost:27017" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. redis (NPX)" -ForegroundColor Gray
    Write-Host "     - Command: npx -y redis-mcp-server --url redis://127.0.0.1:6379" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  4. blender (UVX)" -ForegroundColor Gray
    Write-Host "     - Command: uvx blender-mcp" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Configuration Files:" -ForegroundColor Yellow
    if ($InstallRider) {
        Write-Host "  Rider: $RiderConfigPath" -ForegroundColor Gray
    }
    if ($InstallWebStorm) {
        Write-Host "  WebStorm: $WebStormConfigPath" -ForegroundColor Gray
    }
    Write-Host ""
}

Write-Host "Installation complete! 🎉" -ForegroundColor Green
Write-Host ""
