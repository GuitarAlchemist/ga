#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete setup for GA.MusicTheory.Service
#>

$ErrorActionPreference = "Stop"

$serviceName = "GA.MusicTheory.Service"
$servicePath = Join-Path $PSScriptRoot "..\Apps\ga-server\$serviceName"
$gaApiPath = Join-Path $PSScriptRoot "..\Apps\ga-server\GaApi"

Write-Host "🚀 Setting up $serviceName..." -ForegroundColor Cyan
Write-Host ""

# 1. Create directory structure
Write-Host "📁 Creating directory structure..." -ForegroundColor Yellow
$dirs = @("Services", "Models", "Extensions")
foreach ($dir in $dirs) {
    $dirPath = Join-Path $servicePath $dir
    if (-not (Test-Path $dirPath)) {
        New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
        Write-Host "  ✅ Created $dir/" -ForegroundColor Green
    }
}

# 2. Copy Services
Write-Host ""
Write-Host "📦 Copying services..." -ForegroundColor Yellow
$services = @(
    "MongoDbService.cs",
    "PerformanceMetricsService.cs"
)
foreach ($service in $services) {
    $source = Join-Path $gaApiPath "Services\$service"
    $dest = Join-Path $servicePath "Services\$service"
    if (Test-Path $source) {
        Copy-Item $source $dest -Force
        
        # Update namespace
        $content = Get-Content $dest -Raw
        $content = $content.Replace('namespace GaApi.Services;', "namespace $serviceName.Services;")
        $content = $content.Replace('using GaApi.', "using $serviceName.")
        Set-Content $dest -Value $content -NoNewline
        
        Write-Host "  ✅ Copied $service" -ForegroundColor Green
    }
}

# 3. Copy Models
Write-Host ""
Write-Host "📦 Copying models..." -ForegroundColor Yellow
$models = @(
    "MongoDbSettings.cs",
    "ApiResponse.cs",
    "KeyDto.cs",
    "ModeDto.cs",
    "ScaleDegreeDto.cs",
    "ParseGrothendieckRequest.cs",
    "ParseGrothendieckResponse.cs",
    "GenerateGrothendieckRequest.cs",
    "GenerateGrothendieckResponse.cs"
)
foreach ($model in $models) {
    $source = Join-Path $gaApiPath "Models\$model"
    $dest = Join-Path $servicePath "Models\$model"
    if (Test-Path $source) {
        Copy-Item $source $dest -Force
        
        # Update namespace
        $content = Get-Content $dest -Raw
        $content = $content.Replace('namespace GaApi.Models;', "namespace $serviceName.Models;")
        $content = $content.Replace('using GaApi.', "using $serviceName.")
        Set-Content $dest -Value $content -NoNewline
        
        Write-Host "  ✅ Copied $model" -ForegroundColor Green
    } else {
        Write-Host "  ⏭️  Skipped $model (not found)" -ForegroundColor DarkGray
    }
}

# 4. Add MongoDB package reference
Write-Host ""
Write-Host "📦 Adding MongoDB package..." -ForegroundColor Yellow
$csprojPath = Join-Path $servicePath "$serviceName.csproj"
$csprojContent = Get-Content $csprojPath -Raw

if ($csprojContent -notmatch "MongoDB.Driver") {
    $packageRef = @"
    <PackageReference Include="MongoDB.Driver" Version="3.2.0"/>
"@
    $csprojContent = $csprojContent.Replace('</ItemGroup>', "$packageRef`n  </ItemGroup>")
    Set-Content $csprojPath -Value $csprojContent -NoNewline
    Write-Host "  ✅ Added MongoDB.Driver package" -ForegroundColor Green
}

# 5. Update Program.cs to register services
Write-Host ""
Write-Host "📝 Updating Program.cs..." -ForegroundColor Yellow

$programPath = Join-Path $servicePath "Program.cs"
$programContent = Get-Content $programPath -Raw

# Add using statements
$usings = @"
using GA.MusicTheory.Service.Models;
using GA.MusicTheory.Service.Services;
using Microsoft.Extensions.Caching.Memory;

"@

if ($programContent -notmatch "using GA.MusicTheory.Service.Models") {
    $programContent = $usings + $programContent
}

# Add service registrations before builder.Services.AddControllers()
$serviceRegistrations = @"

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<PerformanceMetricsService>();
builder.Services.AddMemoryCache();

"@

if ($programContent -notmatch "MongoDbService") {
    $programContent = $programContent.Replace('builder.Services.AddControllers();', "$serviceRegistrations`nbuilder.Services.AddControllers();")
}

Set-Content $programPath -Value $programContent -NoNewline
Write-Host "  ✅ Updated Program.cs" -ForegroundColor Green

# 6. Create appsettings.json with MongoDB configuration
Write-Host ""
Write-Host "📝 Updating appsettings.json..." -ForegroundColor Yellow

$appsettingsPath = Join-Path $servicePath "appsettings.json"
$appsettings = @"
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "guitar-alchemist",
    "Collections": {
      "Chords": "chords",
      "ChordTemplates": "chord-templates",
      "Scales": "scales",
      "Progressions": "progressions"
    }
  }
}
"@

Set-Content $appsettingsPath -Value $appsettings -NoNewline
Write-Host "  ✅ Updated appsettings.json" -ForegroundColor Green

Write-Host ""
Write-Host "✨ $serviceName setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Build the service: dotnet build Apps/ga-server/$serviceName" -ForegroundColor White
Write-Host "2. Run the service: dotnet run --project Apps/ga-server/$serviceName" -ForegroundColor White
Write-Host "3. Test endpoints at: https://localhost:7001/swagger" -ForegroundColor White

