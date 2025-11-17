param(
    [string]$InputWav = "playwright-downloads\guitar-mix.wav",
    [string]$OutputWav = "playwright-downloads\guitar-mix-ddsp.wav",
    [string]$CkptDir
)

$ErrorActionPreference = 'Stop'

Write-Host "=== GA Guitar demo: apply DDSP coloration ==="

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
Set-Location $scriptRoot

# Note: -CkptDir is optional; when omitted, the Python script may fall back to a non-DDSP spectral coloration.

$inputFull = Resolve-Path $InputWav -ErrorAction SilentlyContinue
if (-not $inputFull) {
    Write-Error "Input WAV not found at '$InputWav'. Run full-auto.ps1 first to generate guitar-mix.wav."
}
$inputFull = $inputFull.Path

$ckptFull = $null
if ($CkptDir) {
    $ckptFull = Resolve-Path $CkptDir -ErrorAction SilentlyContinue
    if (-not $ckptFull) {
        Write-Warning "Checkpoint directory not found: '$CkptDir' (DDSP script may fall back to spectral mode)."
        $ckptFull = $null
    } else {
        $ckptFull = $ckptFull.Path
    }
}

# Run DDSP timbre coloration via WSL (Ubuntu) using a Python venv at ~/.venvs/ddsp
$ddspScriptWin = Join-Path $scriptRoot 'scripts\run_ddsp_color.py'
if (-not (Test-Path $ddspScriptWin)) {
    Write-Error "DDSP coloration script not found at $ddspScriptWin"
}

$toWsl = {
    param($p)
    $p2 = $p -replace '\\','/'
    if ($p2 -match '^(?<d>[A-Za-z]):(.*)$') {
        $drive = $matches['d'].ToLower()
        "/mnt/$drive$($matches[2])"
    } else {
        $p2
    }
}

$maxSeconds = 4.0

$inputWsl   = & $toWsl $inputFull
$outputFull = [System.IO.Path]::GetFullPath($OutputWav)
$outputWsl  = & $toWsl $outputFull
$ckptArg    = ""
if ($ckptFull) {
    $ckptWsl = & $toWsl $ckptFull
    $ckptArg = "--ckpt_dir '$ckptWsl'"
}
$scriptWsl  = & $toWsl $ddspScriptWin

Write-Host "Input WAV : $inputFull"
Write-Host "Output WAV: $OutputWav"
if ($ckptFull) {
    Write-Host "Checkpoint: $ckptFull"
} else {
    Write-Host "Checkpoint: (none / spectral fallback)"
}

$wslCmd = "source ~/.venvs/ddsp/bin/activate && python '$scriptWsl' --input '$inputWsl' --output '$outputWsl' $ckptArg --max_seconds $maxSeconds"
wsl -e bash -lc "$wslCmd"

if ($LASTEXITCODE -ne 0) {
    Write-Error "DDSP coloration via WSL failed with exit code $LASTEXITCODE."
} else {
    Write-Host "DDSP coloration via WSL completed. Output written to $OutputWav"
}

return

$python = $null
try {
    $python = Get-Command python -ErrorAction Stop
} catch {
    Write-Error "Python not found on PATH. Install Python 3 and ensure 'python' is available."
}

$ddspScript = Join-Path $scriptRoot 'scripts\run_ddsp_color.py'
if (-not (Test-Path $ddspScript)) {
    Write-Error "DDSP coloration script not found at $ddspScript"
}

$maxSeconds = 4.0

Write-Host "Input WAV : $inputFull"
Write-Host "Output WAV: $OutputWav"
Write-Host "Checkpoint: $ckptFull"

& $python.Source $ddspScript --input $inputFull --output $OutputWav --ckpt_dir $ckptFull --max_seconds $maxSeconds

if ($LASTEXITCODE -ne 0) {
    Write-Error "DDSP coloration failed with exit code $LASTEXITCODE."
} else {
    Write-Host "DDSP coloration completed. Output written to $OutputWav"
}

