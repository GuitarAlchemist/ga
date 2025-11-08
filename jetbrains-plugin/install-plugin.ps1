#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install Music Theory DSL Plugin in JetBrains IDEs

.DESCRIPTION
    Builds and installs the Music Theory DSL plugin in Rider, WebStorm, or other JetBrains IDEs.

.PARAMETER IDE
    The IDE to install the plugin in. Options: Rider, WebStorm, IntelliJ, PhpStorm, PyCharm, CLion, GoLand, RubyMine

.PARAMETER SkipBuild
    Skip building the plugin and use existing build

.EXAMPLE
    .\install-plugin.ps1 -IDE Rider
    .\install-plugin.ps1 -IDE WebStorm -SkipBuild
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Rider", "WebStorm", "IntelliJ", "PhpStorm", "PyCharm", "CLion", "GoLand", "RubyMine")]
    [string]$IDE,
    
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Music Theory DSL Plugin Installer" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Get plugin directory
$pluginDir = $PSScriptRoot
$buildDir = Join-Path $pluginDir "build"
$distDir = Join-Path $buildDir "distributions"

# Build the plugin if not skipped
if (-not $SkipBuild) {
    Write-Host "Step 1: Building plugin..." -ForegroundColor Yellow
    Write-Host ""
    
    # Check if gradlew exists
    $gradlew = Join-Path $pluginDir "gradlew.bat"
    if (-not (Test-Path $gradlew)) {
        Write-Host "ERROR: gradlew.bat not found. Initializing Gradle wrapper..." -ForegroundColor Red
        
        # Initialize Gradle wrapper
        Push-Location $pluginDir
        try {
            gradle wrapper --gradle-version 8.5
        } catch {
            Write-Host "ERROR: Failed to initialize Gradle wrapper. Please install Gradle manually." -ForegroundColor Red
            exit 1
        }
        Pop-Location
    }
    
    # Build the plugin
    Push-Location $pluginDir
    try {
        & $gradlew buildPlugin
        if ($LASTEXITCODE -ne 0) {
            throw "Gradle build failed"
        }
    } catch {
        Write-Host "ERROR: Failed to build plugin: $_" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location
    
    Write-Host ""
    Write-Host "✓ Plugin built successfully" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Step 1: Skipping build (using existing build)" -ForegroundColor Yellow
    Write-Host ""
}

# Find the plugin ZIP file
$pluginZip = Get-ChildItem -Path $distDir -Filter "*.zip" | Select-Object -First 1
if (-not $pluginZip) {
    Write-Host "ERROR: Plugin ZIP file not found in $distDir" -ForegroundColor Red
    Write-Host "Please run without -SkipBuild to build the plugin first." -ForegroundColor Red
    exit 1
}

Write-Host "Step 2: Installing plugin in $IDE..." -ForegroundColor Yellow
Write-Host ""

# Determine IDE plugins directory
$idePluginsDir = switch ($IDE) {
    "Rider" { Join-Path $env:APPDATA "JetBrains\Rider2023.3\plugins" }
    "WebStorm" { Join-Path $env:APPDATA "JetBrains\WebStorm2023.3\plugins" }
    "IntelliJ" { Join-Path $env:APPDATA "JetBrains\IntelliJIdea2023.3\plugins" }
    "PhpStorm" { Join-Path $env:APPDATA "JetBrains\PhpStorm2023.3\plugins" }
    "PyCharm" { Join-Path $env:APPDATA "JetBrains\PyCharm2023.3\plugins" }
    "CLion" { Join-Path $env:APPDATA "JetBrains\CLion2023.3\plugins" }
    "GoLand" { Join-Path $env:APPDATA "JetBrains\GoLand2023.3\plugins" }
    "RubyMine" { Join-Path $env:APPDATA "JetBrains\RubyMine2023.3\plugins" }
}

# Check if IDE is installed
if (-not (Test-Path $idePluginsDir)) {
    Write-Host "WARNING: $IDE plugins directory not found at:" -ForegroundColor Yellow
    Write-Host "  $idePluginsDir" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Trying alternative installation method..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please install the plugin manually:" -ForegroundColor Cyan
    Write-Host "  1. Open $IDE" -ForegroundColor Cyan
    Write-Host "  2. Go to File → Settings → Plugins" -ForegroundColor Cyan
    Write-Host "  3. Click the gear icon ⚙️ → Install Plugin from Disk..." -ForegroundColor Cyan
    Write-Host "  4. Select: $($pluginZip.FullName)" -ForegroundColor Cyan
    Write-Host "  5. Restart $IDE" -ForegroundColor Cyan
    Write-Host ""
    
    # Open the distributions folder
    Start-Process $distDir
    exit 0
}

# Create plugins directory if it doesn't exist
if (-not (Test-Path $idePluginsDir)) {
    New-Item -ItemType Directory -Path $idePluginsDir -Force | Out-Null
}

# Extract plugin to plugins directory
$pluginName = "music-theory-dsl-plugin"
$targetDir = Join-Path $idePluginsDir $pluginName

# Remove old version if exists
if (Test-Path $targetDir) {
    Write-Host "Removing old version..." -ForegroundColor Yellow
    Remove-Item -Path $targetDir -Recurse -Force
}

# Extract new version
Write-Host "Extracting plugin to $targetDir..." -ForegroundColor Yellow
Expand-Archive -Path $pluginZip.FullName -DestinationPath $targetDir -Force

Write-Host ""
Write-Host "✓ Plugin installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Installation Complete" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart $IDE" -ForegroundColor White
Write-Host "  2. Create a new file with extension:" -ForegroundColor White
Write-Host "     - .chordprog (Chord Progression DSL)" -ForegroundColor White
Write-Host "     - .fretboard (Fretboard Navigation DSL)" -ForegroundColor White
Write-Host "     - .scaletrans (Scale Transformation DSL)" -ForegroundColor White
Write-Host "     - .groth (Grothendieck Operations DSL)" -ForegroundColor White
Write-Host "  3. Start typing to see syntax highlighting and auto-completion!" -ForegroundColor White
Write-Host ""
Write-Host "Plugin location: $targetDir" -ForegroundColor Gray
Write-Host ""

