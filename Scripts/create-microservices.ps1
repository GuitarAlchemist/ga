#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Creates microservice projects for Guitar Alchemist architecture
.DESCRIPTION
    This script creates 6 microservice projects with proper structure, dependencies, and configuration
.EXAMPLE
    .\create-microservices.ps1
#>

param(
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"

# Define microservices
$services = @(
    @{
        Name = "GA.MusicTheory.Service"
        Port = 7001
        Description = "Music theory operations (keys, modes, scales, intervals, chords)"
        Dependencies = @("GA.Business.Core", "GA.MusicTheory.DSL", "GA.Core")
        Controllers = @("MusicTheoryController", "ChordsController", "DslController")
    },
    @{
        Name = "GA.BSP.Service"
        Port = 7002
        Description = "Binary Space Partitioning and spatial analysis"
        Dependencies = @("GA.BSP.Core", "GA.Business.Orchestration", "GA.Data.MongoDB")
        Controllers = @("BSPController", "BSPRoomController", "MusicRoomController", "IntelligentBSPController")
    },
    @{
        Name = "GA.AI.Service"
        Port = 7003
        Description = "AI/ML operations, embeddings, semantic search"
        Dependencies = @("GA.Business.AI", "GA.Business.Intelligence", "GA.Data.SemanticKernel.Embeddings")
        Controllers = @("SemanticSearchController", "VectorSearchController", "VectorSearchStrategyController", "AdvancedAIController", "AdaptiveAIController")
    },
    @{
        Name = "GA.Knowledge.Service"
        Port = 7004
        Description = "YAML configuration management and musical knowledge"
        Dependencies = @("GA.Business.Config", "GA.Business.Assets", "GA.Data.EntityFramework")
        Controllers = @("MusicalKnowledgeController", "GuitarTechniquesController", "SpecializedTuningsController", "AssetsController", "AssetRelationshipsController")
    },
    @{
        Name = "GA.Fretboard.Service"
        Port = 7005
        Description = "Guitar-specific analysis and biomechanics"
        Dependencies = @("GA.Business.Fretboard", "GA.Business.Core.Fretboard")
        Controllers = @("GuitarPlayingController", "BiomechanicsController", "ContextualChordsController", "ChordProgressionsController", "MonadicChordsController")
    },
    @{
        Name = "GA.Analytics.Service"
        Port = 7006
        Description = "Advanced mathematical analysis"
        Dependencies = @("GA.Business.Analytics", "GA.Business.Intelligence")
        Controllers = @("SpectralAnalyticsController", "GrothendieckController", "InvariantsController", "AdvancedAnalyticsController", "MetricsController")
    }
)

Write-Host "🚀 Creating Guitar Alchemist Microservices Architecture" -ForegroundColor Cyan
Write-Host ""

$baseDir = Join-Path $PSScriptRoot "..\Apps\ga-server"

foreach ($service in $services) {
    $serviceName = $service.Name
    $serviceDir = Join-Path $baseDir $serviceName
    
    Write-Host "📦 Creating $serviceName..." -ForegroundColor Yellow
    
    # Create directory structure
    if (Test-Path $serviceDir) {
        if ($Force) {
            Write-Host "   ⚠️  Removing existing directory..." -ForegroundColor DarkYellow
            Remove-Item $serviceDir -Recurse -Force
        } else {
            Write-Host "   ⏭️  Skipping (already exists, use -Force to recreate)" -ForegroundColor DarkGray
            continue
        }
    }
    
    New-Item -ItemType Directory -Path $serviceDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $serviceDir "Controllers") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $serviceDir "Properties") -Force | Out-Null
    
    # Create .csproj file
    $csprojContent = @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>$($serviceName.ToLower())-secrets</UserSecretsId>
    <WarningsAsErrors>CS1998</WarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.StackExchange.Redis.DistributedCaching" Version="9.5.2"/>
    <PackageReference Include="Microsoft.Extensions.Caching.StackExchangeRedis" Version="9.0.10"/>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.9.0"/>
    <PackageReference Include="Swashbuckle.AspNetCore.Annotations" Version="6.9.0"/>
    <PackageReference Include="Hellang.Middleware.ProblemDetails" Version="6.5.1"/>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\AllProjects.ServiceDefaults\AllProjects.ServiceDefaults.csproj"/>
"@

    foreach ($dep in $service.Dependencies) {
        $depPath = if ($dep.EndsWith(".fsproj")) {
            "..\..\..\Common\$($dep.Replace('.fsproj', ''))\$dep"
        } else {
            "..\..\..\Common\$dep\$dep.csproj"
        }
        $csprojContent += "`n    <ProjectReference Include=`"$depPath`"/>"
    }

    $csprojContent += @"

  </ItemGroup>
</Project>
"@

    Set-Content -Path (Join-Path $serviceDir "$serviceName.csproj") -Value $csprojContent
    
    # Create Program.cs
    $programContent = @"
using System.Reflection;
using Hellang.Middleware.ProblemDetails;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Redis distributed caching
builder.AddRedisDistributedCache("redis");

// Add controllers
builder.Services.AddControllers();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "$($service.Name.Replace('GA.', '').Replace('.Service', '')) Service",
        Version = "v1",
        Description = "$($service.Description)"
    });

    var xmlFile = `$"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.EnableAnnotations();
});

// Add problem details middleware
builder.Services.AddProblemDetails(options =>
{
    options.IncludeExceptionDetails = (ctx, ex) => builder.Environment.IsDevelopment();
    options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
    options.MapToStatusCode<ArgumentException>(StatusCodes.Status400BadRequest);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

var app = builder.Build();

// Configure middleware pipeline
app.UseProblemDetails();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "$($service.Name.Replace('GA.', '').Replace('.Service', '')) Service v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
app.UseRateLimiter();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
"@

    Set-Content -Path (Join-Path $serviceDir "Program.cs") -Value $programContent
    
    # Create appsettings.json
    $appsettingsContent = @"
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
"@

    Set-Content -Path (Join-Path $serviceDir "appsettings.json") -Value $appsettingsContent
    
    # Create launchSettings.json
    $launchSettingsContent = @"
{
  "profiles": {
    "$serviceName": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:$($service.Port);http://localhost:$($service.Port - 1000)",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
"@

    $propertiesDir = Join-Path $serviceDir "Properties"
    Set-Content -Path (Join-Path $propertiesDir "launchSettings.json") -Value $launchSettingsContent
    
    Write-Host "   ✅ Created $serviceName" -ForegroundColor Green
    Write-Host "      Port: $($service.Port)" -ForegroundColor DarkGray
    Write-Host "      Controllers: $($service.Controllers.Count)" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "✨ Microservices created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Add services to AllProjects.sln" -ForegroundColor White
Write-Host "2. Copy controllers from GaApi to respective services" -ForegroundColor White
Write-Host "3. Update AllProjects.AppHost to orchestrate services" -ForegroundColor White
Write-Host "4. Build and test each service" -ForegroundColor White

