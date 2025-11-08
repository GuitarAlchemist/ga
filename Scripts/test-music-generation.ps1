#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test the Hugging Face Music Generation API integration

.DESCRIPTION
    This script tests the Music Generation API endpoints by making sample requests
    and saving the generated audio files.

.PARAMETER ApiUrl
    Base URL of the GaApi service (default: http://localhost:5232)

.PARAMETER OutputDir
    Directory to save generated audio files (default: ./test-output)

.EXAMPLE
    .\test-music-generation.ps1

.EXAMPLE
    .\test-music-generation.ps1 -ApiUrl "https://localhost:7001" -OutputDir "./audio-samples"
#>

param(
    [string]$ApiUrl = "http://localhost:5232",
    [string]$OutputDir = "./test-output"
)

$ErrorActionPreference = "Stop"

# Colors for output
$colors = @{
    Info = "Cyan"
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
}

function Write-ColorOutput
{
    param(
        [string]$Message,
        [string]$Type = "Info"
    )
    $color = $colors[$Type]
    if ($null -eq $color)
    {
        $color = "White"
    }
    Write-Host $Message -ForegroundColor $color
}

function Test-ApiEndpoint
{
    param(
        [string]$Url,
        [string]$Name
    )

    Write-ColorOutput "Testing $Name..." "Info"
    try
    {
        $response = Invoke-RestMethod -Uri $Url -Method Get -ErrorAction Stop
        Write-ColorOutput "✓ $Name - OK" "Success"
        return $response
    }
    catch
    {
        Write-ColorOutput "✗ $Name - FAILED: $_" "Error"
        return $null
    }
}

function Test-MusicGeneration
{
    param(
        [string]$Description,
        [int]$Duration = 5,
        [string]$OutputFile
    )

    Write-ColorOutput "`nGenerating: $Description" "Info"
    Write-ColorOutput "Duration: $Duration seconds" "Info"

    $body = @{
        description = $Description
        durationSeconds = $Duration
        temperature = 0.7
    } | ConvertTo-Json

    try
    {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        Invoke-RestMethod -Uri "$ApiUrl/api/MusicGeneration/generate" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -OutFile $OutputFile `
            -ErrorAction Stop

        $sw.Stop()

        if (Test-Path $OutputFile)
        {
            $fileSize = (Get-Item $OutputFile).Length
            Write-ColorOutput "✓ Generated successfully in $($sw.Elapsed.TotalSeconds.ToString('F2') )s" "Success"
            Write-ColorOutput "  File: $OutputFile ($([math]::Round($fileSize/1KB, 2) ) KB)" "Success"
            return $true
        }
        else
        {
            Write-ColorOutput "✗ File not created" "Error"
            return $false
        }
    }
    catch
    {
        Write-ColorOutput "✗ Generation failed: $_" "Error"
        if ($_.Exception.Response)
        {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-ColorOutput "  Response: $responseBody" "Error"
        }
        return $false
    }
}

# Main script
Write-ColorOutput @"

========================================
Hugging Face Music Generation API Test
========================================

"@ "Cyan"

Write-ColorOutput "API URL: $ApiUrl" "Info"
Write-ColorOutput "Output Directory: $OutputDir`n" "Info"

# Create output directory
if (-not (Test-Path $OutputDir))
{
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-ColorOutput "Created output directory: $OutputDir`n" "Success"
}

# Test 1: Health Check
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "Test 1: Health Check" "Cyan"
Write-ColorOutput "========================================" "Cyan"

$health = Test-ApiEndpoint "$ApiUrl/api/MusicGeneration/health" "Health Endpoint"
if ($health)
{
    Write-ColorOutput "Status: $( $health.status )" "Info"
    Write-ColorOutput "Service: $( $health.service )" "Info"
    Write-ColorOutput "Timestamp: $( $health.timestamp )" "Info"
}

# Test 2: List Models
Write-ColorOutput "`n========================================" "Cyan"
Write-ColorOutput "Test 2: List Available Models" "Cyan"
Write-ColorOutput "========================================" "Cyan"

$models = Test-ApiEndpoint "$ApiUrl/api/MusicGeneration/models" "Models Endpoint"
if ($models)
{
    Write-ColorOutput "`nAvailable Models:" "Info"
    foreach ($model in $models)
    {
        $recommended = if ($model.recommended)
        {
            " (Recommended)"
        }
        else
        {
            ""
        }
        Write-ColorOutput "  • $( $model.name )$recommended" "Success"
        Write-ColorOutput "    ID: $( $model.id )" "Info"
        Write-ColorOutput "    Description: $( $model.description )" "Info"
    }
}

# Test 3: Generate Music Samples
Write-ColorOutput "`n========================================" "Cyan"
Write-ColorOutput "Test 3: Generate Music Samples" "Cyan"
Write-ColorOutput "========================================" "Cyan"

Write-ColorOutput "`nNote: First generation may take 20-30 seconds (model loading)" "Warning"
Write-ColorOutput "Subsequent generations will be faster`n" "Warning"

$tests = @(
    @{
        Description = "upbeat blues guitar riff in A minor"
        Duration = 5
        OutputFile = "$OutputDir/blues-guitar.wav"
    },
    @{
        Description = "calm acoustic guitar melody"
        Duration = 5
        OutputFile = "$OutputDir/acoustic-calm.wav"
    },
    @{
        Description = "energetic rock guitar solo"
        Duration = 5
        OutputFile = "$OutputDir/rock-solo.wav"
    }
)

$successCount = 0
$totalTests = $tests.Count

foreach ($test in $tests)
{
    if (Test-MusicGeneration -Description $test.Description -Duration $test.Duration -OutputFile $test.OutputFile)
    {
        $successCount++
    }
    Start-Sleep -Seconds 1  # Brief pause between requests
}

# Summary
Write-ColorOutput "`n========================================" "Cyan"
Write-ColorOutput "Test Summary" "Cyan"
Write-ColorOutput "========================================" "Cyan"

Write-ColorOutput "`nTotal Tests: $totalTests" "Info"
Write-ColorOutput "Successful: $successCount" "Success"
Write-ColorOutput "Failed: $( $totalTests - $successCount )" $( if ($successCount -eq $totalTests)
{
    "Success"
}
else
{
    "Warning"
} )

if ($successCount -eq $totalTests)
{
    Write-ColorOutput "`n✓ All tests passed!" "Success"
    Write-ColorOutput "`nGenerated audio files are in: $OutputDir" "Success"
    Write-ColorOutput "You can play them with any audio player.`n" "Info"
}
else
{
    Write-ColorOutput "`n⚠ Some tests failed. Check the output above for details.`n" "Warning"
}

# Cleanup suggestion
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "Next Steps" "Cyan"
Write-ColorOutput "========================================`n" "Cyan"

Write-ColorOutput "1. Listen to the generated audio files in $OutputDir" "Info"
Write-ColorOutput "2. Set your Hugging Face API token for better performance:" "Info"
Write-ColorOutput "   dotnet user-secrets set 'HuggingFace:ApiToken' 'your-token' --project Apps/ga-server/GaApi/GaApi.csproj" "Info"
Write-ColorOutput "3. Try the Swagger UI at: $ApiUrl/swagger" "Info"
Write-ColorOutput "4. Check the documentation: docs/HuggingFace-Integration.md`n" "Info"

