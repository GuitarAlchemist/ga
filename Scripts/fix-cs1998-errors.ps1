# Script to fix CS1998 errors by removing async keyword from methods without await
# This script processes the build output and fixes the errors automatically

param(
    [string]$ProjectPath = "Common/GA.Business.Core/GA.Business.Core.csproj"
)

Write-Host "Building project to identify CS1998 errors..." -ForegroundColor Cyan
$buildOutput = dotnet build $ProjectPath -c Debug --no-restore 2>&1 | Out-String

# Extract CS1998 errors
$errors = $buildOutput | Select-String "error CS1998" | ForEach-Object {
    if ($_ -match '([^\\]+\.cs)\((\d+),\d+\):')
    {
        [PSCustomObject]@{
            File = $Matches[1]
            Line = [int]$Matches[2]
            FullPath = $_ -replace '.*?(C:\\[^:]+\.cs).*', '$1'
        }
    }
} | Where-Object { $_.File -ne $null } | Sort-Object FullPath, Line -Unique

Write-Host "Found $( $errors.Count ) CS1998 errors" -ForegroundColor Yellow

# Group by file
$fileGroups = $errors | Group-Object FullPath

foreach ($fileGroup in $fileGroups)
{
    $filePath = $fileGroup.Name
    Write-Host "`nProcessing $filePath..." -ForegroundColor Green

    $content = Get-Content $filePath -Raw
    $lines = Get-Content $filePath

    # Process each error line in reverse order (to maintain line numbers)
    $sortedErrors = $fileGroup.Group | Sort-Object Line -Descending

    foreach ($error in $sortedErrors)
    {
        $lineNum = $error.Line - 1  # Convert to 0-based index
        $line = $lines[$lineNum]

        Write-Host "  Line $( $error.Line ): $($line.Trim() )" -ForegroundColor Gray

        # Remove 'async ' from the method signature
        if ($line -match '\basync\s+Task')
        {
            $lines[$lineNum] = $line -replace '\basync\s+Task', 'Task'
            Write-Host "    → Removed 'async' keyword" -ForegroundColor DarkGreen
        }
    }

    # Save the modified content
    $lines | Set-Content $filePath -Encoding UTF8
    Write-Host "  ✓ Saved changes to $filePath" -ForegroundColor Green
}

Write-Host "`n✓ Fixed async keywords in $( $fileGroups.Count ) files" -ForegroundColor Green
Write-Host "`nNow you need to manually add Task.FromResult() or Task.CompletedTask to return statements." -ForegroundColor Yellow
Write-Host "Building again to verify..." -ForegroundColor Cyan

dotnet build $ProjectPath -c Debug --no-restore

