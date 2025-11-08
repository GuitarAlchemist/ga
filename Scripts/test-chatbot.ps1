#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test the Guitar Alchemist chatbot with various queries
.DESCRIPTION
    Runs a comprehensive test suite against the chatbot API to assess:
    - Response accuracy
    - Response time
    - Semantic search integration
    - Error handling
.PARAMETER ApiUrl
    Base URL of the API (default: http://localhost:5232)
.PARAMETER Verbose
    Show detailed output
.EXAMPLE
    .\Scripts\test-chatbot.ps1
.EXAMPLE
    .\Scripts\test-chatbot.ps1 -ApiUrl "https://localhost:7001" -Verbose
#>

param(
    [string]$ApiUrl = "http://localhost:5232",
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

# ANSI color codes
$Green = "`e[32m"
$Red = "`e[31m"
$Yellow = "`e[33m"
$Blue = "`e[34m"
$Reset = "`e[0m"

function Write-TestHeader
{
    param([string]$Message)
    Write-Host "`n$Blue═══════════════════════════════════════════════════════════════$Reset"
    Write-Host "$Blue  $Message$Reset"
    Write-Host "$Blue═══════════════════════════════════════════════════════════════$Reset`n"
}

function Write-Success
{
    param([string]$Message)
    Write-Host "$Green✓ $Message$Reset"
}

function Write-Failure
{
    param([string]$Message)
    Write-Host "$Red✗ $Message$Reset"
}

function Write-Warning
{
    param([string]$Message)
    Write-Host "$Yellow⚠ $Message$Reset"
}

function Test-ChatbotStatus
{
    Write-TestHeader "Testing Chatbot Status"

    try
    {
        $response = Invoke-RestMethod -Uri "$ApiUrl/api/chatbot/status" -Method Get

        if ($response.isAvailable)
        {
            Write-Success "Chatbot is available"
            Write-Host "  Message: $( $response.message )"
            return $true
        }
        else
        {
            Write-Failure "Chatbot is not available"
            Write-Host "  Message: $( $response.message )"
            return $false
        }
    }
    catch
    {
        Write-Failure "Failed to connect to chatbot API"
        Write-Host "  Error: $( $_.Exception.Message )"
        return $false
    }
}

function Test-ChatQuery
{
    param(
        [string]$Query,
        [string]$ExpectedKeywords,
        [bool]$UseSemanticSearch = $false,
        [string]$TestName
    )

    Write-Host "`n${Yellow}Test: $TestName$Reset"
    Write-Host "Query: $Query"
    Write-Host "Semantic Search: $UseSemanticSearch"

    $body = @{
        message = $Query
        useSemanticSearch = $UseSemanticSearch
    } | ConvertTo-Json

    $startTime = Get-Date

    try
    {
        $response = Invoke-RestMethod -Uri "$ApiUrl/api/chatbot/chat" -Method Post -Body $body -ContentType "application/json"
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds

        Write-Host "`nResponse (${duration}s):"
        Write-Host "─────────────────────────────────────────────────────────────"
        Write-Host $response.message
        Write-Host "─────────────────────────────────────────────────────────────"

        # Check for expected keywords
        $foundKeywords = @()
        $missingKeywords = @()

        foreach ($keyword in $ExpectedKeywords -split ',')
        {
            $keyword = $keyword.Trim()
            if ($response.message -match $keyword)
            {
                $foundKeywords += $keyword
            }
            else
            {
                $missingKeywords += $keyword
            }
        }

        if ($foundKeywords.Count -gt 0)
        {
            Write-Success "Found keywords: $( $foundKeywords -join ', ' )"
        }

        if ($missingKeywords.Count -gt 0)
        {
            Write-Warning "Missing keywords: $( $missingKeywords -join ', ' )"
        }

        # Performance assessment
        if ($duration -lt 5)
        {
            Write-Success "Fast response time: ${duration}s"
        }
        elseif ($duration -lt 15)
        {
            Write-Warning "Moderate response time: ${duration}s"
        }
        else
        {
            Write-Failure "Slow response time: ${duration}s"
        }

        return @{
            Success = $true
            Duration = $duration
            Response = $response.message
            FoundKeywords = $foundKeywords
            MissingKeywords = $missingKeywords
        }

    }
    catch
    {
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalSeconds

        Write-Failure "Query failed after ${duration}s"
        Write-Host "  Error: $( $_.Exception.Message )"

        return @{
            Success = $false
            Duration = $duration
            Error = $_.Exception.Message
        }
    }
}

# Main test execution
Write-TestHeader "Guitar Alchemist Chatbot Test Suite"

# Test 1: Check if API is running
if (-not (Test-ChatbotStatus))
{
    Write-Failure "API is not running. Please start the API first:"
    Write-Host "  dotnet run --project Apps/ga-server/GaApi"
    exit 1
}

# Test 2: Basic music theory
$test1 = Test-ChatQuery `
    -Query "What notes are in a C major chord?" `
    -ExpectedKeywords "C,E,G" `
    -UseSemanticSearch $false `
    -TestName "Basic Music Theory (No Semantic Search)"

# Test 3: Guitar technique
$test2 = Test-ChatQuery `
    -Query "Explain barre chords for beginners" `
    -ExpectedKeywords "barre,finger,fret" `
    -UseSemanticSearch $false `
    -TestName "Guitar Technique"

# Test 4: Semantic search integration
$test3 = Test-ChatQuery `
    -Query "Show me some jazz chords" `
    -ExpectedKeywords "chord,jazz" `
    -UseSemanticSearch $true `
    -TestName "Semantic Search Integration"

# Test 5: Scale theory
$test4 = Test-ChatQuery `
    -Query "What is the C major scale?" `
    -ExpectedKeywords "C,D,E,F,G,A,B" `
    -UseSemanticSearch $false `
    -TestName "Scale Theory"

# Test 6: Complex query
$test5 = Test-ChatQuery `
    -Query "What's the difference between major and minor chords?" `
    -ExpectedKeywords "major,minor,third" `
    -UseSemanticSearch $false `
    -TestName "Complex Theory Question"

# Summary
Write-TestHeader "Test Summary"

$tests = @($test1, $test2, $test3, $test4, $test5)
$successCount = ($tests | Where-Object { $_.Success }).Count
$totalCount = $tests.Count
$avgDuration = ($tests | Measure-Object -Property Duration -Average).Average

Write-Host "Total Tests: $totalCount"
Write-Host "Passed: $successCount"
Write-Host "Failed: $( $totalCount - $successCount )"
Write-Host "Average Response Time: $([math]::Round($avgDuration, 2) )s"

if ($successCount -eq $totalCount)
{
    Write-Success "`nAll tests passed!"
}
else
{
    Write-Warning "`nSome tests failed. Check the output above for details."
}

# Recommendations
Write-TestHeader "Recommendations"

if ($avgDuration -gt 10)
{
    Write-Warning "Response times are slow. Consider:"
    Write-Host "  - Using a smaller model (tinyllama:1.1b)"
    Write-Host "  - Enabling GPU acceleration"
    Write-Host "  - Reducing context window size"
}

$allFoundKeywords = $tests | ForEach-Object { $_.FoundKeywords } | Where-Object { $_ }
$allMissingKeywords = $tests | ForEach-Object { $_.MissingKeywords } | Where-Object { $_ }

if ($allMissingKeywords.Count -gt $allFoundKeywords.Count)
{
    Write-Warning "Many expected keywords missing. Consider:"
    Write-Host "  - Using an instruction-tuned model"
    Write-Host "  - Populating semantic search index"
    Write-Host "  - Implementing function calling for facts"
}

Write-Host "`n${Green}Test suite completed!$Reset`n"

