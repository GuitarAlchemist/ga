#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Configure Music Theory DSL LSP Server in JetBrains IDEs

.DESCRIPTION
    Automatically configures the Music Theory DSL Language Server in Rider and WebStorm.
    This script modifies the IDE configuration files to add LSP server definitions.

.PARAMETER IDE
    The IDE to configure. Options: Rider, WebStorm, Both

.EXAMPLE
    .\configure-lsp-in-jetbrains.ps1 -IDE Rider
    .\configure-lsp-in-jetbrains.ps1 -IDE WebStorm
    .\configure-lsp-in-jetbrains.ps1 -IDE Both
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Rider", "WebStorm", "Both")]
    [string]$IDE = "Both"
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Music Theory DSL LSP Configuration for JetBrains IDEs" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Get repository root
$repoRoot = Split-Path -Parent $PSScriptRoot
$lspServerPath = Join-Path $repoRoot "Apps\GaMusicTheoryLsp\bin\Debug\net9.0\ga-music-theory-lsp.dll"

# Check if LSP server is built
if (-not (Test-Path $lspServerPath)) {
    Write-Host "ERROR: LSP server not found at:" -ForegroundColor Red
    Write-Host "  $lspServerPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Building LSP server..." -ForegroundColor Yellow

    $lspProjectPath = Join-Path $repoRoot "Apps\GaMusicTheoryLsp\GaMusicTheoryLsp.fsproj"
    try {
        dotnet build $lspProjectPath
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
    } catch {
        Write-Host "ERROR: Failed to build LSP server: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please build manually:" -ForegroundColor Yellow
        Write-Host "  cd Apps\GaMusicTheoryLsp" -ForegroundColor Yellow
        Write-Host "  dotnet build" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "✓ LSP server found at: $lspServerPath" -ForegroundColor Green
Write-Host ""

# Find dotnet executable
$dotnetPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetPath) {
    Write-Host "ERROR: dotnet not found in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 9.0 SDK" -ForegroundColor Red
    exit 1
}

Write-Host "✓ dotnet found at: $dotnetPath" -ForegroundColor Green
Write-Host ""

function Configure-IDE {
    param(
        [string]$IDEName,
        [string]$ConfigPath
    )

    Write-Host "Configuring $IDEName..." -ForegroundColor Yellow

    # Check if IDE is installed
    if (-not (Test-Path $ConfigPath)) {
        Write-Host "  ⚠ $IDEName not found at: $ConfigPath" -ForegroundColor Yellow
        Write-Host "  Skipping $IDEName configuration" -ForegroundColor Yellow
        return $false
    }

    Write-Host "  ✓ $IDEName found" -ForegroundColor Green

    # Create LSP configuration directory
    $lspConfigDir = Join-Path $ConfigPath "options"
    if (-not (Test-Path $lspConfigDir)) {
        New-Item -ItemType Directory -Path $lspConfigDir -Force | Out-Null
    }

    $lspConfigFile = Join-Path $lspConfigDir "lsp.xml"

    # Create LSP configuration XML
    $lspConfig = @"
<application>
  <component name="LspServerConfiguration">
    <servers>
      <server>
        <name>Music Theory DSL</name>
        <extensions>chordprog;fretboard;scaletrans;groth</extensions>
        <command>$dotnetPath</command>
        <args>$lspServerPath</args>
        <enabled>true</enabled>
      </server>
    </servers>
  </component>
</application>
"@

    # Write configuration file
    $lspConfig | Out-File -FilePath $lspConfigFile -Encoding UTF8 -Force

    Write-Host "  ✓ LSP configuration written to: $lspConfigFile" -ForegroundColor Green

    # Create file type associations
    $fileTypesConfigFile = Join-Path $lspConfigDir "filetypes.xml"

    $fileTypesConfig = @"
<application>
  <component name="FileTypeManager" version="18">
    <extensionMap>
      <mapping pattern="*.chordprog" type="PLAIN_TEXT" />
      <mapping pattern="*.fretboard" type="PLAIN_TEXT" />
      <mapping pattern="*.scaletrans" type="PLAIN_TEXT" />
      <mapping pattern="*.groth" type="PLAIN_TEXT" />
    </extensionMap>
  </component>
</application>
"@

    $fileTypesConfig | Out-File -FilePath $fileTypesConfigFile -Encoding UTF8 -Force

    Write-Host "  ✓ File type associations written to: $fileTypesConfigFile" -ForegroundColor Green
    Write-Host ""

    return $true
}

# Configure IDEs
$configured = @()

if ($IDE -eq "Rider" -or $IDE -eq "Both") {
    # Try multiple Rider versions
    $riderVersions = @("2023.3", "2024.1", "2024.2", "2024.3")
    $riderConfigured = $false

    foreach ($version in $riderVersions) {
        $riderConfigPath = Join-Path $env:APPDATA "JetBrains\Rider$version"
        if (Configure-IDE -IDEName "Rider $version" -ConfigPath $riderConfigPath) {
            $configured += "Rider $version"
            $riderConfigured = $true
        }
    }

    if (-not $riderConfigured) {
        Write-Host "⚠ Rider not found. Please install Rider or configure manually." -ForegroundColor Yellow
        Write-Host ""
    }
}

if ($IDE -eq "WebStorm" -or $IDE -eq "Both") {
    # Try multiple WebStorm versions
    $webstormVersions = @("2023.3", "2024.1", "2024.2", "2024.3")
    $webstormConfigured = $false

    foreach ($version in $webstormVersions) {
        $webstormConfigPath = Join-Path $env:APPDATA "JetBrains\WebStorm$version"
        if (Configure-IDE -IDEName "WebStorm $version" -ConfigPath $webstormConfigPath) {
            $configured += "WebStorm $version"
            $webstormConfigured = $true
        }
    }

    if (-not $webstormConfigured) {
        Write-Host "⚠ WebStorm not found. Please install WebStorm or configure manually." -ForegroundColor Yellow
        Write-Host ""
    }
}

# Summary
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Configuration Complete" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

if ($configured.Count -gt 0) {
    Write-Host "Configured LSP server in:" -ForegroundColor Green
    foreach ($ideVersion in $configured) {
        Write-Host "  - $ideVersion" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Install the 'LSP Support' plugin in your IDE:" -ForegroundColor White
    Write-Host "     File → Settings → Plugins → Search 'LSP Support' → Install" -ForegroundColor White
    Write-Host "  2. Restart your IDE" -ForegroundColor White
    Write-Host "  3. Create a test file:" -ForegroundColor White
    Write-Host "     - example.chordprog (Chord Progression DSL)" -ForegroundColor White
    Write-Host "     - example.fretboard (Fretboard Navigation DSL)" -ForegroundColor White
    Write-Host "     - example.scaletrans (Scale Transformation DSL)" -ForegroundColor White
    Write-Host "     - example.groth (Grothendieck Operations DSL)" -ForegroundColor White
    Write-Host "  4. Start typing to see auto-completion and validation!" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "⚠ No IDEs were configured." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please install Rider or WebStorm and run this script again." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Or configure manually using the guide:" -ForegroundColor Yellow
    Write-Host "  jetbrains-plugin\JETBRAINS_LSP_INTEGRATION.md" -ForegroundColor White
    Write-Host ""
}

Write-Host "For more information, see:" -ForegroundColor Gray
Write-Host "  jetbrains-plugin\JETBRAINS_LSP_INTEGRATION.md" -ForegroundColor Gray
Write-Host ""

