#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Replace Newtonsoft.Json with System.Text.Json in F# files
#>

$ErrorActionPreference = "Stop"

Write-Host "🔄 Replacing Newtonsoft.Json with System.Text.Json..." -ForegroundColor Cyan
Write-Host ""

# Files to update
$files = @(
    "Common/GA.MusicTheory.DSL/Adapters/TarsGrammarAdapter.fs",
    "Common/GA.MusicTheory.DSL/LSP/LanguageServer.fs"
)

foreach ($file in $files) {
    $fullPath = Join-Path $PSScriptRoot "..\$file"
    
    if (-not (Test-Path $fullPath)) {
        Write-Host "⚠️  File not found: $file" -ForegroundColor Yellow
        continue
    }
    
    Write-Host "📝 Processing: $file" -ForegroundColor Green
    
    $content = Get-Content $fullPath -Raw
    
    # Replace Newtonsoft.Json with System.Text.Json
    $content = $content -replace 'Newtonsoft\.Json\.JsonConvert\.SerializeObject\(([^,]+), Newtonsoft\.Json\.Formatting\.Indented\)', 'System.Text.Json.JsonSerializer.Serialize($1, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })'
    $content = $content -replace 'Newtonsoft\.Json\.JsonConvert\.DeserializeObject<([^>]+)>\(([^)]+)\)', 'System.Text.Json.JsonSerializer.Deserialize<$1>($2)'
    $content = $content -replace 'Newtonsoft\.Json\.JsonConvert\.SerializeObject\(([^)]+)\)', 'System.Text.Json.JsonSerializer.Serialize($1)'
    $content = $content -replace 'Newtonsoft\.Json\.Linq\.JObject', 'System.Text.Json.Nodes.JsonObject'
    $content = $content -replace 'Newtonsoft\.Json\.Linq\.JArray', 'System.Text.Json.Nodes.JsonArray'
    $content = $content -replace 'Newtonsoft\.Json\.Linq\.JValue', 'System.Text.Json.Nodes.JsonValue'
    $content = $content -replace 'Newtonsoft\.Json\.Linq\.JToken', 'System.Text.Json.Nodes.JsonNode'
    
    # Add System.Text.Json open statement if not present
    if ($content -notmatch 'open System\.Text\.Json') {
        $content = $content -replace '(open System\b)', "`$1`nopen System.Text.Json`nopen System.Text.Json.Nodes"
    }
    
    Set-Content $fullPath -Value $content -NoNewline
    Write-Host "  ✅ Updated $file" -ForegroundColor DarkGreen
}

Write-Host ""
Write-Host "✅ Newtonsoft.Json replacement complete!" -ForegroundColor Green

