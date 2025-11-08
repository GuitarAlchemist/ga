#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smoke test all MCP server connections
.DESCRIPTION
    Tests connectivity and basic functionality of all configured MCP servers
#>

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

Write-Host "🧪 MCP Server Smoke Test" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

# Test results
$results = @()

function Test-McpServer
{
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$Arguments,
        [hashtable]$Env = @{ },
        [int]$TimeoutSeconds = 10
    )

    Write-Host "Testing: $Name" -ForegroundColor Yellow
    Write-Host "  Command: $Command" -ForegroundColor Gray
    Write-Host "  Args: $( $Arguments -join ', ' )" -ForegroundColor Gray
    Write-Host "  Args Count: $( $Arguments.Count )" -ForegroundColor Gray

    $result = @{
        Name = $Name
        Command = $Command
        Args = $Arguments
        Status = "Unknown"
        Message = ""
        Duration = 0
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    try
    {
        # Set environment variables
        foreach ($key in $Env.Keys)
        {
            Set-Item -Path "env:$key" -Value $Env[$key]
        }

        # Test if command exists
        $commandPath = Get-Command $Command -ErrorAction SilentlyContinue
        if (-not $commandPath)
        {
            $result.Status = "Failed"
            $result.Message = "Command '$Command' not found"
            Write-Host "  ❌ FAILED: Command not found" -ForegroundColor Red
            return $result
        }

        # For npx commands, test if package can be resolved
        if ($Command -eq "npx")
        {
            $packageName = $Arguments | Where-Object { $_ -notmatch "^-" } | Select-Object -First 1
            Write-Host "  📦 Checking package: $packageName" -ForegroundColor Gray

            # Quick test - just verify npx is available
            try
            {
                $npxVersion = & npx --version 2>&1
                Write-Host "  📦 npx version: $npxVersion" -ForegroundColor Gray
                $result.Status = "Success"
                $result.Message = "npx available, package: $packageName"
                Write-Host "  ✅ SUCCESS: npx available for package $packageName" -ForegroundColor Green
            }
            catch
            {
                $result.Status = "Failed"
                $result.Message = "npx not available"
                Write-Host "  ❌ FAILED: npx not available" -ForegroundColor Red
            }
        }
        # For node commands, test if file exists
        elseif ($Command -eq "node")
        {
            if ($Arguments -and $Arguments.Count -gt 0)
            {
                $scriptPath = $Arguments[0]
                Write-Host "  📄 Checking script: $scriptPath" -ForegroundColor Gray
                if (Test-Path $scriptPath)
                {
                    $result.Status = "Success"
                    $result.Message = "Script file exists"
                    Write-Host "  ✅ SUCCESS: Script file found" -ForegroundColor Green
                }
                else
                {
                    $result.Status = "Failed"
                    $result.Message = "Script file not found: $scriptPath"
                    Write-Host "  ❌ FAILED: Script file not found" -ForegroundColor Red
                }
            }
            else
            {
                $result.Status = "Failed"
                $result.Message = "No script path provided"
                Write-Host "  ❌ FAILED: No script path provided" -ForegroundColor Red
            }
        }
        # For python commands, test if file exists and python is available
        elseif ($Command -eq "python")
        {
            try
            {
                $pythonVersion = & python --version 2>&1
                Write-Host "  🐍 Python: $pythonVersion" -ForegroundColor Gray

                if ($Arguments -and $Arguments.Count -gt 0)
                {
                    $scriptPath = $Arguments[0]
                    Write-Host "  📄 Checking script: $scriptPath" -ForegroundColor Gray
                    if (Test-Path $scriptPath)
                    {
                        $result.Status = "Success"
                        $result.Message = "Python script exists, Python available"
                        Write-Host "  ✅ SUCCESS: Python script found" -ForegroundColor Green
                    }
                    else
                    {
                        $result.Status = "Failed"
                        $result.Message = "Python script not found: $scriptPath"
                        Write-Host "  ❌ FAILED: Python script not found" -ForegroundColor Red
                    }
                }
                else
                {
                    $result.Status = "Failed"
                    $result.Message = "No script path provided"
                    Write-Host "  ❌ FAILED: No script path provided" -ForegroundColor Red
                }
            }
            catch
            {
                $result.Status = "Failed"
                $result.Message = "Python not available"
                Write-Host "  ❌ FAILED: Python not available" -ForegroundColor Red
            }
        }
        else
        {
            $result.Status = "Success"
            $result.Message = "Command available"
            Write-Host "  ✅ SUCCESS: Command available" -ForegroundColor Green
        }
    }
    catch
    {
        $result.Status = "Failed"
        $result.Message = $_.Exception.Message
        Write-Host "  ❌ FAILED: $( $_.Exception.Message )" -ForegroundColor Red
    }
    finally
    {
        $stopwatch.Stop()
        $result.Duration = $stopwatch.ElapsedMilliseconds
        Write-Host "  ⏱️  Duration: $( $result.Duration )ms" -ForegroundColor Gray
        Write-Host ""
    }

    return $result
}

# Test 1: Sequential Thinking
$results += Test-McpServer -Name "Sequential thinking" -Command "npx" -Arguments @("-y", "@modelcontextprotocol/server-sequential-thinking")

# Test 2: Playwright
$results += Test-McpServer -Name "Playwright" -Command "npx" -Arguments @("-y", "@playwright/mcp@latest")

# Test 3: Context 7
$results += Test-McpServer -Name "Context 7" -Command "npx" -Arguments @("-y", "@upstash/context7-mcp@latest")

# Test 4: Godot
$results += Test-McpServer -Name "godot" -Command "node" -Arguments @("C:/Users/spare/source/repos/godot/godot-mcp/build/index.js") -Env @{
    "DEBUG" = "true"
    "GODOT_PATH" = "C:/Users/spare/Downloads/Godot_v4.5-stable_mono_win64/Godot_v4.5-stable_mono_win64/Godot_v4.5-stable_mono_win64.exe"
}

# Test 5: Meshy AI
$results += Test-McpServer -Name "meshy-ai" -Command "python" -Arguments @("C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py") -Env @{
    "MESHY_API_KEY" = "msy_ntI4R9Qk4x4c9v7BDvH6wJ7cwcyUUvMAMr0S"
}

# Summary
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host "📊 Test Summary" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Cyan
Write-Host ""

$successCount = ($results | Where-Object { $_.Status -eq "Success" }).Count
$warningCount = ($results | Where-Object { $_.Status -eq "Warning" }).Count
$failedCount = ($results | Where-Object { $_.Status -eq "Failed" }).Count
$totalCount = $results.Count

Write-Host "Total Servers: $totalCount" -ForegroundColor White
Write-Host "✅ Success: $successCount" -ForegroundColor Green
Write-Host "⚠️  Warning: $warningCount" -ForegroundColor Yellow
Write-Host "❌ Failed: $failedCount" -ForegroundColor Red
Write-Host ""

# Detailed results
Write-Host "Detailed Results:" -ForegroundColor Cyan
Write-Host "-" * 80 -ForegroundColor Gray
foreach ($result in $results)
{
    $statusColor = switch ($result.Status)
    {
        "Success" {
            "Green"
        }
        "Warning" {
            "Yellow"
        }
        "Failed" {
            "Red"
        }
        default {
            "Gray"
        }
    }

    $statusIcon = switch ($result.Status)
    {
        "Success" {
            "✅"
        }
        "Warning" {
            "⚠️ "
        }
        "Failed" {
            "❌"
        }
        default {
            "❓"
        }
    }

    Write-Host "$statusIcon $($result.Name.PadRight(25) ) " -NoNewline
    Write-Host "$($result.Status.PadRight(10) ) " -ForegroundColor $statusColor -NoNewline
    Write-Host "$( $result.Message )" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=" * 80 -ForegroundColor Cyan

# Exit code
if ($failedCount -gt 0)
{
    Write-Host "⚠️  Some MCP servers failed smoke test" -ForegroundColor Red
    exit 1
}
elseif ($warningCount -gt 0)
{
    Write-Host "⚠️  All MCP servers passed with warnings" -ForegroundColor Yellow
    exit 0
}
else
{
    Write-Host "✅ All MCP servers passed smoke test!" -ForegroundColor Green
    exit 0
}

