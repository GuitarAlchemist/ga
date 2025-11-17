param(
    [string]$DownloadsDir = "$env:USERPROFILE\Downloads",
    [string]$OutputDir   = "$PSScriptRoot\analysis",
    [string]$SonicVisualiserExe = "C:\\Program Files\\Sonic Visualiser\\sonic-visualiser.exe"
)

$ErrorActionPreference = 'Stop'

Write-Host "=== GA Guitar demo: analyse latest recording ==="

# 1) Locate latest guitar-mix*.webm in the Downloads folder
$pattern = "guitar-mix*.webm"
Write-Host "Searching for latest '$pattern' in $DownloadsDir ..."

if (-not (Test-Path $DownloadsDir)) {
    Write-Error "Downloads directory not found: $DownloadsDir"
}

$latest = Get-ChildItem -Path $DownloadsDir -Filter $pattern -ErrorAction SilentlyContinue |
    Where-Object { -not $_.PSIsContainer } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $latest) {
    Write-Error "No file matching '$pattern' found in $DownloadsDir. Make sure you have downloaded a 'guitar-mix.webm' from the browser first."
}

Write-Host "Using latest recording:" $latest.FullName

# 2) Ensure ffmpeg is available
$ffmpeg = Get-Command ffmpeg -ErrorAction SilentlyContinue
if (-not $ffmpeg) {
    Write-Error "ffmpeg not found in PATH. Please install ffmpeg and ensure 'ffmpeg.exe' is available from PowerShell."
}

# 3) Prepare output directory and WAV path
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$outWav = Join-Path $OutputDir ("{0}.wav" -f $latest.BaseName)

Write-Host "Converting to WAV:" $outWav

# 4) Convert with ffmpeg -> mono 48 kHz 16-bit PCM
& $ffmpeg.Source -y -i $latest.FullName -vn -acodec pcm_s16le -ar 48000 -ac 1 $outWav

# 5) Launch Sonic Visualiser with the resulting WAV (if configured)
if (-not (Test-Path $outWav)) {
    Write-Error "Conversion failed: WAV not found at $outWav"
}

if (Test-Path $SonicVisualiserExe) {
    Write-Host "Launching Sonic Visualiser ..."
    Start-Process $SonicVisualiserExe $outWav
} else {
    Write-Warning "Sonic Visualiser executable not found at '$SonicVisualiserExe'. Edit SonicVisualiserExe at top of this script, or open:`n$outWav` manually in Sonic Visualiser."
}

Write-Host "Done. In Sonic Visualiser: Pane -> Add Spectrogram, puis fais ton screenshot pour ChatGPT."
