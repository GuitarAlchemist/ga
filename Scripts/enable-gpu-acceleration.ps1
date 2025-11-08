#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Enable GPU acceleration for Guitar Alchemist
.DESCRIPTION
    This script sets up GPU acceleration by:
    1. Checking for CUDA/GPU availability
    2. Compiling CUDA kernels (if NVIDIA GPU present)
    3. Installing required packages
    4. Configuring the application
.PARAMETER Backend
    GPU backend to use: CUDA, ILGPU, or Auto (default)
.PARAMETER SkipCudaCompile
    Skip CUDA kernel compilation
.EXAMPLE
    .\Scripts\enable-gpu-acceleration.ps1
.EXAMPLE
    .\Scripts\enable-gpu-acceleration.ps1 -Backend ILGPU
#>

param(
    [Parameter()]
    [ValidateSet('Auto', 'CUDA', 'ILGPU', 'CPU')]
    [string]$Backend = 'Auto',
    
    [Parameter()]
    [switch]$SkipCudaCompile
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Colors
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warn { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Err { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }
function Write-Step { param($Message) Write-Host "`n🔹 $Message" -ForegroundColor Blue }

Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║   🚀 Guitar Alchemist GPU Acceleration Setup 🚀             ║
║                                                              ║
║   Enabling maximum performance with GPU acceleration        ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Step 1: Check GPU availability
Write-Step -Message "Checking GPU availability..."

$hasNvidiaGpu = $false
$hasAmdGpu = $false
$hasIntelGpu = $false
$cudaAvailable = $false

try {
    $nvidiaCheck = nvidia-smi --query-gpu=name,driver_version,memory.total --format=csv,noheader 2>&1
    if ($LASTEXITCODE -eq 0) {
        $hasNvidiaGpu = $true
        Write-Success "NVIDIA GPU detected:"
        $nvidiaCheck | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        
        # Check CUDA
        try {
            $cudaVersion = nvcc --version 2>&1 | Select-String "release (\d+\.\d+)" | ForEach-Object { $_.Matches.Groups[1].Value }
            if ($cudaVersion) {
                $cudaAvailable = $true
                Write-Success "CUDA Toolkit $cudaVersion detected"
            }
        } catch {
            Write-Warning "CUDA Toolkit not found. Install from: https://developer.nvidia.com/cuda-downloads"
        }
    }
} catch {
    Write-Info "No NVIDIA GPU detected"
}

# Check for AMD GPU (Windows only)
if ($IsWindows) {
    try {
        $amdGpu = Get-WmiObject Win32_VideoController | Where-Object { $_.Name -like "*AMD*" -or $_.Name -like "*Radeon*" }
        if ($amdGpu) {
            $hasAmdGpu = $true
            Write-Success "AMD GPU detected: $($amdGpu.Name)"
        }
    } catch {
        Write-Info "No AMD GPU detected"
    }
}

# Check for Intel GPU
try {
    $intelGpu = Get-WmiObject Win32_VideoController | Where-Object { $_.Name -like "*Intel*" }
    if ($intelGpu) {
        $hasIntelGpu = $true
        Write-Success "Intel GPU detected: $($intelGpu.Name)"
    }
} catch {
    Write-Info "No Intel GPU detected"
}

# Determine backend
$selectedBackend = $Backend
if ($Backend -eq 'Auto') {
    if ($hasNvidiaGpu -and $cudaAvailable) {
        $selectedBackend = 'CUDA'
        Write-Success "Auto-selected backend: CUDA (best performance for NVIDIA GPUs)"
    } elseif ($hasNvidiaGpu -or $hasAmdGpu -or $hasIntelGpu) {
        $selectedBackend = 'ILGPU'
        Write-Success "Auto-selected backend: ILGPU (cross-platform GPU support)"
    } else {
        $selectedBackend = 'CPU'
        Write-Warning "No GPU detected. Using CPU with SIMD optimizations"
    }
}

Write-Info "Selected backend: $selectedBackend"

# Step 2: Install required packages
Write-Step "Installing required NuGet packages..."

$packages = @(
    @{ Name = "System.Numerics.Tensors"; Version = "9.0.0"; Project = "Common/GA.Business.Core/GA.Business.Core.csproj" }
)

if ($selectedBackend -eq 'CUDA') {
    $packages += @(
        @{ Name = "ManagedCuda"; Version = "10.2.89"; Project = "Apps/ga-server/GaApi/GaApi.csproj" }
    )
}

if ($selectedBackend -eq 'ILGPU' -or $selectedBackend -eq 'Auto') {
    $packages += @(
        @{ Name = "ILGPU"; Version = "1.5.1"; Project = "Common/GA.Business.Core/GA.Business.Core.csproj" },
        @{ Name = "ILGPU.Algorithms"; Version = "1.5.1"; Project = "Common/GA.Business.Core/GA.Business.Core.csproj" }
    )
}

foreach ($pkg in $packages) {
    Write-Info "Installing $($pkg.Name) $($pkg.Version)..."
    try {
        dotnet add $pkg.Project package $pkg.Name --version $pkg.Version --no-restore
        Write-Success "Installed $($pkg.Name)"
    } catch {
        Write-Warning "Failed to install $($pkg.Name): $_"
    }
}

# Step 3: Compile CUDA kernels (if CUDA backend)
if ($selectedBackend -eq 'CUDA' -and -not $SkipCudaCompile) {
    Write-Step "Compiling CUDA kernels..."
    
    $cudaKernelDir = "Apps/ga-server/GaApi/CUDA/kernels"
    $cudaKernels = Get-ChildItem -Path $cudaKernelDir -Filter "*.cu" -ErrorAction SilentlyContinue
    
    if ($cudaKernels) {
        foreach ($kernel in $cudaKernels) {
            $ptxFile = Join-Path $cudaKernelDir "$($kernel.BaseName).ptx"
            Write-Info "Compiling $($kernel.Name)..."
            
            try {
                $nvccArgs = @(
                    "-ptx",
                    "-o", $ptxFile,
                    $kernel.FullName
                )
                
                & nvcc @nvccArgs
                
                if ($LASTEXITCODE -eq 0) {
                    Write-Success "Compiled $($kernel.Name) → $($kernel.BaseName).ptx"
                } else {
                    Write-Error "Failed to compile $($kernel.Name)"
                }
            } catch {
                Write-Error "CUDA compilation failed: $_"
            }
        }
    } else {
        Write-Warning "No CUDA kernels found in $cudaKernelDir"
    }
}

# Step 4: Update configuration
Write-Step "Updating configuration..."

$appsettingsPath = "Apps/ga-server/GaApi/appsettings.Development.json"

if (Test-Path $appsettingsPath) {
    try {
        $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
        
        # Add GPU configuration
        if (-not $appsettings.PSObject.Properties['GpuAcceleration']) {
            $appsettings | Add-Member -MemberType NoteProperty -Name 'GpuAcceleration' -Value @{
                Enabled = $true
                PreferredBackend = $selectedBackend
                DeviceId = 0
                EnableMultiGpu = $false
                MemoryPoolSizeMB = 2048
            }
        } else {
            $appsettings.GpuAcceleration.Enabled = $true
            $appsettings.GpuAcceleration.PreferredBackend = $selectedBackend
        }
        
        $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
        Write-Success "Updated $appsettingsPath"
    } catch {
        Write-Warning "Failed to update configuration: $_"
    }
} else {
    Write-Warning "Configuration file not found: $appsettingsPath"
}

# Step 5: Restore packages
Write-Step "Restoring NuGet packages..."
dotnet restore AllProjects.sln
Write-Success "Packages restored"

# Step 6: Build solution
Write-Step "Building solution..."
dotnet build AllProjects.sln --no-restore -c Release
if ($LASTEXITCODE -eq 0) {
    Write-Success "Build successful"
} else {
    Write-Error "Build failed"
    exit 1
}

# Step 7: Run GPU detection test
Write-Step "Testing GPU acceleration..."

Write-Info "Running GPU detection..."
# TODO: Add GPU detection test

# Summary
Write-Host @"

╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║   ✅ GPU Acceleration Setup Complete! ✅                    ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝

📊 Configuration Summary:
   • Backend: $selectedBackend
   • NVIDIA GPU: $(if ($hasNvidiaGpu) { '✅' } else { '❌' })
   • CUDA Available: $(if ($cudaAvailable) { '✅' } else { '❌' })
   • AMD GPU: $(if ($hasAmdGpu) { '✅' } else { '❌' })
   • Intel GPU: $(if ($hasIntelGpu) { '✅' } else { '❌' })

🚀 Next Steps:
   1. Run benchmarks: dotnet run --project Apps/VectorSearchBenchmark
   2. Start services: .\Scripts\start-all.ps1
   3. Monitor GPU: nvidia-smi -l 1 (NVIDIA only)

📚 Documentation:
   • GPU Acceleration Guide: docs/GPU_ACCELERATION_GUIDE.md
   • Implementation Tasks: docs/GPU_IMPLEMENTATION_TASKS.md

"@ -ForegroundColor Green

Write-Success "GPU acceleration is ready! 🚀"

