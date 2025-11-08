#!/usr/bin/env pwsh

Write-Host "Fixing .NET 9.0 and package versions..." -ForegroundColor Green

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
    
    # Revert .NET version to 9.0
    $content = $content -replace "net10\.0", "net9.0"
    
    # Fix package versions to consistent 9.0.0
    $content = $content -replace 'Version="10\.0\.0"', 'Version="9.0.0"'
    $content = $content -replace 'Version="9\.0\.9"', 'Version="9.0.0"'
    $content = $content -replace 'Version="9\.0\.10"', 'Version="9.0.0"'
    
    if ($content -ne $originalContent) {
        Write-Host "Fixing: $($file.Name)" -ForegroundColor Yellow
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "Package version fix complete!" -ForegroundColor Green
