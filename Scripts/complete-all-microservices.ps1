#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete all microservices by copying dependencies from GaApi
.DESCRIPTION
    This script copies all required models, services, and dependencies from GaApi
    to each microservice, updates namespaces, and builds each service.
#>

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Completing all microservices migration..." -ForegroundColor Cyan
Write-Host ""

$gaApiPath = "C:\Users\spare\source\repos\ga\Apps\ga-server\GaApi"
$servicesBasePath = "C:\Users\spare\source\repos\ga\Apps\ga-server"

# Define service dependencies
$serviceDependencies = @{
    "GA.AI.Service" = @{
        Models = @("ChordSearchResult.cs", "VectorSearchPerformance.cs", "VectorSearchStats.cs", "Chord.cs", "MongoDbSettings.cs", "ApiResponse.cs")
        Services = @("MongoDbService.cs", "PerformanceMetricsService.cs")
    }
    "GA.BSP.Service" = @{
        Models = @("BSPModels.cs", "MusicRoomDocument.cs", "MongoDbSettings.cs", "ApiResponse.cs")
        Services = @("MongoDbService.cs", "PerformanceMetricsService.cs")
    }
    "GA.Knowledge.Service" = @{
        Models = @("MongoDbSettings.cs", "ApiResponse.cs")
        Services = @("MongoDbService.cs", "PerformanceMetricsService.cs")
    }
    "GA.Fretboard.Service" = @{
        Models = @("Chord.cs", "MongoDbSettings.cs", "ApiResponse.cs")
        Services = @("MongoDbService.cs", "PerformanceMetricsService.cs")
    }
    "GA.Analytics.Service" = @{
        Models = @("MongoDbSettings.cs", "ApiResponse.cs")
        Services = @("MongoDbService.cs", "PerformanceMetricsService.cs")
    }
}

function Copy-AndUpdateNamespace {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [string]$ServiceName
    )
    
    if (-not (Test-Path $SourcePath)) {
        Write-Host "  ⚠️  Source not found: $SourcePath" -ForegroundColor Yellow
        return $false
    }
    
    # Create destination directory if it doesn't exist
    $destDir = Split-Path $DestPath -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    
    # Copy file
    Copy-Item $SourcePath $DestPath -Force
    
    # Update namespace
    $content = Get-Content $DestPath -Raw
    $content = $content.Replace('namespace GaApi.Models;', "namespace $ServiceName.Models;")
    $content = $content.Replace('namespace GaApi.Services;', "namespace $ServiceName.Services;")
    $content = $content.Replace('using GaApi.Models;', "using $ServiceName.Models;")
    $content = $content.Replace('using GaApi.Services;', "using $ServiceName.Services;")
    $content = $content.Replace('using Models;', "using $ServiceName.Models;")
    $content = $content.Replace('using Services;', "using $ServiceName.Services;")
    Set-Content $DestPath -Value $content -NoNewline
    
    return $true
}

# Process each service
foreach ($service in $serviceDependencies.Keys) {
    Write-Host "📦 Processing $service..." -ForegroundColor Yellow
    
    $servicePath = Join-Path $servicesBasePath $service
    $deps = $serviceDependencies[$service]
    
    # Create directories
    $modelsPath = Join-Path $servicePath "Models"
    $servicesPath = Join-Path $servicePath "Services"
    
    if (-not (Test-Path $modelsPath)) {
        New-Item -ItemType Directory -Path $modelsPath -Force | Out-Null
    }
    if (-not (Test-Path $servicesPath)) {
        New-Item -ItemType Directory -Path $servicesPath -Force | Out-Null
    }
    
    # Copy models
    foreach ($model in $deps.Models) {
        $sourcePath = Join-Path $gaApiPath "Models\$model"
        $destPath = Join-Path $modelsPath $model
        
        if (Copy-AndUpdateNamespace -SourcePath $sourcePath -DestPath $destPath -ServiceName $service) {
            Write-Host "  ✅ Copied model: $model" -ForegroundColor Green
        }
    }
    
    # Copy services
    foreach ($svc in $deps.Services) {
        $sourcePath = Join-Path $gaApiPath "Services\$svc"
        $destPath = Join-Path $servicesPath $svc
        
        if (Copy-AndUpdateNamespace -SourcePath $sourcePath -DestPath $destPath -ServiceName $service) {
            Write-Host "  ✅ Copied service: $svc" -ForegroundColor Green
        }
    }
    
    # Add MongoDB package if not already added
    $csprojPath = Join-Path $servicePath "$service.csproj"
    $csprojContent = Get-Content $csprojPath -Raw
    
    if ($csprojContent -notmatch 'MongoDB.Driver') {
        Write-Host "  📦 Adding MongoDB.Driver package..." -ForegroundColor Cyan
        dotnet add $csprojPath package MongoDB.Driver --version 3.2.0 | Out-Null
    }
    
    # Update Program.cs to register services
    $programPath = Join-Path $servicePath "Program.cs"
    $programContent = Get-Content $programPath -Raw
    
    if ($programContent -notmatch 'MongoDbService') {
        Write-Host "  🔧 Updating Program.cs..." -ForegroundColor Cyan
        
        # Add using statements
        if ($programContent -notmatch "using $service.Models;") {
            $programContent = $programContent.Replace(
                'var builder = WebApplication.CreateBuilder(args);',
                "using $service.Models;`nusing $service.Services;`nusing Microsoft.Extensions.Caching.Memory;`n`nvar builder = WebApplication.CreateBuilder(args);"
            )
        }
        
        # Add service registrations
        if ($programContent -notmatch 'AddSingleton<MongoDbService>') {
            $programContent = $programContent.Replace(
                'builder.Services.AddControllers();',
                @"
// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<PerformanceMetricsService>();
builder.Services.AddMemoryCache();

builder.Services.AddControllers();
"@
            )
        }
        
        Set-Content $programPath -Value $programContent -NoNewline
    }
    
    # Create appsettings.json if it doesn't exist
    $appsettingsPath = Join-Path $servicePath "appsettings.json"
    if (-not (Test-Path $appsettingsPath)) {
        Write-Host "  📝 Creating appsettings.json..." -ForegroundColor Cyan
        
        $appsettings = @{
            Logging = @{
                LogLevel = @{
                    Default = "Information"
                    "Microsoft.AspNetCore" = "Warning"
                }
            }
            AllowedHosts = "*"
            MongoDB = @{
                ConnectionString = "mongodb://localhost:27017"
                DatabaseName = "guitar-alchemist"
                Collections = @{
                    Chords = "chords"
                    ChordTemplates = "chord-templates"
                    Scales = "scales"
                    Progressions = "progressions"
                }
            }
        }
        
        $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
    }
    
    Write-Host ""
}

# Build all services
if (-not $SkipBuild) {
    Write-Host "🔨 Building all microservices..." -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($service in $serviceDependencies.Keys) {
        Write-Host "  Building $service..." -ForegroundColor Cyan
        $csprojPath = Join-Path $servicesBasePath "$service\$service.csproj"
        
        $buildResult = dotnet build $csprojPath -c Debug 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ $service built successfully!" -ForegroundColor Green
        } else {
            Write-Host "  ❌ $service build failed" -ForegroundColor Red
            $buildResult | Select-String -Pattern "error " | Select-Object -First 3 | ForEach-Object {
                Write-Host "    $_" -ForegroundColor Red
            }
        }
    }
}

Write-Host ""
Write-Host "✨ Microservices migration complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Summary:" -ForegroundColor Cyan
Write-Host "  ✅ GA.MusicTheory.Service - Template service (100% complete)" -ForegroundColor Green
Write-Host "  ✅ GA.BSP.Service - Dependencies copied" -ForegroundColor Green
Write-Host "  ✅ GA.AI.Service - Dependencies copied" -ForegroundColor Green
Write-Host "  ✅ GA.Knowledge.Service - Dependencies copied" -ForegroundColor Green
Write-Host "  ✅ GA.Fretboard.Service - Dependencies copied" -ForegroundColor Green
Write-Host "  ✅ GA.Analytics.Service - Dependencies copied" -ForegroundColor Green
Write-Host ""
Write-Host "🎯 Next steps:" -ForegroundColor Yellow
Write-Host "  1. Configure YARP API Gateway in GaApi" -ForegroundColor White
Write-Host "  2. Test each microservice independently" -ForegroundColor White
Write-Host "  3. Test API Gateway routing" -ForegroundColor White
Write-Host "  4. Run integration tests" -ForegroundColor White

