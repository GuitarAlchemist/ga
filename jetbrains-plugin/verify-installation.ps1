#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verify LSP Support plugin installation and test Music Theory DSL integration

.DESCRIPTION
    Checks if the LSP Support plugin is installed and opens test files for verification.
#>

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Music Theory DSL Installation Verification" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Check for LSP Support plugin installation
function Test-PluginInstalled {
    param([string]$PluginsPath)
    
    $pluginId = "com.redhat.devtools.lsp4ij"
    $pluginPath = Join-Path $PluginsPath $pluginId
    
    if (Test-Path $pluginPath) {
        return $true
    }
    
    # Also check for alternative plugin directory names
    $lspDirs = Get-ChildItem -Path $PluginsPath -Directory -ErrorAction SilentlyContinue | 
               Where-Object { $_.Name -like "*lsp*" }
    
    return ($lspDirs.Count -gt 0)
}

# Get installed IDEs
$ides = @()

$riderVersions = @("2023.3", "2024.1", "2024.2", "2024.3")
foreach ($version in $riderVersions) {
    $path = Join-Path $env:APPDATA "JetBrains\Rider$version"
    if (Test-Path $path) {
        $pluginsPath = Join-Path $path "plugins"
        $ides += @{
            Name = "Rider"
            Version = $version
            PluginsPath = $pluginsPath
            HasLSP = (Test-PluginInstalled -PluginsPath $pluginsPath)
        }
    }
}

$webstormVersions = @("2023.3", "2024.1", "2024.2", "2024.3")
foreach ($version in $webstormVersions) {
    $path = Join-Path $env:APPDATA "JetBrains\WebStorm$version"
    if (Test-Path $path) {
        $pluginsPath = Join-Path $path "plugins"
        $ides += @{
            Name = "WebStorm"
            Version = $version
            PluginsPath = $pluginsPath
            HasLSP = (Test-PluginInstalled -PluginsPath $pluginsPath)
        }
    }
}

Write-Host "Checking IDE installations..." -ForegroundColor Yellow
Write-Host ""

$installedCount = 0
$configuredCount = 0

foreach ($ide in $ides) {
    $ideName = "$($ide.Name) $($ide.Version)"
    
    if ($ide.HasLSP) {
        Write-Host "  ✓ $ideName - LSP Support plugin installed" -ForegroundColor Green
        $installedCount++
    } else {
        Write-Host "  ✗ $ideName - LSP Support plugin NOT installed" -ForegroundColor Red
    }
    $configuredCount++
}

Write-Host ""

if ($installedCount -eq 0) {
    Write-Host "==================================================================" -ForegroundColor Red
    Write-Host "  LSP Support Plugin Not Found" -ForegroundColor Red
    Write-Host "==================================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "The LSP Support plugin is not installed in any IDE." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please install it:" -ForegroundColor White
    Write-Host "  1. Open Rider or WebStorm" -ForegroundColor White
    Write-Host "  2. Go to File → Settings → Plugins → Marketplace" -ForegroundColor White
    Write-Host "  3. Search for 'LSP Support'" -ForegroundColor White
    Write-Host "  4. Click Install → Restart IDE" -ForegroundColor White
    Write-Host ""
    Write-Host "Or run: pwsh -File jetbrains-plugin/open-plugin-marketplace.ps1" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "==================================================================" -ForegroundColor Green
Write-Host "  ✓ LSP Support Plugin Installed!" -ForegroundColor Green
Write-Host "==================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Found LSP Support plugin in $installedCount out of $configuredCount IDE(s)" -ForegroundColor Green
Write-Host ""

# Check LSP server
$lspServerPath = "C:\Users\spare\source\repos\ga\Apps\GaMusicTheoryLsp\bin\Debug\net9.0\ga-music-theory-lsp.dll"

Write-Host "Checking LSP server..." -ForegroundColor Yellow
if (Test-Path $lspServerPath) {
    Write-Host "  ✓ LSP server found at: $lspServerPath" -ForegroundColor Green
} else {
    Write-Host "  ✗ LSP server not found" -ForegroundColor Red
    Write-Host "  Building LSP server..." -ForegroundColor Yellow
    
    $lspProjectPath = "C:\Users\spare\source\repos\ga\Apps\GaMusicTheoryLsp\GaMusicTheoryLsp.fsproj"
    try {
        dotnet build $lspProjectPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ LSP server built successfully" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ✗ Failed to build LSP server" -ForegroundColor Red
    }
}

Write-Host ""

# Open test files
$testFilesDir = "C:\Users\spare\source\repos\ga\jetbrains-plugin\test-files"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Next Steps: Test the Integration!" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Open one of the test files in Rider or WebStorm:" -ForegroundColor Yellow
Write-Host ""

if (Test-Path $testFilesDir) {
    $testFiles = Get-ChildItem -Path $testFilesDir -File
    foreach ($file in $testFiles) {
        Write-Host "   - $($file.Name)" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "2. Try these tests:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Test Auto-Completion:" -ForegroundColor White
    Write-Host "   - In example.chordprog: Type 'I' and press Ctrl+Space" -ForegroundColor Gray
    Write-Host "     You should see: I, II, III, IV, V, VI, VII" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   - In example.fretboard: Type 'pos' and press Ctrl+Space" -ForegroundColor Gray
    Write-Host "     You should see: position" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   - In example.groth: Type 'ten' and press Ctrl+Space" -ForegroundColor Gray
    Write-Host "     You should see: tensor" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Test Syntax Validation:" -ForegroundColor White
    Write-Host "   - Type something invalid like 'INVALID_CHORD'" -ForegroundColor Gray
    Write-Host "     You should see a red underline with error message" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Test Hover Documentation:" -ForegroundColor White
    Write-Host "   - Hover over any DSL construct" -ForegroundColor Gray
    Write-Host "     You should see documentation popup" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "Opening test files directory..." -ForegroundColor Yellow
    Start-Process $testFilesDir
    Write-Host "✓ Test files directory opened" -ForegroundColor Green
    Write-Host ""
}

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Troubleshooting" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "If auto-completion doesn't work:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Check LSP server status in IDE:" -ForegroundColor White
Write-Host "   Tools → Language Server Protocol → Server Status" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Restart LSP server:" -ForegroundColor White
Write-Host "   Tools → Language Server Protocol → Restart All Servers" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Check IDE logs:" -ForegroundColor White
Write-Host "   Help → Show Log in Explorer/Finder" -ForegroundColor Gray
Write-Host "   Look for LSP-related errors" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Verify file extension:" -ForegroundColor White
Write-Host "   Make sure the file has .chordprog, .fretboard, .scaletrans, or .groth extension" -ForegroundColor Gray
Write-Host ""

Write-Host "==================================================================" -ForegroundColor Green
Write-Host "  ✓ Setup Complete - Ready to Test!" -ForegroundColor Green
Write-Host "==================================================================" -ForegroundColor Green
Write-Host ""

