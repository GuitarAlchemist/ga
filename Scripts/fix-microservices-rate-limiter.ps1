#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fixes rate limiter configuration in all microservices for .NET 10
#>

$ErrorActionPreference = "Stop"

$services = @(
    "GA.BSP.Service",
    "GA.AI.Service",
    "GA.Knowledge.Service",
    "GA.Fretboard.Service",
    "GA.Analytics.Service"
)

$oldPattern = @'
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
'@

$newPattern = @'
// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});
'@

foreach ($service in $services) {
    $programFile = Join-Path $PSScriptRoot "..\Apps\ga-server\$service\Program.cs"
    
    if (Test-Path $programFile) {
        Write-Host "Fixing $service..." -ForegroundColor Yellow
        $content = Get-Content $programFile -Raw
        $content = $content.Replace($oldPattern, $newPattern)
        Set-Content $programFile -Value $content -NoNewline
        Write-Host "  ✅ Fixed $service" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  File not found: $programFile" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "✨ All microservices fixed!" -ForegroundColor Green

