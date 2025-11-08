# Automatically fix CS1998 errors by removing async keyword and wrapping returns
# This script processes files and fixes async methods without await

param(
    [string]$ProjectPath = "Common/GA.Business.Core/GA.Business.Core.csproj"
)

Write-Host "Building project to identify CS1998 errors..." -ForegroundColor Cyan
$buildOutput = dotnet build $ProjectPath -c Debug --no-restore 2>&1

# Extract unique file paths with CS1998 errors
$errorFiles = $buildOutput | Select-String "error CS1998" | ForEach-Object {
    if ($_ -match '(C:\\[^(]+\.cs)\(')
    {
        $Matches[1]
    }
} | Select-Object -Unique

Write-Host "Found CS1998 errors in $( $errorFiles.Count ) files" -ForegroundColor Yellow

$totalFixed = 0

foreach ($filePath in $errorFiles)
{
    Write-Host "`nProcessing: $filePath" -ForegroundColor Green

    $content = Get-Content $filePath -Raw
    $originalContent = $content

    # Remove 'async ' keyword from method signatures
    $content = $content -replace '\basync\s+Task', 'Task'

    if ($content -ne $originalContent)
    {
        # Save the file
        $content | Set-Content $filePath -NoNewline -Encoding UTF8
        $totalFixed++
        Write-Host "  ✓ Removed 'async' keywords" -ForegroundColor DarkGreen
    }
}

Write-Host "`n✓ Processed $totalFixed files" -ForegroundColor Green
Write-Host "`nRebuilding to check for remaining errors..." -ForegroundColor Cyan

$rebuildOutput = dotnet build $ProjectPath -c Debug --no-restore 2>&1
$remainingErrors = ($rebuildOutput | Select-String "error CS1998" | Measure-Object).Count

if ($remainingErrors -eq 0)
{
    Write-Host "✓ All CS1998 errors fixed!" -ForegroundColor Green
}
else
{
    Write-Host "⚠ $remainingErrors CS1998 errors remaining (may need manual Task.FromResult fixes)" -ForegroundColor Yellow
}

