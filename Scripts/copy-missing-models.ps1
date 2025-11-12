#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Copy missing models to microservices to fix build errors
#>

$ErrorActionPreference = "Stop"

Write-Host "📦 Copying missing models to microservices..." -ForegroundColor Cyan
Write-Host ""

$repoRoot = "C:\Users\spare\source\repos\ga"
$servicesPath = "$repoRoot\Apps\ga-server"

# Define what needs to be copied where
$copyOperations = @(
    @{
        Source = "$servicesPath\GA.MusicTheory.Service\Models\Chord.cs"
        Destinations = @(
            "$servicesPath\GA.BSP.Service\Models\Chord.cs",
            "$servicesPath\GA.Knowledge.Service\Models\Chord.cs",
            "$servicesPath\GA.Analytics.Service\Models\Chord.cs"
        )
    },
    @{
        Source = "$servicesPath\GA.MusicTheory.Service\Models\MusicRoomDocument.cs"
        Destinations = @(
            "$servicesPath\GA.AI.Service\Models\MusicRoomDocument.cs",
            "$servicesPath\GA.Analytics.Service\Models\MusicRoomDocument.cs"
        )
    }
)

foreach ($op in $copyOperations) {
    $sourcePath = $op.Source
    $sourceFile = Split-Path $sourcePath -Leaf

    if (-not (Test-Path $sourcePath)) {
        Write-Host "⚠️  Source not found: $sourceFile" -ForegroundColor Yellow
        continue
    }

    Write-Host "📄 Copying $sourceFile..." -ForegroundColor Yellow

    foreach ($destPath in $op.Destinations) {
        $destDir = Split-Path $destPath -Parent
        $destService = Split-Path (Split-Path $destPath -Parent) -Parent | Split-Path -Leaf

        # Create destination directory if it doesn't exist
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        # Read source content
        $content = Get-Content $sourcePath -Raw

        # Update namespace for destination service
        $content = $content -replace 'namespace GA\.MusicTheory\.Service\.Models', "namespace $destService.Models"

        # Write to destination
        Set-Content -Path $destPath -Value $content -NoNewline

        Write-Host "  ✅ Copied to $destService" -ForegroundColor Green
    }

    Write-Host ""
}

Write-Host "✨ Missing models copied!" -ForegroundColor Green
Write-Host ""
Write-Host "🔨 Building all microservices..." -ForegroundColor Yellow
Write-Host ""

$services = @(
    "GA.MusicTheory.Service",
    "GA.BSP.Service",
    "GA.AI.Service",
    "GA.Knowledge.Service",
    "GA.Fretboard.Service",
    "GA.Analytics.Service"
)

$buildResults = @{}

foreach ($service in $services) {
    Write-Host "Building $service..." -ForegroundColor Cyan
    $csprojPath = "$servicesPath\$service\$service.csproj"

    $buildOutput = dotnet build $csprojPath -c Debug 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ $service built successfully!" -ForegroundColor Green
        $buildResults[$service] = "SUCCESS"
    } else {
        Write-Host "❌ $service build failed" -ForegroundColor Red
        $buildResults[$service] = "FAILED"

        # Show first 3 errors
        $errorLines = $buildOutput | Select-String -Pattern "error " | Select-Object -First 3
        if ($errorLines) {
            foreach ($errorLine in $errorLines) {
                Write-Host "  $errorLine" -ForegroundColor Red
            }
        }
    }
    Write-Host ""
}

Write-Host ""
Write-Host "📊 Build Summary:" -ForegroundColor Cyan
Write-Host ""

foreach ($service in $services | Sort-Object) {
    $status = $buildResults[$service]
    if ($status -eq "SUCCESS") {
        Write-Host "  ✅ $service" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $service" -ForegroundColor Red
    }
}

$successCount = ($buildResults.Values | Where-Object { $_ -eq "SUCCESS" }).Count
$totalCount = $buildResults.Count

Write-Host ""
if ($successCount -eq $totalCount) {
    Write-Host "🎉 All $totalCount services built successfully!" -ForegroundColor Green
} else {
    Write-Host "Result: $successCount/$totalCount services built successfully" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "💡 Next steps for failed services:" -ForegroundColor Cyan
    Write-Host "  1. Review error messages above" -ForegroundColor Gray
    Write-Host "  2. Add missing project references if needed" -ForegroundColor Gray
    Write-Host "  3. Create missing model files manually" -ForegroundColor Gray
    Write-Host "  4. Check controller dependencies" -ForegroundColor Gray
}

