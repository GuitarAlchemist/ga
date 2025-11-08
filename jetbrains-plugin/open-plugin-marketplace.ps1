#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Open JetBrains Plugin Marketplace in browser to install LSP Support

.DESCRIPTION
    Opens the LSP Support plugin page in the browser and provides installation instructions.
#>

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  LSP Support Plugin Installation Helper" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Plugin marketplace URL
$pluginUrl = "https://plugins.jetbrains.com/plugin/23257-lsp-support"

Write-Host "Opening LSP Support plugin page in your browser..." -ForegroundColor Yellow
Write-Host ""

# Open the plugin page
Start-Process $pluginUrl

Write-Host "✓ Plugin page opened in browser" -ForegroundColor Green
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Installation Instructions" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Option 1: Install from IDE (RECOMMENDED)" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Open Rider or WebStorm" -ForegroundColor White
Write-Host "2. Go to File → Settings → Plugins" -ForegroundColor White
Write-Host "3. Click the 'Marketplace' tab" -ForegroundColor White
Write-Host "4. Search for 'LSP Support'" -ForegroundColor White
Write-Host "5. Click 'Install' on the plugin by Red Hat" -ForegroundColor White
Write-Host "6. Click 'Restart IDE' when prompted" -ForegroundColor White
Write-Host ""
Write-Host "Option 2: Install from Browser" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. On the plugin page that just opened, click the 'Install to IDE' button" -ForegroundColor White
Write-Host "2. Select your IDE (Rider or WebStorm)" -ForegroundColor White
Write-Host "3. The IDE will open and prompt you to install the plugin" -ForegroundColor White
Write-Host "4. Click 'Install' and then 'Restart IDE'" -ForegroundColor White
Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  After Installation" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The Music Theory DSL Language Server is already configured!" -ForegroundColor Green
Write-Host ""
Write-Host "Test files are ready at:" -ForegroundColor White
Write-Host "  C:\Users\spare\source\repos\ga\jetbrains-plugin\test-files\" -ForegroundColor Gray
Write-Host ""
Write-Host "After restarting your IDE, open any test file and start typing!" -ForegroundColor Yellow
Write-Host ""
Write-Host "Test files:" -ForegroundColor White
Write-Host "  - example.chordprog (Chord Progression DSL)" -ForegroundColor Gray
Write-Host "  - example.fretboard (Fretboard Navigation DSL)" -ForegroundColor Gray
Write-Host "  - example.scaletrans (Scale Transformation DSL)" -ForegroundColor Gray
Write-Host "  - example.groth (Grothendieck Operations DSL)" -ForegroundColor Gray
Write-Host ""

# Open test files directory
$testFilesDir = "C:\Users\spare\source\repos\ga\jetbrains-plugin\test-files"
if (Test-Path $testFilesDir) {
    Write-Host "Opening test files directory..." -ForegroundColor Yellow
    Start-Process $testFilesDir
    Write-Host "✓ Test files directory opened" -ForegroundColor Green
    Write-Host ""
}

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  Quick Reference" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configured IDEs:" -ForegroundColor White
Write-Host "  ✓ Rider 2023.3" -ForegroundColor Green
Write-Host "  ✓ Rider 2024.2" -ForegroundColor Green
Write-Host "  ✓ Rider 2024.3" -ForegroundColor Green
Write-Host "  ✓ WebStorm 2024.2" -ForegroundColor Green
Write-Host "  ✓ WebStorm 2024.3" -ForegroundColor Green
Write-Host ""
Write-Host "LSP Server Location:" -ForegroundColor White
Write-Host "  C:\Users\spare\source\repos\ga\Apps\GaMusicTheoryLsp\bin\Debug\net9.0\ga-music-theory-lsp.dll" -ForegroundColor Gray
Write-Host ""
Write-Host "Documentation:" -ForegroundColor White
Write-Host "  - Quick Start: jetbrains-plugin\QUICK_START.md" -ForegroundColor Gray
Write-Host "  - Integration Guide: jetbrains-plugin\JETBRAINS_LSP_INTEGRATION.md" -ForegroundColor Gray
Write-Host "  - Installation Summary: jetbrains-plugin\INSTALLATION_COMPLETE.md" -ForegroundColor Gray
Write-Host ""

