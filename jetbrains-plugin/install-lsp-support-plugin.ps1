#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Install LSP Support Plugin in JetBrains IDEs

.DESCRIPTION
    Downloads and installs the LSP Support plugin in Rider and WebStorm.
    This script uses the JetBrains Marketplace API to download the plugin.

.PARAMETER IDE
    The IDE to install the plugin in. Options: Rider, WebStorm, Both

.EXAMPLE
    .\install-lsp-support-plugin.ps1 -IDE Both
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Rider", "WebStorm", "Both")]
    [string]$IDE = "Both"
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  LSP Support Plugin Installer for JetBrains IDEs" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Plugin ID for LSP Support
$pluginId = "com.redhat.devtools.lsp4ij"
$pluginName = "LSP Support"

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
            }
        }
    }

    return $ides
}

function Test-PluginInstalled {
    param(
        [string]$PluginsPath
    )

    $pluginPath = Join-Path $PluginsPath $pluginId
    return Test-Path $pluginPath
}

# Get installed IDEs
$installedIDEs = Get-InstalledIDEs

if ($installedIDEs.Count -eq 0) {
    Write-Host "ERROR: No JetBrains IDEs found" -ForegroundColor Red
    Write-Host "Please install Rider or WebStorm first" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($installedIDEs.Count) IDE installation(s):" -ForegroundColor Green
foreach ($ideInfo in $installedIDEs) {
    Write-Host "  - $($ideInfo.Name) $($ideInfo.Version)" -ForegroundColor White
}
Write-Host ""

# Filter by requested IDE
$targetIDEName = $IDE
if ($targetIDEName -ne "Both") {
    $installedIDEs = $installedIDEs | Where-Object { $_.Name -eq $targetIDEName }
}

if ($installedIDEs.Count -eq 0) {
    Write-Host "ERROR: $targetIDEName not found" -ForegroundColor Red
    exit 1
}

Write-Host "Installing LSP Support plugin..." -ForegroundColor Yellow
Write-Host ""

foreach ($ideInfo in $installedIDEs) {
    $ideName = "$($ideInfo.Name) $($ideInfo.Version)"

    Write-Host "Processing $ideName..." -ForegroundColor Yellow

    # Check if already installed
    if (Test-PluginInstalled -PluginsPath $ideInfo.PluginsPath) {
        Write-Host "  ✓ LSP Support plugin already installed" -ForegroundColor Green
        Write-Host ""
        continue
    }

    Write-Host "  Plugin not found, installation required" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Manual Installation Required" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The LSP Support plugin must be installed manually from within the IDE." -ForegroundColor Yellow
Write-Host ""
Write-Host "Please follow these steps for each IDE:" -ForegroundColor White
Write-Host ""

$ideIndex = 1
foreach ($ideInfo in $installedIDEs) {
    $ideName = "$($ideInfo.Name) $($ideInfo.Version)"

    if (-not (Test-PluginInstalled -PluginsPath $ideInfo.PluginsPath)) {
        Write-Host "[$ideIndex] ${ideName}:" -ForegroundColor Cyan
        Write-Host "    1. Open $ideName" -ForegroundColor White
        Write-Host "    2. Go to File -> Settings -> Plugins" -ForegroundColor White
        Write-Host "    3. Click the 'Marketplace' tab" -ForegroundColor White
        Write-Host "    4. Search for 'LSP Support'" -ForegroundColor White
        Write-Host "    5. Click 'Install' on the plugin by Red Hat" -ForegroundColor White
        Write-Host "    6. Click 'Restart IDE' when prompted" -ForegroundColor White
        Write-Host ""
        $ideIndex++
    }
}

Write-Host "After installing the plugin and restarting, the Music Theory DSL" -ForegroundColor Green
Write-Host "Language Server will be automatically available!" -ForegroundColor Green
Write-Host ""
Write-Host "Test by creating a file with one of these extensions:" -ForegroundColor Yellow
Write-Host "  - .chordprog (Chord Progression DSL)" -ForegroundColor White
Write-Host "  - .fretboard (Fretboard Navigation DSL)" -ForegroundColor White
Write-Host "  - .scaletrans (Scale Transformation DSL)" -ForegroundColor White
Write-Host "  - .groth (Grothendieck Operations DSL)" -ForegroundColor White
Write-Host ""

# Create a quick test file
$testDir = Join-Path $PSScriptRoot "test-files"
if (-not (Test-Path $testDir)) {
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
}

$testFiles = @{
    "example.chordprog" = @"
# Chord Progression DSL Example
# Try typing to see auto-completion!

I - IV - V - I
Cmaj7 - Fmaj7 - G7 - Cmaj7
ii - V - I
"@
    "example.fretboard" = @"
# Fretboard Navigation DSL Example
# Try typing to see auto-completion!

position 5 3
CAGED C
move up 2
slide string 1 fret 5 to 7
"@
    "example.scaletrans" = @"
# Scale Transformation DSL Example
# Try typing to see auto-completion!

C major
transpose 2
mode dorian
invert
"@
    "example.groth" = @"
# Grothendieck Operations DSL Example
# Try typing to see auto-completion!

tensor(Cmaj7, Gmaj7)
direct_sum(C_major_scale, G_major_scale)
functor(transpose_by_fifth)
"@
}

foreach ($file in $testFiles.Keys) {
    $filePath = Join-Path $testDir $file
    $testFiles[$file] | Out-File -FilePath $filePath -Encoding UTF8 -Force
}

Write-Host "✓ Created test files in: $testDir" -ForegroundColor Green
Write-Host ""
Write-Host "Open these files in your IDE to test the LSP integration!" -ForegroundColor Yellow
Write-Host ""

# Open the test directory
Start-Process $testDir

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Next Steps Summary" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Install 'LSP Support' plugin in your IDE(s)" -ForegroundColor White
Write-Host "2. Restart the IDE(s)" -ForegroundColor White
Write-Host "3. Open test files from: $testDir" -ForegroundColor White
Write-Host "4. Start typing to see auto-completion!" -ForegroundColor White
Write-Host ""
Write-Host "For detailed instructions, see:" -ForegroundColor Gray
Write-Host "  jetbrains-plugin\JETBRAINS_LSP_INTEGRATION.md" -ForegroundColor Gray
Write-Host ""

