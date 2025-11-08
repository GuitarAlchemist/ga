#!/usr/bin/env pwsh
# Fix TARS MCP Server TypeScript Errors

Write-Host "🔧 Fixing TARS MCP Server TypeScript Errors..." -ForegroundColor Cyan

$tarsPath = "C:/Users/spare/source/repos/tars/mcp-server"

# Fix diagnostics.ts
Write-Host "`n📝 Fixing diagnostics.ts..." -ForegroundColor Yellow
$diagnosticsPath = "$tarsPath/src/diagnostics.ts"
$lines = Get-Content $diagnosticsPath

$fixed = @()
foreach ($line in $lines)
{
    # Fix vramUsed property access (line 52, 53)
    if ($line -match '^\s+memoryUsed: controller\.vramUsed')
    {
        $fixed += $line -replace 'controller\.vramUsed', '(controller as any).vramUsed'
    }
    elseif ($line -match '^\s+memoryFree: \(controller\.vram.*controller\.vramUsed')
    {
        $fixed += $line -replace 'controller\.vramUsed', '(controller as any).vramUsed'
    }
    # Fix error.message access
    elseif ($line -match 'error\.message\s*\}')
    {
        $fixed += $line -replace 'error\.message', '(error as Error).message'
    }
    # Fix dnsResolutionTime assignment (line 226)
    elseif ($line -match '^\s+dnsResolutionTime = Date\.now\(\) - dnsStart;')
    {
        $fixed += "      dnsResolutionTime = String(Date.now() - dnsStart);"
    }
    # Fix env vars assignment (line 345)
    elseif ($line -match '^\s+envVars\[key\] = value;')
    {
        $fixed += "        envVars[key] = value || '';"
    }
    else
    {
        $fixed += $line
    }
}

$fixed | Set-Content $diagnosticsPath
Write-Host "✅ Fixed diagnostics.ts" -ForegroundColor Green

# Fix index.ts
Write-Host "`n📝 Fixing index.ts..." -ForegroundColor Yellow
$indexPath = "$tarsPath/src/index.ts"
$lines = Get-Content $indexPath

$fixed = @()
foreach ($line in $lines)
{
    # Fix error.message access in logger.error calls
    if ($line -match "logger\.error\('Tool execution failed', \{ name, error: error\.message \}\);")
    {
        $fixed += "    logger.error('Tool execution failed', { name, error: (error as Error).message });"
    }
    # Fix error.message in template string
    elseif ($line -match 'text: `Error executing tool \$\{name\}: \$\{error\.message\}`')
    {
        $fixed += "          text: `Error executing tool ${name}: ${(error as Error).message}`,"
    }
    else
    {
        $fixed += $line
    }
}

$fixed | Set-Content $indexPath
Write-Host "✅ Fixed index.ts" -ForegroundColor Green

# Try building
Write-Host "`n🔨 Building TARS MCP Server..." -ForegroundColor Cyan
Push-Location $tarsPath
try
{
    $buildOutput = npm run build 2>&1
    if ($LASTEXITCODE -eq 0)
    {
        Write-Host "✅ Build successful!" -ForegroundColor Green

        # Check if dist/index.js exists
        if (Test-Path "dist/index.js")
        {
            Write-Host "✅ dist/index.js created" -ForegroundColor Green

            # Show file size
            $size = (Get-Item "dist/index.js").Length
            Write-Host "📦 Size: $([math]::Round($size/1KB, 2) ) KB" -ForegroundColor Gray
        }
    }
    else
    {
        Write-Host "❌ Build failed" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Gray
    }
}
finally
{
    Pop-Location
}

Write-Host "`n📊 Summary:" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray

if (Test-Path "$tarsPath/dist/index.js")
{
    Write-Host "✅ TARS MCP Server built successfully" -ForegroundColor Green
    Write-Host "`n💡 To enable in Codex:" -ForegroundColor Yellow
    Write-Host "   1. Edit ~/.codex/config.toml" -ForegroundColor Gray
    Write-Host "   2. Uncomment [mcp_servers.tars_mcp] section" -ForegroundColor Gray
    Write-Host "   3. Restart Codex/Augment" -ForegroundColor Gray
}
else
{
    Write-Host "❌ Build failed - see errors above" -ForegroundColor Red
}

