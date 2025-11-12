#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Add project references to all microservices
#>

$ErrorActionPreference = "Stop"

Write-Host "🔧 Adding project references to microservices..." -ForegroundColor Cyan
Write-Host ""

$servicesBasePath = "C:\Users\spare\source\repos\ga\Apps\ga-server"

# Define project references for each service (absolute paths)
$repoRoot = "C:\Users\spare\source\repos\ga"
$serviceReferences = @{
    "GA.BSP.Service" = @(
        "$repoRoot\Common\GA.Business.Core\GA.Business.Core.csproj",
        "$repoRoot\Common\GA.BSP.Core\GA.BSP.Core.csproj",
        "$repoRoot\Common\GA.Business.Core.Orchestration\GA.Business.Core.Orchestration.csproj"
    )
    "GA.AI.Service" = @(
        "$repoRoot\Common\GA.Business.Core\GA.Business.Core.csproj",
        "$repoRoot\Common\GA.Business.AI\GA.Business.AI.csproj",
        "$repoRoot\GA.Data.SemanticKernel.Embeddings\GA.Data.SemanticKernel.Embeddings.csproj"
    )
    "GA.Knowledge.Service" = @(
        "$repoRoot\Common\GA.Business.Core\GA.Business.Core.csproj",
        "$repoRoot\Common\GA.Business.Config\GA.Business.Config.fsproj"
    )
    "GA.Fretboard.Service" = @(
        "$repoRoot\Common\GA.Business.Core\GA.Business.Core.csproj",
        "$repoRoot\Common\GA.Business.Core.Fretboard\GA.Business.Core.Fretboard.csproj"
    )
    "GA.Analytics.Service" = @(
        "$repoRoot\Common\GA.Business.Core\GA.Business.Core.csproj",
        "$repoRoot\Common\GA.Business.Core.Analysis\GA.Business.Core.Analysis.csproj"
    )
    "GA.MusicTheory.Service" = @(
        "$repoRoot\Common\GA.Business.Core\GA.Business.Core.csproj",
        "$repoRoot\Common\GA.Business.Core.Harmony\GA.Business.Core.Harmony.csproj",
        "$repoRoot\Common\GA.MusicTheory.DSL\GA.MusicTheory.DSL.fsproj"
    )
}

foreach ($service in $serviceReferences.Keys) {
    Write-Host "📦 Adding references to $service..." -ForegroundColor Yellow

    $csprojPath = Join-Path $servicesBasePath "$service\$service.csproj"

    if (-not (Test-Path $csprojPath)) {
        Write-Host "  ⚠️  Project file not found: $csprojPath" -ForegroundColor Yellow
        continue
    }

    $references = $serviceReferences[$service]

    foreach ($ref in $references) {
        if (Test-Path $ref) {
            $refName = Split-Path $ref -Leaf
            Write-Host "  Adding: $refName" -ForegroundColor Gray
            dotnet add $csprojPath reference $ref 2>&1 | Out-Null

            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✅ Added: $refName" -ForegroundColor Green
            } else {
                Write-Host "  ⚠️  Already exists or error: $refName" -ForegroundColor Yellow
            }
        } else {
            Write-Host "  ⚠️  Reference not found: $ref" -ForegroundColor Yellow
        }
    }

    Write-Host ""
}

Write-Host "✨ Project references added!" -ForegroundColor Green
Write-Host ""
Write-Host "🔨 Building all microservices..." -ForegroundColor Yellow
Write-Host ""

$buildResults = @{}

foreach ($service in $serviceReferences.Keys) {
    Write-Host "Building $service..." -ForegroundColor Cyan
    $csprojPath = Join-Path $servicesBasePath "$service\$service.csproj"

    $buildOutput = dotnet build $csprojPath -c Debug 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ $service built successfully!" -ForegroundColor Green
        $buildResults[$service] = "SUCCESS"
    } else {
        Write-Host "❌ $service build failed" -ForegroundColor Red
        $buildResults[$service] = "FAILED"

        # Show first 3 errors
        $buildOutput | Select-String -Pattern "error " | Select-Object -First 3 | ForEach-Object {
            Write-Host "  $_" -ForegroundColor Red
        }
    }
    Write-Host ""
}

Write-Host ""
Write-Host "📊 Build Summary:" -ForegroundColor Cyan
Write-Host ""

foreach ($service in $buildResults.Keys | Sort-Object) {
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
Write-Host "Result: $successCount/$totalCount services built successfully" -ForegroundColor Cyan

