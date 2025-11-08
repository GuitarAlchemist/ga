# Download Capo Model Script
# This script helps users download the guitar capo 3D model from Sketchfab

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
$Reset = "`e[0m"

function Write-ColorOutput {
    param($Message, $Color = $Reset)
    Write-Host "$Color$Message$Reset"
}

function Show-Help {
    Write-ColorOutput "🎸 Guitar Capo 3D Model Download Helper" $Blue
    Write-Host ""
    Write-ColorOutput "This script helps you download the guitar capo 3D model from Sketchfab." $Yellow
    Write-Host ""
    Write-ColorOutput "MANUAL STEPS REQUIRED:" $Red
    Write-Host "1. Create a free Sketchfab account at https://sketchfab.com"
    Write-Host "2. Visit the model page: https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7"
    Write-Host "3. Click 'Download 3D Model' button"
    Write-Host "4. Select 'glTF' format (.glb preferred)"
    Write-Host "5. Download and extract the ZIP file"
    Write-Host "6. Copy the .glb file to: public/models/guitar-capo.glb"
    Write-Host ""
    Write-ColorOutput "USAGE:" $Blue
    Write-Host "  .\scripts\download-capo-model.ps1 -Info    # Show model information"
    Write-Host "  .\scripts\download-capo-model.ps1 -Help    # Show this help"
    Write-Host ""
    Write-ColorOutput "ATTRIBUTION:" $Yellow
    Write-Host "Model: 'Guitar Capo' by Chad (@cpenfold)"
    Write-Host "License: CC Attribution"
    Write-Host "URL: https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7"
}

function Show-Info {
    Write-ColorOutput "🎸 Guitar Capo 3D Model Information" $Blue
    Write-Host ""
    Write-ColorOutput "Model Details:" $Green
    Write-Host "  Name: Guitar Capo"
    Write-Host "  Author: Chad (@cpenfold)"
    Write-Host "  License: CC Attribution"
    Write-Host "  Triangles: 389.5k"
    Write-Host "  Vertices: 195.9k"
    Write-Host "  Published: June 20th, 2020"
    Write-Host ""
    Write-ColorOutput "Download URL:" $Yellow
    Write-Host "  https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7"
    Write-Host ""
    Write-ColorOutput "Target Location:" $Blue
    Write-Host "  ReactComponents/ga-react-components/public/models/guitar-capo.glb"
    Write-Host ""
    Write-ColorOutput "Test Pages:" $Green
    Write-Host "  http://localhost:5173/test/capo-model"
    Write-Host "  http://localhost:5173/test/three-fretboard"
    Write-Host "  http://localhost:5173/test/minimal-three"
}

function Check-ModelExists {
    $modelPath = "public/models/guitar-capo.glb"
    if (Test-Path $modelPath) {
        $fileSize = (Get-Item $modelPath).Length
        $fileSizeMB = [math]::Round($fileSize / 1MB, 2)
        Write-ColorOutput "✅ Model found: $modelPath ($fileSizeMB MB)" $Green
        return $true
    } else {
        Write-ColorOutput "❌ Model not found: $modelPath" $Red
        return $false
    }
}

function Open-SketchfabPage {
    $url = "https://sketchfab.com/3d-models/guitar-capo-255712235bcd49d6b8bd7fd056868de7"
    Write-ColorOutput "🌐 Opening Sketchfab model page..." $Blue
    Start-Process $url
}

function Main {
    Write-ColorOutput "🎸 Guitar Capo 3D Model Download Helper" $Blue
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

    # Check if model already exists
    if (Check-ModelExists) {
        Write-ColorOutput "Model is already downloaded and ready to use!" $Green
        Write-Host ""
        Write-ColorOutput "Test the model at:" $Blue
        Write-Host "  http://localhost:5173/test/capo-model"
        return
    }

    Write-ColorOutput "Model not found. Manual download required." $Yellow
    Write-Host ""
    Write-ColorOutput "STEPS TO DOWNLOAD:" $Blue
    Write-Host "1. Create a free Sketchfab account (if you don't have one)"
    Write-Host "2. Visit the model page (opening in browser...)"
    Write-Host "3. Click 'Download 3D Model' button"
    Write-Host "4. Select 'glTF' format (.glb preferred)"
    Write-Host "5. Download and extract the ZIP file"
    Write-Host "6. Copy the .glb file to: public/models/guitar-capo.glb"
    Write-Host ""

    $response = Read-Host "Open Sketchfab model page in browser? (y/N)"
    if ($response -eq "y" -or $response -eq "Y") {
        Open-SketchfabPage
    }

    Write-Host ""
    Write-ColorOutput "After downloading, run this script again to verify the model." $Green
    Write-Host ""
    Write-ColorOutput "For more information, see:" $Blue
    Write-Host "  public/models/CAPO_MODEL_SETUP.md"
}

# Run main function
Main
