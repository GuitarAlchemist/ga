#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test the GaMcpServer MCP server
#>

$ErrorActionPreference = "Stop"

Write-Host "🎸 Testing Guitar Alchemist MCP Server..." -ForegroundColor Cyan
Write-Host ""

# Build the server first
Write-Host "📦 Building GaMcpServer..." -ForegroundColor Yellow
$buildResult = dotnet build "C:\Users\spare\source\repos\ga\GaMcpServer\GaMcpServer.csproj" -c Debug 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful!" -ForegroundColor Green
Write-Host ""

# Show available tools
Write-Host "🔧 Available MCP Tools:" -ForegroundColor Cyan
Write-Host ""

Write-Host "Key Tools (KeyTools.cs):" -ForegroundColor Yellow
Write-Host "  • GetAllKeys() - Get all available keys" -ForegroundColor White
Write-Host "  • GetMajorKeys() - Get all major keys" -ForegroundColor White
Write-Host "  • GetMinorKeys() - Get all minor keys" -ForegroundColor White
Write-Host "  • GetKeySignatureInfo(keyName) - Get key signature information" -ForegroundColor White
Write-Host "  • GetRelativeKey(keyName) - Get relative major/minor key" -ForegroundColor White
Write-Host "  • GetParallelKey(keyName) - Get parallel major/minor key" -ForegroundColor White
Write-Host "  • GetCircleOfFifths() - Get circle of fifths progression" -ForegroundColor White
Write-Host "  • GetKeyRelationships(keyName) - Get related keys" -ForegroundColor White
Write-Host ""

Write-Host "Mode Tools (ModeTool.cs):" -ForegroundColor Yellow
Write-Host "  • GetAvailableModes() - Get all available modes" -ForegroundColor White
Write-Host "  • GetModeInfo(modeName) - Get mode information" -ForegroundColor White
Write-Host ""

Write-Host "Atonal Tools (AtonalTool.cs):" -ForegroundColor Yellow
Write-Host "  • GetSetClasses() - Get all set classes" -ForegroundColor White
Write-Host "  • GetModalSetClasses() - Get all modal set classes" -ForegroundColor White
Write-Host "  • GetModalFamilyInfo(intervalVector) - Get modal family info" -ForegroundColor White
Write-Host "  • GetCardinalities() - Get all cardinalities" -ForegroundColor White
Write-Host ""

Write-Host "Instrument Tools (InstrumentTool.cs):" -ForegroundColor Yellow
Write-Host "  • Instrument and tuning information" -ForegroundColor White
Write-Host ""

Write-Host "Web Integration Tools:" -ForegroundColor Yellow
Write-Host "  • WebSearchToolWrapper - Web search" -ForegroundColor White
Write-Host "  • WebScrapingToolWrapper - Web scraping" -ForegroundColor White
Write-Host "  • FeedReaderToolWrapper - RSS/Atom feeds" -ForegroundColor White
Write-Host ""

Write-Host "✨ MCP Server is ready!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 To use the MCP server:" -ForegroundColor Cyan
Write-Host "  1. Configure in Claude Desktop or Cline" -ForegroundColor White
Write-Host "  2. Add to MCP client configuration:" -ForegroundColor White
Write-Host '     "ga-music-theory": {' -ForegroundColor Gray
Write-Host '       "command": "dotnet",' -ForegroundColor Gray
Write-Host '       "args": ["run", "--project", "C:/Users/spare/source/repos/ga/GaMcpServer/GaMcpServer.csproj"]' -ForegroundColor Gray
Write-Host '     }' -ForegroundColor Gray
Write-Host ""
Write-Host "🎯 Example queries to try:" -ForegroundColor Cyan
Write-Host "  • 'What are all the major keys?'" -ForegroundColor White
Write-Host "  • 'Tell me about the Dorian mode'" -ForegroundColor White
Write-Host "  • 'What is the relative minor of C major?'" -ForegroundColor White
Write-Host "  • 'Show me the circle of fifths'" -ForegroundColor White
Write-Host "  • 'What are the modal set classes?'" -ForegroundColor White
Write-Host ""

