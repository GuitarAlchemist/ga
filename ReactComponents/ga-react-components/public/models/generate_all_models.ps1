# PowerShell script to generate all Blender models
# Run this script to create all Egyptian-themed 3D models for BSP DOOM Explorer

Write-Host "Generating All Blender Models..." -ForegroundColor Cyan
Write-Host ""

# Check if Blender is installed
$blenderPath = "blender"
try {
    $blenderVersion = & $blenderPath --version 2>&1 | Select-Object -First 1
    Write-Host "[OK] Found Blender: $blenderVersion" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Blender not found in PATH!" -ForegroundColor Red
    Write-Host "Please install Blender and add it to your PATH" -ForegroundColor Yellow
    Write-Host "Download from: https://www.blender.org/download/" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Models to generate
$models = @(
    @{Name="Ankh"; Script="create_ankh.py"; Output="ankh.glb"},
    @{Name="Stele"; Script="create_stele.py"; Output="stele.glb"},
    @{Name="Scarab"; Script="create_scarab.py"; Output="scarab.glb"},
    @{Name="Pyramid"; Script="create_pyramid.py"; Output="pyramid.glb"},
    @{Name="Lotus"; Script="create_lotus.py"; Output="lotus.glb"}
)

$successCount = 0
$failCount = 0

foreach ($model in $models) {
    Write-Host "Generating $($model.Name)..." -ForegroundColor Yellow
    
    $scriptPath = Join-Path $scriptDir $model.Script
    $outputPath = Join-Path $scriptDir $model.Output
    
    # Check if script exists
    if (-not (Test-Path $scriptPath)) {
        Write-Host "   [ERROR] Script not found: $scriptPath" -ForegroundColor Red
        $failCount++
        continue
    }
    
    # Run Blender in background mode
    try {
        & $blenderPath --background --python $scriptPath 2>&1 | Out-Null
        
        # Check if output file was created
        if (Test-Path $outputPath) {
            $fileSize = (Get-Item $outputPath).Length / 1KB
            $fileSizeRounded = [math]::Round($fileSize, 2)
            Write-Host "   [OK] Created: $($model.Output) ($fileSizeRounded KB)" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "   [FAIL] Failed to create: $($model.Output)" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "   [ERROR] Error running Blender: $_" -ForegroundColor Red
        $failCount++
    }
    
    Write-Host ""
}

# Summary
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Generation Summary:" -ForegroundColor Cyan
Write-Host "   [OK] Success: $successCount models" -ForegroundColor Green
if ($failCount -gt 0) {
    Write-Host "   [FAIL] Failed: $failCount models" -ForegroundColor Red
}
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

if ($successCount -gt 0) {
    Write-Host "Models are ready to use in Three.js!" -ForegroundColor Green
    Write-Host "Location: $scriptDir" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Update BSPDoomExplorer.tsx to use the new models" -ForegroundColor White
    Write-Host "2. Update Models3DTest.tsx to display the new models" -ForegroundColor White
    Write-Host "3. Test in the React app: npm run dev" -ForegroundColor White
}

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')

