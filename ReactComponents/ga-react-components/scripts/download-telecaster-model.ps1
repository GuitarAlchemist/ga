# Download Fender Telecaster Model Script
# This script helps users download the Fender Telecaster 3D model from Sketchfab for slot 2

param(
    [switch]$Help,
    [switch]$Info
)

$ErrorActionPreference = "Stop"

# Colors for output
$Green = "`e[32m"
$Yellow = "`e[33m"
$Red = "`e[31m"
$Blue = "`e[34m"
$Cyan = "`e[36m"
$Reset = "`e[0m"

function Write-ColorOutput {
    param($Message, $Color = $Reset)
    Write-Host "$Color$Message$Reset"
}

function Show-Help {
    Write-ColorOutput "🎸 Fender Telecaster 3D Model Download Helper" $Blue
    Write-Host ""
    Write-ColorOutput "This script helps you download the Fender Telecaster 3D model from Sketchfab for Guitar Model Slot 2." $Yellow
    Write-Host ""
    Write-ColorOutput "MANUAL STEPS REQUIRED:" $Red
    Write-Host "1. Create a free Sketchfab account at https://sketchfab.com"
    Write-Host "2. Visit the model page: https://sketchfab.com/3d-models/fender-telecaster-f2b583f97def4b1d954bda871f65eaf2"
    Write-Host "3. Click 'Download 3D Model' button"
    Write-Host "4. Select 'glTF' format (.glb preferred)"
    Write-Host "5. Download and extract the ZIP file"
    Write-Host "6. Copy the .glb file to: public/models/guitar2.glb"
    Write-Host ""
    Write-ColorOutput "USAGE:" $Blue
    Write-Host "  .\scripts\download-telecaster-model.ps1 -Info    # Show model information"
    Write-Host "  .\scripts\download-telecaster-model.ps1 -Help    # Show this help"
    Write-Host ""
    Write-ColorOutput "ATTRIBUTION:" $Yellow
    Write-Host "Model: 'Fender Telecaster' by Sladegeorg"
    Write-Host "URL: https://sketchfab.com/3d-models/fender-telecaster-f2b583f97def4b1d954bda871f65eaf2"
}

function Show-Info {
    Write-ColorOutput "🎸 Fender Telecaster 3D Model Information" $Blue
    Write-Host ""
    Write-ColorOutput "Model Details:" $Green
    Write-Host "  Name: Fender Telecaster"
    Write-Host "  Author: Sladegeorg"
    Write-Host "  Triangles: 25.4k"
    Write-Host "  Vertices: 20.5k"
    Write-Host "  Published: August 29th, 2017"
    Write-Host "  Views: 25,167"
    Write-Host "  Likes: 276"
    Write-Host ""
    Write-ColorOutput "Download URL:" $Yellow
    Write-Host "  https://sketchfab.com/3d-models/fender-telecaster-f2b583f97def4b1d954bda871f65eaf2"
    Write-Host ""
    Write-ColorOutput "Target Location:" $Blue
    Write-Host "  ReactComponents/ga-react-components/public/models/guitar2.glb"
    Write-Host ""
    Write-ColorOutput "Guitar Model Slot:" $Cyan
    Write-Host "  Slot 2 - Electric Guitar (Fender Telecaster)"
    Write-Host ""
    Write-ColorOutput "Test Pages:" $Green
    Write-Host "  http://localhost:5173/test/guitar-3d"
    Write-Host "  http://localhost:5173/test/three-fretboard"
}

function Check-ModelExists {
    $modelPath = "public/models/guitar2.glb"
    if (Test-Path $modelPath) {
        $fileSize = (Get-Item $modelPath).Length
        $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
        Write-ColorOutput "✅ Telecaster model found: $modelPath ($fileSizeMB MB)" $Green
        return $true
    } else {
        Write-ColorOutput "❌ Telecaster model not found: $modelPath" $Red
        return $false
    }
}

function Check-AllModels {
    Write-ColorOutput "🎸 Guitar Model Slots Status:" $Blue
    Write-Host ""
    
    $models = @(
        @{ Slot = 1; Name = "Classical Guitar"; Path = "public/models/guitar.glb"; Description = "Classical/Acoustic Guitar" },
        @{ Slot = 2; Name = "Fender Telecaster"; Path = "public/models/guitar2.glb"; Description = "Electric Guitar (Telecaster)" },
        @{ Slot = 3; Name = "Electric Guitar"; Path = "public/models/guitar3.glb"; Description = "Electric Guitar (TBD)" }
    )
    
    foreach ($model in $models) {
        $status = if (Test-Path $model.Path) { "✅ Found" } else { "❌ Missing" }
        $color = if (Test-Path $model.Path) { $Green } else { $Red }
        
        Write-ColorOutput "  Slot $($model.Slot): $($model.Name)" $Cyan
        Write-ColorOutput "    Status: $status" $color
        Write-Host "    Description: $($model.Description)"
        Write-Host "    Path: $($model.Path)"
        
        if (Test-Path $model.Path) {
            $fileSize = (Get-Item $model.Path).Length
            $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
            Write-Host "    Size: $fileSizeMB MB"
        }
        Write-Host ""
    }
}

function Open-SketchfabPage {
    $url = "https://sketchfab.com/3d-models/fender-telecaster-f2b583f97def4b1d954bda871f65eaf2"
    Write-ColorOutput "🌐 Opening Sketchfab Telecaster model page..." $Blue
    Start-Process $url
}

function Main {
    Write-ColorOutput "🎸 Fender Telecaster 3D Model Download Helper" $Blue
    Write-Host ""

    if ($Help) {
        Show-Help
        return
    }

    if ($Info) {
        Show-Info
        return
    }

    # Check if we're in the right directory
    if (-not (Test-Path "public/models")) {
        Write-ColorOutput "❌ Error: public/models directory not found." $Red
        Write-Host "Please run this script from the ReactComponents/ga-react-components directory."
        Write-Host ""
        Write-ColorOutput "Current directory: $(Get-Location)" $Yellow
        Write-ColorOutput "Expected directory: ReactComponents/ga-react-components" $Yellow
        exit 1
    }

    # Show all model slots status
    Check-AllModels

    # Check if Telecaster model already exists
    if (Check-ModelExists) {
        Write-ColorOutput "Fender Telecaster model is already downloaded and ready to use!" $Green
        Write-Host ""
        Write-ColorOutput "Test the model at:" $Blue
        Write-Host "  http://localhost:5173/test/guitar-3d"
        return
    }

    Write-ColorOutput "Telecaster model not found. Manual download required." $Yellow
    Write-Host ""
    Write-ColorOutput "STEPS TO DOWNLOAD:" $Blue
    Write-Host "1. Create a free Sketchfab account (if you don't have one)"
    Write-Host "2. Visit the Telecaster model page (opening in browser...)"
    Write-Host "3. Click 'Download 3D Model' button"
    Write-Host "4. Select 'glTF' format (.glb preferred)"
    Write-Host "5. Download and extract the ZIP file"
    Write-Host "6. Copy the .glb file to: public/models/guitar2.glb"
    Write-Host ""

    Write-ColorOutput "IMPORTANT:" $Red
    Write-Host "Make sure to rename the downloaded file to exactly 'guitar2.glb'"
    Write-Host "This will replace the placeholder in Guitar Model Slot 2"
    Write-Host ""

    $response = Read-Host "Open Sketchfab Telecaster model page in browser? (y/N)"
    if ($response -eq "y" -or $response -eq "Y") {
        Open-SketchfabPage
    }

    Write-Host ""
    Write-ColorOutput "After downloading, run this script again to verify the model." $Green
    Write-Host ""
    Write-ColorOutput "For more information, see:" $Blue
    Write-Host "  public/models/README.md"
}

# Run main function
Main
