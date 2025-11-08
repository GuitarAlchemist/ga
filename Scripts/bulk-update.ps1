#!/usr/bin/env pwsh

Write-Host "Bulk updating project files..." -ForegroundColor Green

# Get all .csproj files
$csprojFiles = Get-ChildItem -Path . -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Found $($csprojFiles.Count) project files"

# Update each file
foreach ($file in $csprojFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Update .NET version
    $content = $content -replace "net9\.0", "net10.0"
    $content = $content -replace "net8\.0", "net10.0"
    
    # Update project references
    $content = $content -replace "GA\.Business\.Core\.AI", "GA.Business.AI"
    $content = $content -replace "GA\.Business\.Core\.Analysis", "GA.Business.Analysis"
    $content = $content -replace "GA\.Business\.Core\.Fretboard", "GA.Business.Fretboard"
    $content = $content -replace "GA\.Business\.Core\.Harmony", "GA.Business.Harmony"
    $content = $content -replace "GA\.Business\.Core\.Orchestration", "GA.Business.Orchestration"
    $content = $content -replace "GA\.Business\.Core\.UI", "GA.Business.UI"
    $content = $content -replace "GA\.Business\.Core\.Web", "GA.Business.Web"
    $content = $content -replace "GA\.Business\.Core\.Mapping", "GA.Business.Mapping"
    $content = $content -replace "GA\.Business\.Core\.Graphiti", "GA.Business.Graphiti"
    
    # Update package versions
    $content = $content -replace 'Version="9\.0\.0"', 'Version="10.0.0"'
    $content = $content -replace 'Version="9\.0\.9"', 'Version="10.0.0"'
    $content = $content -replace 'Version="9\.0\.10"', 'Version="10.0.0"'
    
    if ($content -ne $originalContent) {
        Write-Host "Updating: $($file.Name)" -ForegroundColor Yellow
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

# Update C# files
Write-Host "Updating C# source files..." -ForegroundColor Green

$csFiles = Get-ChildItem -Path . -Recurse -Filter "*.cs" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Found $($csFiles.Count) C# files"

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    
    $originalContent = $content
    
    # Update namespaces
    $content = $content -replace "namespace GA\.Business\.Core\.AI", "namespace GA.Business.AI"
    $content = $content -replace "namespace GA\.Business\.Core\.Analysis", "namespace GA.Business.Analysis"
    $content = $content -replace "namespace GA\.Business\.Core\.Fretboard", "namespace GA.Business.Fretboard"
    $content = $content -replace "namespace GA\.Business\.Core\.Harmony", "namespace GA.Business.Harmony"
    $content = $content -replace "namespace GA\.Business\.Core\.Orchestration", "namespace GA.Business.Orchestration"
    $content = $content -replace "namespace GA\.Business\.Core\.UI", "namespace GA.Business.UI"
    $content = $content -replace "namespace GA\.Business\.Core\.Web", "namespace GA.Business.Web"
    $content = $content -replace "namespace GA\.Business\.Core\.Mapping", "namespace GA.Business.Mapping"
    $content = $content -replace "namespace GA\.Business\.Core\.Graphiti", "namespace GA.Business.Graphiti"
    
    # Update using statements
    $content = $content -replace "using GA\.Business\.Core\.AI", "using GA.Business.AI"
    $content = $content -replace "using GA\.Business\.Core\.Analysis", "using GA.Business.Analysis"
    $content = $content -replace "using GA\.Business\.Core\.Fretboard", "using GA.Business.Fretboard"
    $content = $content -replace "using GA\.Business\.Core\.Harmony", "using GA.Business.Harmony"
    $content = $content -replace "using GA\.Business\.Core\.Orchestration", "using GA.Business.Orchestration"
    $content = $content -replace "using GA\.Business\.Core\.UI", "using GA.Business.UI"
    $content = $content -replace "using GA\.Business\.Core\.Web", "using GA.Business.Web"
    $content = $content -replace "using GA\.Business\.Core\.Mapping", "using GA.Business.Mapping"
    $content = $content -replace "using GA\.Business\.Core\.Graphiti", "using GA.Business.Graphiti"
    
    if ($content -ne $originalContent) {
        Write-Host "Updating: $($file.Name)" -ForegroundColor Yellow
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "Bulk update complete!" -ForegroundColor Green
