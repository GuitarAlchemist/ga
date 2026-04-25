# start-dev.ps1 — Start GA development stack (GaApi + Vite)
# Run with: pwsh Scripts/start-dev.ps1

$ErrorActionPreference = "Continue"
$repoRoot = Split-Path $PSScriptRoot -Parent

Write-Host "Starting GA Dev Stack..." -ForegroundColor Cyan

# Kill any existing instances
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match "GaApi" } | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process -Name "GaApi" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2

# Build GaApi to the current slot target, then launch the slot exe directly.
# `dotnet run` doesn't work here because the slot OutputPath override (in
# Directory.Build.targets) doesn't reach TargetPath, so dotnet run looks at
# bin\Debug\net10.0\ instead of bin\<slot>\net10.0\ and fails. We respect the
# existing .slot-target value rather than forcing it — the deploy workflow
# (ga-build.ps1) controls slot semantics; dev just builds to whatever's set.
. "$repoRoot\Scripts\lib\SlotState.ps1"
$slot = Get-SlotTarget
if (-not $slot) { $slot = "blue" }  # fallback if .slot-target is missing
Write-Host "  [Build] GaApi -> $slot slot..." -ForegroundColor Cyan
dotnet build "$repoRoot\Apps\ga-server\GaApi\GaApi.csproj" -c Debug | Out-Null
$gaApiExe = "$repoRoot\Apps\ga-server\GaApi\bin\$slot\net10.0\GaApi.exe"
if (-not (Test-Path $gaApiExe)) {
    Write-Host "  [Build] FAILED — $gaApiExe not found" -ForegroundColor Red
    exit 1
}

# Start GaApi
$gaApiJob = Start-Job -ScriptBlock {
    Set-Location "$using:repoRoot\Apps\ga-server\GaApi"
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:5232"
    & "$using:gaApiExe" 2>&1
}
Write-Host "  GaApi starting (Job $($gaApiJob.Id))..." -ForegroundColor Green

# Start Vite
$viteJob = Start-Job -ScriptBlock {
    Set-Location "$using:repoRoot/ReactComponents/ga-react-components"
    npx vite --port 5176 --host 2>&1
}
Write-Host "  Vite starting (Job $($viteJob.Id))..." -ForegroundColor Green

# Wait for GaApi
Start-Sleep 10
$status = try { (Invoke-RestMethod http://localhost:5232/api/chatbot/status).isAvailable } catch { $false }
if ($status) {
    Write-Host "  GaApi: READY on http://localhost:5232" -ForegroundColor Green
} else {
    Write-Host "  GaApi: Still starting..." -ForegroundColor Yellow
}

# Wait for Vite
Start-Sleep 3
$viteUp = try { (Invoke-WebRequest http://localhost:5176 -TimeoutSec 5).StatusCode -eq 200 } catch { $false }
if ($viteUp) {
    Write-Host "  Vite: READY on http://localhost:5176" -ForegroundColor Green
} else {
    Write-Host "  Vite: Still starting..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Dev stack running. Press Ctrl+C to stop all." -ForegroundColor Cyan
Write-Host "  API:  http://localhost:5232" -ForegroundColor White
Write-Host "  Vite: http://localhost:5176" -ForegroundColor White
Write-Host ""

# Keep alive and monitor
try {
    while ($true) {
        Start-Sleep 30
        # Check GaApi
        if ($gaApiJob.State -ne "Running") {
            Write-Host "  [!] GaApi died, restarting..." -ForegroundColor Red
            $gaApiJob = Start-Job -ScriptBlock {
                Set-Location "$using:repoRoot\Apps\ga-server\GaApi"
                $env:ASPNETCORE_ENVIRONMENT = "Development"
                $env:ASPNETCORE_URLS = "http://localhost:5232"
                & "$using:gaApiExe" 2>&1
            }
        }
        # Check Vite
        if ($viteJob.State -ne "Running") {
            Write-Host "  [!] Vite died, restarting..." -ForegroundColor Red
            $viteJob = Start-Job -ScriptBlock {
                Set-Location "$using:repoRoot/ReactComponents/ga-react-components"
                npx vite --port 5176 --host 2>&1
            }
        }
    }
} finally {
    Write-Host "Stopping dev stack..." -ForegroundColor Yellow
    Stop-Job $gaApiJob -ErrorAction SilentlyContinue
    Stop-Job $viteJob -ErrorAction SilentlyContinue
    Remove-Job $gaApiJob -ErrorAction SilentlyContinue
    Remove-Job $viteJob -ErrorAction SilentlyContinue
}
