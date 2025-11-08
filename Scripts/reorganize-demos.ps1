#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Reorganize demo projects in the solution file
.DESCRIPTION
    This script reorganizes demo projects into logical solution folders:
    - Demos/Music Theory
    - Demos/Performance & Benchmarks
    - Demos/Advanced Features
    - Tools & Utilities
#>

param(
    [string]$SolutionFile = "AllProjects.sln",
    [switch]$DryRun
)

Write-Host "🎯 Demo Project Reorganization Script" -ForegroundColor Cyan
Write-Host "=" * 80

if ($DryRun)
{
    Write-Host "⚠️  DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
}

# Backup the solution file
$backupFile = "$SolutionFile.backup-$( Get-Date -Format 'yyyyMMdd-HHmmss' )"
if (-not $DryRun)
{
    Copy-Item $SolutionFile $backupFile
    Write-Host "✅ Created backup: $backupFile" -ForegroundColor Green
}

# Read the solution file
$content = Get-Content $SolutionFile -Raw

# Define the new folder structure
$folders = @{
    "Demos" = @{
        GUID = "{D3E4F5A6-B7C8-4D9E-0F1A-2B3C4D5E6F7A}"
        SubFolders = @{
            "Music Theory" = @{
                GUID = "{E4F5A6B7-C8D9-4E0F-1A2B-3C4D5E6F7A8B}"
                Projects = @(
                    "ChordNamingDemo",
                    "FretboardChordTest",
                    "FretboardExplorer",
                    "PsychoacousticVoicingDemo",
                    "MusicalAnalysisApp",
                    "PracticeRoutineDSLDemo"
                )
            }
            "Performance & Benchmarks" = @{
                GUID = "{F5A6B7C8-D9E0-4F1A-2B3C-4D5E6F7A8B9C}"
                Projects = @(
                    "VectorSearchBenchmark",
                    "GpuBenchmark",
                    "PerformanceOptimizationDemo"
                )
            }
            "Advanced Features" = @{
                GUID = "{A6B7C8D9-E0F1-4A2B-3C4D-5E6F7A8B9C0D}"
                Projects = @(
                    "AdvancedMathematicsDemo",
                    "BSPDemo",
                    "InternetContentDemo"
                )
            }
        }
    }
    "Tools & Utilities" = @{
        GUID = "{B7C8D9E0-F1A2-4B3C-4D5E-6F7A8B9C0D1E}"
        Projects = @(
            "MongoImporter",
            "MongoVerify",
            "EmbeddingGenerator",
            "LocalEmbedding",
            "GaDataCLI"
        )
    }
}

Write-Host "`n📊 Proposed Organization:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Demos/" -ForegroundColor Yellow
Write-Host "  ├─ Music Theory/" -ForegroundColor Yellow
foreach ($proj in $folders["Demos"].SubFolders["Music Theory"].Projects)
{
    Write-Host "  │  ├─ $proj" -ForegroundColor Gray
}
Write-Host "  ├─ Performance & Benchmarks/" -ForegroundColor Yellow
foreach ($proj in $folders["Demos"].SubFolders["Performance & Benchmarks"].Projects)
{
    Write-Host "  │  ├─ $proj" -ForegroundColor Gray
}
Write-Host "  └─ Advanced Features/" -ForegroundColor Yellow
foreach ($proj in $folders["Demos"].SubFolders["Advanced Features"].Projects)
{
    Write-Host "     ├─ $proj" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Tools & Utilities/" -ForegroundColor Yellow
foreach ($proj in $folders["Tools & Utilities"].Projects)
{
    Write-Host "  ├─ $proj" -ForegroundColor Gray
}

if ($DryRun)
{
    Write-Host "`n⚠️  DRY RUN - No changes made" -ForegroundColor Yellow
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Yellow
    exit 0
}

Write-Host "`n❓ This will reorganize the solution file." -ForegroundColor Yellow
Write-Host "   A backup has been created at: $backupFile" -ForegroundColor Gray
$response = Read-Host "Continue? (y/N)"

if ($response -ne 'y' -and $response -ne 'Y')
{
    Write-Host "❌ Cancelled by user" -ForegroundColor Red
    Remove-Item $backupFile
    exit 1
}

Write-Host "`n🔧 Reorganizing solution..." -ForegroundColor Cyan

# Note: Actual solution file modification would go here
# For safety, we'll use Visual Studio's solution manipulation APIs or dotnet sln commands
# Manual .sln editing is error-prone

Write-Host "`n⚠️  Manual Step Required:" -ForegroundColor Yellow
Write-Host ""
Write-Host "To complete the reorganization, please:" -ForegroundColor White
Write-Host "1. Open AllProjects.sln in Visual Studio or Rider" -ForegroundColor Gray
Write-Host "2. Create the following solution folders:" -ForegroundColor Gray
Write-Host "   - Demos" -ForegroundColor Gray
Write-Host "     - Music Theory" -ForegroundColor Gray
Write-Host "     - Performance & Benchmarks" -ForegroundColor Gray
Write-Host "     - Advanced Features" -ForegroundColor Gray
Write-Host "   - Tools & Utilities" -ForegroundColor Gray
Write-Host "3. Drag and drop projects into their respective folders" -ForegroundColor Gray
Write-Host "4. Save the solution" -ForegroundColor Gray
Write-Host ""
Write-Host "Or use the following dotnet sln commands:" -ForegroundColor White
Write-Host ""

# Generate dotnet sln commands
Write-Host "# Create solution folders (requires manual IDE operation)" -ForegroundColor Green
Write-Host "# Then move projects:" -ForegroundColor Green
Write-Host ""

foreach ($folderName in $folders.Keys)
{
    $folder = $folders[$folderName]
    if ( $folder.ContainsKey("SubFolders"))
    {
        foreach ($subFolderName in $folder.SubFolders.Keys)
        {
            $subFolder = $folder.SubFolders[$subFolderName]
            foreach ($project in $subFolder.Projects)
            {
                Write-Host "# Move $project to Demos/$subFolderName" -ForegroundColor Gray
            }
        }
    }
    else
    {
        foreach ($project in $folder.Projects)
        {
            Write-Host "# Move $project to $folderName" -ForegroundColor Gray
        }
    }
}

Write-Host "`n✅ Backup created: $backupFile" -ForegroundColor Green
Write-Host "📝 Please complete the manual steps above" -ForegroundColor Cyan

