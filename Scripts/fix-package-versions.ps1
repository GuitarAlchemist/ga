#!/usr/bin/env pwsh

Write-Host "Fixing package version conflicts..." -ForegroundColor Green

# Get all .csproj files
$csprojFiles = Get-ChildItem -Path . -Recurse -Filter "*.csproj" | Where-Object { 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" 
}

Write-Host "Found $($csprojFiles.Count) project files"

# Update each file to use consistent higher versions
foreach ($file in $csprojFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Update to consistent higher versions to avoid downgrades
    $content = $content -replace 'Version="9\.0\.0"', 'Version="9.0.10"'
    $content = $content -replace 'Version="9\.0\.3"', 'Version="9.0.10"'
    $content = $content -replace 'Version="9\.0\.9"', 'Version="9.0.10"'
    
    # Fix specific packages that need higher versions
    $content = $content -replace 'Microsoft\.Extensions\.Caching\.StackExchangeRedis.*Version="[^"]*"', 'Microsoft.Extensions.Caching.StackExchangeRedis" Version="9.0.10"'
    $content = $content -replace 'Microsoft\.Extensions\.Logging\.Abstractions.*Version="[^"]*"', 'Microsoft.Extensions.Logging.Abstractions" Version="9.0.10"'
    $content = $content -replace 'System\.Numerics\.Tensors.*Version="[^"]*"', 'System.Numerics.Tensors" Version="9.0.10"'
    
    if ($content -ne $originalContent) {
        Write-Host "Fixing: $($file.Name)" -ForegroundColor Yellow
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "Package version conflicts fixed!" -ForegroundColor Green
