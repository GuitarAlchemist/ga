#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automatically download and install LSP Support plugin in JetBrains IDEs

.DESCRIPTION
    Downloads the LSP Support plugin from JetBrains Marketplace and installs it in all detected IDEs.
#>

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Automated LSP Support Plugin Installation" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Plugin details
$pluginId = "com.redhat.devtools.lsp4ij"
$pluginXmlId = "23257"  # LSP Support plugin ID on JetBrains Marketplace

# Get installed IDEs
function Get-InstalledIDEs {
    $ides = @()

    # Check for Rider
    $riderVersions = @("2023.3", "2024.1", "2024.2", "2024.3")
    foreach ($version in $riderVersions) {
        $path = Join-Path $env:APPDATA "JetBrains\Rider$version"
        if (Test-Path $path) {
            $ides += @{
                Name = "Rider"
                Version = $version
                Path = $path
                PluginsPath = Join-Path $path "plugins"
                ProductCode = "RD"
            }
        }
    }

    # Check for WebStorm
    $webstormVersions = @("2023.3", "2024.1", "2024.2", "2024.3")
    foreach ($version in $webstormVersions) {
        $path = Join-Path $env:APPDATA "JetBrains\WebStorm$version"
        if (Test-Path $path) {
            $ides += @{
                Name = "WebStorm"
                Version = $version
                Path = $path
                PluginsPath = Join-Path $path "plugins"
                ProductCode = "WS"
            }
        }
    }

    return $ides
}

# Download plugin from JetBrains Marketplace
function Download-Plugin {
    param(
        [string]$PluginId,
        [string]$OutputPath
    )

    Write-Host "Downloading LSP Support plugin from JetBrains Marketplace..." -ForegroundColor Yellow

    # Use build number for 2024.2 (compatible with most versions)
    $buildNumber = "242.20224.155"  # Rider/WebStorm 2024.2 build
    $downloadUrl = "https://plugins.jetbrains.com/pluginManager/?action=download&id=$PluginId&build=$buildNumber"

    try {
        Write-Host "  Download URL: $downloadUrl" -ForegroundColor Gray

        # Download the plugin
        Invoke-WebRequest -Uri $downloadUrl -OutFile $OutputPath -UseBasicParsing

        # Check if file was downloaded
        if (Test-Path $OutputPath) {
            $fileSize = (Get-Item $OutputPath).Length
            if ($fileSize -gt 1000) {  # At least 1KB
                Write-Host "  ✓ Downloaded: $([math]::Round($fileSize / 1MB, 2)) MB" -ForegroundColor Green
                return $true
            } else {
                Write-Host "  ✗ Download failed - file too small ($fileSize bytes)" -ForegroundColor Red
                return $false
            }
        } else {
            Write-Host "  ✗ Download failed - file not created" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "  ✗ Failed to download: $_" -ForegroundColor Red
        return $false
    }
}

# Install plugin in IDE
function Install-PluginInIDE {
    param(
        [hashtable]$IDE,
        [string]$PluginZipPath
    )

    $ideName = "$($IDE.Name) $($IDE.Version)"
    Write-Host "Installing in $ideName..." -ForegroundColor Yellow

    # Create plugins directory if it doesn't exist
    if (-not (Test-Path $IDE.PluginsPath)) {
        New-Item -ItemType Directory -Path $IDE.PluginsPath -Force | Out-Null
    }

    # Extract plugin
    $pluginDir = Join-Path $IDE.PluginsPath $pluginId

    # Remove old version if exists
    if (Test-Path $pluginDir) {
        Write-Host "  Removing old version..." -ForegroundColor Yellow
        Remove-Item -Path $pluginDir -Recurse -Force
    }

    try {
        # Extract the ZIP file
        Write-Host "  Extracting plugin..." -ForegroundColor Yellow
        Expand-Archive -Path $PluginZipPath -DestinationPath $IDE.PluginsPath -Force

        # The plugin might be in a subdirectory, find it
        $extractedDirs = Get-ChildItem -Path $IDE.PluginsPath -Directory | Where-Object { $_.Name -like "*lsp*" -or $_.Name -eq $pluginId }

        if ($extractedDirs) {
            $extractedDir = $extractedDirs | Select-Object -First 1

            # Rename to correct plugin ID if needed
            if ($extractedDir.Name -ne $pluginId) {
                Move-Item -Path $extractedDir.FullName -Destination $pluginDir -Force
            }

            Write-Host "  ✓ Installed successfully" -ForegroundColor Green
            return $true
        } else {
            Write-Host "  ✗ Plugin directory not found after extraction" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "  ✗ Installation failed: $_" -ForegroundColor Red
        return $false
    }
}

# Main execution
$installedIDEs = Get-InstalledIDEs

if ($installedIDEs.Count -eq 0) {
    Write-Host "ERROR: No JetBrains IDEs found" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($installedIDEs.Count) IDE installation(s):" -ForegroundColor Green
foreach ($ideInfo in $installedIDEs) {
    Write-Host "  - $($ideInfo.Name) $($ideInfo.Version)" -ForegroundColor White
}
Write-Host ""

# Create temp directory for download
$tempDir = Join-Path $env:TEMP "jetbrains-lsp-plugin"
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
}

$pluginZipPath = Join-Path $tempDir "lsp-support.zip"

# Download plugin
if (Download-Plugin -PluginId $pluginXmlId -OutputPath $pluginZipPath) {
    Write-Host ""

    # Install in each IDE
    $successCount = 0
    foreach ($ideInfo in $installedIDEs) {
        if (Install-PluginInIDE -IDE $ideInfo -PluginZipPath $pluginZipPath) {
            $successCount++
        }
        Write-Host ""
    }

    # Cleanup
    Remove-Item -Path $pluginZipPath -Force -ErrorAction SilentlyContinue

    # Summary
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host "  Installation Complete" -ForegroundColor Cyan
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host ""

    if ($successCount -gt 0) {
        Write-Host "✓ Successfully installed LSP Support plugin in $successCount IDE(s)" -ForegroundColor Green
        Write-Host ""
        Write-Host "IMPORTANT: You must restart your IDE(s) for the plugin to take effect!" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "After restarting, test with these files:" -ForegroundColor White
        Write-Host "  C:\Users\spare\source\repos\ga\jetbrains-plugin\test-files\" -ForegroundColor Gray
        Write-Host ""
    } else {
        Write-Host "⚠ No IDEs were successfully configured" -ForegroundColor Yellow
        Write-Host "Please try manual installation" -ForegroundColor Yellow
        Write-Host ""
    }
} else {
    Write-Host ""
    Write-Host "Failed to download plugin. Please install manually:" -ForegroundColor Red
    Write-Host "  1. Open your IDE" -ForegroundColor White
    Write-Host "  2. Go to File → Settings → Plugins" -ForegroundColor White
    Write-Host "  3. Search for 'LSP Support'" -ForegroundColor White
    Write-Host "  4. Install the plugin by Red Hat" -ForegroundColor White
    Write-Host "  5. Restart the IDE" -ForegroundColor White
    Write-Host ""
}

