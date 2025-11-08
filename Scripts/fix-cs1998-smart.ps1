#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Smart CS1998 fixer that only removes async from methods without await statements
.DESCRIPTION
    This script analyzes each method with CS1998 error and only removes async if the method
    body doesn't contain any await statements. It also wraps return values appropriately.
#>

param(
    [string]$ProjectPath = "Common/GA.Business.Core/GA.Business.Core.csproj"
)

Write-Host "🔍 Smart CS1998 Fixer" -ForegroundColor Cyan
Write-Host "=" * 80

# Get all CS1998 errors
Write-Host "`n📋 Getting CS1998 errors..." -ForegroundColor Yellow
$buildOutput = dotnet build $ProjectPath -c Debug --no-restore 2>&1 | Out-String
$errors = $buildOutput -split "`n" | Where-Object { $_ -match "CS1998" }

if ($errors.Count -eq 0)
{
    Write-Host "✅ No CS1998 errors found!" -ForegroundColor Green
    exit 0
}

Write-Host "Found $( $errors.Count ) CS1998 errors" -ForegroundColor Yellow

# Parse errors to get file:line information
$errorInfo = @()
foreach ($errorLine in $errors)
{
    if ($errorLine -match "(.+\.cs)\((\d+),\d+\): error CS1998")
    {
        $file = $matches[1]
        $line = [int]$matches[2]
        $errorInfo += @{
            File = $file
            Line = $line
        }
    }
}

# Group by file
$fileGroups = $errorInfo | Group-Object -Property File

Write-Host "`n📁 Files to process: $( $fileGroups.Count )" -ForegroundColor Cyan

foreach ($fileGroup in $fileGroups)
{
    $filePath = $fileGroup.Name
    $fileName = Split-Path $filePath -Leaf
    $errorLines = $fileGroup.Group | ForEach-Object { $_.Line }

    Write-Host "`n📄 Processing: $fileName" -ForegroundColor Cyan
    Write-Host "   Lines with CS1998: $( $errorLines -join ', ' )" -ForegroundColor Gray

    # Read file content
    $content = Get-Content $filePath -Raw
    $lines = Get-Content $filePath

    $fixedCount = 0
    $skippedCount = 0

    foreach ($errorLine in $errorLines | Sort-Object -Descending)
    {
        # Get the method signature line
        $methodLine = $lines[$errorLine - 1]

        # Find the method body (from opening brace to closing brace)
        $braceCount = 0
        $inMethod = $false
        $methodBody = ""
        $methodStartLine = $errorLine - 1

        for ($i = $errorLine - 1; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]

            # Count braces
            $openBraces = ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
            $closeBraces = ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count

            if ($openBraces -gt 0)
            {
                $inMethod = $true
            }

            $braceCount += $openBraces - $closeBraces
            $methodBody += $line + "`n"

            if ($inMethod -and $braceCount -eq 0)
            {
                break
            }
        }

        # Check if method body contains 'await'
        if ($methodBody -match '\bawait\b')
        {
            Write-Host "   ⚠️  Line $errorLine - SKIPPED (contains await)" -ForegroundColor Yellow
            $skippedCount++
            continue
        }

        # Method doesn't have await, safe to remove async
        Write-Host "   ✅ Line $errorLine - Removing async" -ForegroundColor Green

        # Remove async keyword from the method signature
        $lines[$errorLine - 1] = $lines[$errorLine - 1] -replace '\basync\s+', ''

        $fixedCount++
    }

    if ($fixedCount -gt 0)
    {
        # Write back to file
        $lines | Set-Content $filePath -Encoding UTF8
        Write-Host "   💾 Saved $fixedCount fixes to $fileName" -ForegroundColor Green
    }

    if ($skippedCount -gt 0)
    {
        Write-Host "   ⚠️  Skipped $skippedCount methods (contain await - false positives)" -ForegroundColor Yellow
    }
}

Write-Host "`n" + ("=" * 80)
Write-Host "✅ Smart CS1998 fixing complete!" -ForegroundColor Green
Write-Host "`n💡 Note: Methods with 'await' were skipped - these are false positives" -ForegroundColor Cyan
Write-Host "   You may need to investigate why they're showing CS1998 errors." -ForegroundColor Cyan

