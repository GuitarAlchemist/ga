#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run the maintained Guitar Alchemist chatbot quality gate.
.DESCRIPTION
    Runs the current chatbot-related backend, API, frontend unit, build, lint,
    and Playwright suites. This intentionally excludes legacy/out-of-solution
    harnesses that target removed APIs or old local ports.
.PARAMETER SkipBuild
    Skip the solution build step.
.PARAMETER BackendOnly
    Run only the .NET test projects.
.PARAMETER FrontendOnly
    Run only frontend build/lint/unit/e2e checks.
.PARAMETER SkipE2E
    Skip Playwright browser tests.
.PARAMETER LiveA2A
    Run live A2A smoke tests against a running chatbot API.
.PARAMETER A2AApiUrl
    Base URL for live A2A smoke tests.
.PARAMETER A2ATimeoutSeconds
    Per-request timeout for live A2A smoke tests.
.PARAMETER OutRoot
    Directory used for isolated .NET build/test outputs.
#>

param(
    [switch]$SkipBuild,
    [switch]$BackendOnly,
    [switch]$FrontendOnly,
    [switch]$SkipE2E,
    [switch]$LiveA2A,
    [string]$A2AApiUrl = "http://localhost:5252",
    [int]$A2ATimeoutSeconds = 120,
    [string]$OutRoot = (Join-Path $env:TEMP "ga-chatbot-quality")
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$frontendDir = Join-Path $repoRoot "Apps/ga-client"

function Invoke-QualityStep {
    param(
        [string]$Name,
        [scriptblock]$Script
    )

    Write-Host ""
    Write-Host "== $Name ==" -ForegroundColor Cyan
    $started = Get-Date
    & $Script
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
    $elapsed = (Get-Date) - $started
    Write-Host "OK: $Name ($($elapsed.TotalSeconds.ToString('F1'))s)" -ForegroundColor Green
}

function Invoke-DotNetTestProject {
    param(
        [string]$Name,
        [string]$ProjectPath
    )

    $outDir = Join-Path $OutRoot $Name
    if (Test-Path $outDir) {
        Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Invoke-QualityStep "dotnet test $Name" {
        dotnet test $ProjectPath -c Debug -m:1 -p:OutDir="$outDir\"
    }
}

Set-Location $repoRoot
New-Item -ItemType Directory -Force -Path $OutRoot | Out-Null

Write-Host "Guitar Alchemist chatbot quality gate" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot"
Write-Host "Output:     $OutRoot"

if (-not $SkipBuild -and -not $FrontendOnly) {
    $buildOut = Join-Path $OutRoot "solution-build"
    if (Test-Path $buildOut) {
        Remove-Item -LiteralPath $buildOut -Recurse -Force -ErrorAction SilentlyContinue
    }

    Invoke-QualityStep "dotnet build AllProjects.slnx" {
        dotnet build AllProjects.slnx -c Debug -m:1 -p:OutDir="$buildOut\"
    }
}

if (-not $FrontendOnly) {
    $testProjects = @(
        @{ Name = "GaChatbot.Api.Tests"; Path = "Tests\Apps\GaChatbot.Api.Tests\GaChatbot.Api.Tests.csproj" },
        @{ Name = "GaApi.Tests"; Path = "Tests\Apps\GaApi.Tests\GaApi.Tests.csproj" },
        @{ Name = "GA.Business.ML.Tests"; Path = "Tests\Common\GA.Business.ML.Tests\GA.Business.ML.Tests.csproj" },
        @{ Name = "GA.Business.Core.Tests"; Path = "Tests\Common\GA.Business.Core.Tests\GA.Business.Core.Tests.csproj" },
        @{ Name = "GA.Business.DSL.Tests"; Path = "Tests\Common\GA.Business.DSL.Tests\GA.Business.DSL.Tests.csproj" },
        @{ Name = "GA.Core.Tests"; Path = "Tests\Common\GA.Core.Tests\GA.Core.Tests.csproj" },
        @{ Name = "GA.MusicTheory.Service.Tests"; Path = "Tests\Apps\GA.MusicTheory.Service.Tests\GA.MusicTheory.Service.Tests.csproj" },
        @{ Name = "GA.InteractiveExtension.Tests"; Path = "Tests\Common\GA.InteractiveExtension\GA.InteractiveExtension.Tests.csproj" }
    )

    foreach ($project in $testProjects) {
        Invoke-DotNetTestProject -Name $project.Name -ProjectPath $project.Path
    }
}

if (-not $BackendOnly) {
    Push-Location $frontendDir
    try {
        Invoke-QualityStep "ga-client npm run build" {
            npm run build
        }

        Invoke-QualityStep "ga-client npm run lint" {
            npm run lint
        }

        Invoke-QualityStep "ga-client targeted vitest" {
            npm test -- --run src/services/agUiChatService.test.ts src/test/performance.test.ts
        }

        if (-not $SkipE2E) {
            Invoke-QualityStep "ga-client Playwright chromium" {
                npx playwright test --project=chromium
            }
        }
    }
    finally {
        Pop-Location
    }
}

if ($LiveA2A) {
    Invoke-QualityStep "live chatbot A2A smoke" {
        pwsh (Join-Path $scriptDir "test-chatbot-a2a.ps1") -ApiUrl $A2AApiUrl -TimeoutSeconds $A2ATimeoutSeconds
    }
}

Write-Host ""
Write-Host "Chatbot quality gate passed." -ForegroundColor Green
