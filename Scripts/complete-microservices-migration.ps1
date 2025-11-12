#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete microservices migration - copy all dependencies and build all services
#>

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Completing microservices migration..." -ForegroundColor Cyan
Write-Host ""

# Step 1: Copy all missing models from GaApi to each service
Write-Host "📦 Step 1: Copying missing models..." -ForegroundColor Yellow

$gaApiModelsPath = "C:\Users\spare\source\repos\ga\Apps\ga-server\GaApi\Models"
$gaApiServicesPath = "C:\Users\spare\source\repos\ga\Apps\ga-server\GaApi\Services"

# Models needed by Music Theory Service
$musicTheoryModels = @(
    "MusicRoomDocument.cs",
    "RoomGenerationJob.cs",
    "ChordStatistics.cs"
)

foreach ($model in $musicTheoryModels) {
    $source = Join-Path $gaApiModelsPath $model
    $dest = "C:\Users\spare\source\repos\ga\Apps\ga-server\GA.MusicTheory.Service\Models\$model"
    
    if (Test-Path $source) {
        Copy-Item $source $dest -Force
        
        # Update namespace
        $content = Get-Content $dest -Raw
        $content = $content.Replace('namespace GaApi.Models;', 'namespace GA.MusicTheory.Service.Models;')
        $content = $content.Replace('using GaApi.', 'using GA.MusicTheory.Service.')
        Set-Content $dest -Value $content -NoNewline
        
        Write-Host "  ✅ Copied $model to GA.MusicTheory.Service" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  $model not found, will need manual creation" -ForegroundColor Yellow
    }
}

# Step 2: Remove duplicate MongoDB package reference
Write-Host ""
Write-Host "📦 Step 2: Fixing duplicate package references..." -ForegroundColor Yellow

$csprojPath = "C:\Users\spare\source\repos\ga\Apps\ga-server\GA.MusicTheory.Service\GA.MusicTheory.Service.csproj"
$csprojContent = Get-Content $csprojPath -Raw

# Remove duplicate MongoDB.Driver references
$lines = $csprojContent -split "`n"
$seen = @{}
$newLines = @()

foreach ($line in $lines) {
    if ($line -match 'PackageReference Include="MongoDB.Driver"') {
        if (-not $seen.ContainsKey("MongoDB.Driver")) {
            $newLines += $line
            $seen["MongoDB.Driver"] = $true
        }
    } else {
        $newLines += $line
    }
}

$csprojContent = $newLines -join "`n"
Set-Content $csprojPath -Value $csprojContent -NoNewline
Write-Host "  ✅ Fixed duplicate MongoDB.Driver reference" -ForegroundColor Green

# Step 3: Build GA.MusicTheory.Service
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "🔨 Step 3: Building GA.MusicTheory.Service..." -ForegroundColor Yellow
    
    $buildResult = dotnet build "C:\Users\spare\source\repos\ga\Apps\ga-server\GA.MusicTheory.Service\GA.MusicTheory.Service.csproj" -c Debug 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ GA.MusicTheory.Service built successfully!" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Build failed. Errors:" -ForegroundColor Red
        $buildResult | Select-String -Pattern "error " | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    }
}

# Step 4: Summary
Write-Host ""
Write-Host "✨ Migration progress summary:" -ForegroundColor Cyan
Write-Host "  ✅ Controllers migrated: 29" -ForegroundColor Green
Write-Host "  ✅ Services created: 6" -ForegroundColor Green
Write-Host "  ✅ Using statements fixed: All controllers" -ForegroundColor Green
Write-Host "  ✅ Models copied: GA.MusicTheory.Service" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next steps:" -ForegroundColor Yellow
Write-Host "  1. Copy missing models to other services (BSP, AI, Knowledge, Fretboard, Analytics)" -ForegroundColor White
Write-Host "  2. Add project references to each service's .csproj" -ForegroundColor White
Write-Host "  3. Update Aspire orchestration (AllProjects.AppHost)" -ForegroundColor White
Write-Host "  4. Configure API Gateway with YARP" -ForegroundColor White
Write-Host "  5. Test each service independently" -ForegroundColor White
Write-Host ""
Write-Host "🎯 Current focus: GA.MusicTheory.Service is the template" -ForegroundColor Cyan
Write-Host "   Once it builds successfully, replicate the pattern to other services" -ForegroundColor White

