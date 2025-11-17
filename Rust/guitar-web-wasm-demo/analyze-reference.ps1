param(
    [string]$RefPath = "reference\by-the-lake.wav",
    [string]$SynthReportPath = ""
)
$ErrorActionPreference = 'Stop'
Write-Host "=== GA Guitar demo: reference vs synth analysis ==="
$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $SynthReportPath) { $SynthReportPath = Join-Path $scriptRoot 'playwright-downloads\iteration-report.json' }
if (-not (Test-Path $SynthReportPath)) { Write-Error "Synth iteration report not found at '$SynthReportPath'. Run full-auto.ps1 first." }
if (-not (Test-Path $RefPath)) {
    $candidate = Join-Path $scriptRoot $RefPath
    if (Test-Path $candidate) { $RefPath = $candidate } else { Write-Error "Reference WAV not found at '$RefPath'." }
}
$refFull = (Resolve-Path $RefPath).Path
$synthReportFull = (Resolve-Path $SynthReportPath).Path

function Get-WavStats {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    $fs = [System.IO.File]::Open($Path,[System.IO.FileMode]::Open,[System.IO.FileAccess]::Read,[System.IO.FileShare]::Read)
    $br = New-Object System.IO.BinaryReader($fs)
    try {
        $enc = [System.Text.Encoding]::ASCII
        if ($enc.GetString($br.ReadBytes(4)) -ne 'RIFF') { throw 'Not RIFF' }
        $br.ReadUInt32() | Out-Null
        if ($enc.GetString($br.ReadBytes(4)) -ne 'WAVE') { throw 'Not WAVE' }
        $bitsPerSample = 16; $channels = 1; $sampleRate = 48000; $dataSize = 0; $dataStart = 0
        while ($fs.Position -lt $fs.Length) {
            $chunkId = $enc.GetString($br.ReadBytes(4))
            $chunkSize = $br.ReadUInt32()
            if ($chunkId -eq 'fmt ') {
                $formatTag = $br.ReadUInt16()
                if ($formatTag -ne 1) { throw 'Unsupported WAV format (expect PCM)' }
                $channels = $br.ReadUInt16()
                $sampleRate = $br.ReadUInt32()
                $br.ReadUInt32() | Out-Null
                $blockAlign = $br.ReadUInt16()
                $bitsPerSample = $br.ReadUInt16()
                if ($chunkSize -gt 16) { $br.ReadBytes($chunkSize - 16) | Out-Null }
            } elseif ($chunkId -eq 'data') {
                $dataStart = $fs.Position; $dataSize = $chunkSize; break
            } else {
                $br.ReadBytes($chunkSize) | Out-Null
            }
        }
        if ($dataSize -le 0 -or $bitsPerSample -ne 16) { throw 'Unsupported WAV (missing data chunk or non 16-bit)' }
        $fs.Position = $dataStart
        $remaining = $dataSize
        $minSample = [int]::MaxValue; $maxSample = [int]::MinValue
        $sum = 0.0; $sumSq = 0.0; $count = 0; $bufferSize = 8192
        while ($remaining -gt 0) {
            $toRead = [System.Math]::Min($bufferSize, $remaining)
            $bytes = $br.ReadBytes($toRead)
            if ($bytes.Length -eq 0) { break }
            for ($i = 0; $i -lt $bytes.Length; $i += 2) {
                $sample = [System.BitConverter]::ToInt16($bytes, $i)
                if ($sample -lt $minSample) { $minSample = $sample }
                if ($sample -gt $maxSample) { $maxSample = $sample }
                $sum += $sample; $sumSq += $sample * $sample; $count++
            }
            $remaining -= $bytes.Length
        }
        if ($count -eq 0) { throw 'Empty WAV data' }
        $duration = $count / $channels / $sampleRate
        $mean = $sum / $count
        $rms = [Math]::Sqrt($sumSq / $count) / 32768.0
        if ($minSample -eq -32768) { $minAbs = 32768 } else { $minAbs = [Math]::Abs($minSample) }
        $maxAbs = [Math]::Abs($maxSample)
        $peak = ([Math]::Max($minAbs, $maxAbs)) / 32768.0
        return [pscustomobject]@{
            SampleRate      = $sampleRate
            Channels        = $channels
            BitsPerSample   = $bitsPerSample
            Samples         = $count
            DurationSeconds = [Math]::Round($duration, 3)
            RMS             = [Math]::Round($rms, 6)
            Peak            = [Math]::Round($peak, 6)
            Mean            = [Math]::Round($mean / 32768.0, 6)
        }
    } finally {
        $br.Dispose(); $fs.Dispose()
    }
}

$refStats = Get-WavStats -Path $refFull
if (-not $refStats) { Write-Error "Failed to compute WAV stats for reference at '$refFull'." }

# Generate spectrogram for reference using same ffmpeg settings as synth pipeline
$refDir = Split-Path $refFull -Parent
$refSpectrogram = Join-Path $refDir 'by-the-lake-spectrogram.png'
Write-Host "== Rendering spectrogram for reference =="
$ffArgs = @('-y','-i',$refFull,'-lavfi','showspectrumpic=s=1280x720:mode=combined:legend=0',$refSpectrogram)
& ffmpeg @ffArgs

$synthReport = Get-Content $synthReportFull -Raw | ConvertFrom-Json
$synthStats = $synthReport.wav_stats
if (-not $synthStats) { Write-Error "Synth report does not contain wav_stats: '$synthReportFull'." }

$refReportPath = Join-Path (Split-Path $synthReportFull -Parent) 'reference-report.json'

$refReportObject = [ordered]@{
    wav_path         = $refFull
    spectrogram_path = $refSpectrogram
    wav_stats        = $refStats
    iteration_meta   = "Reference recording (external guitar)"
    git_diff_stat    = ""
    git_diff         = ""
    project_root     = $scriptRoot
    lib_rs_path      = (Join-Path $scriptRoot 'rust-engine\src\lib.rs')
}

$refJson = $refReportObject | ConvertTo-Json -Depth 6
Set-Content -Path $refReportPath -Value $refJson -Encoding UTF8

Write-Host "== Running critic on synth (iteration report) =="
$oldEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
$synthCriticOut = & node (Join-Path $scriptRoot 'scripts\run-critic.js') --report $synthReportFull 2>&1
$ErrorActionPreference = $oldEap
$synthCriticOut | Write-Host

Write-Host "== Running critic on reference (by-the-lake.wav) =="
$oldEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
$refCriticOut = & node (Join-Path $scriptRoot 'scripts\run-critic.js') --report $refReportPath 2>&1
$ErrorActionPreference = $oldEap
$refCriticOut | Write-Host

Write-Host "== Running spectral critic (synth vs reference) =="
$oldEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
$spectralOut = & node (Join-Path $scriptRoot 'scripts\run-spectral-critic.js') 2>&1
$ErrorActionPreference = $oldEap
$spectralOut | Write-Host

function Extract-CriticScore {
    param([string[]]$Lines)
    $line = $Lines | Where-Object { $_ -match '^CRITIC_SCORE=' } | Select-Object -First 1
    if (-not $line) { return $null }
    $parts = $line -split '='
    if ($parts.Count -lt 2) { return $null }
    return [double]::Parse($parts[1], [System.Globalization.CultureInfo]::InvariantCulture)
}
$synthScore = Extract-CriticScore -Lines $synthCriticOut
$refScore   = Extract-CriticScore -Lines $refCriticOut
Write-Host ""
Write-Host "== Summary: synth vs reference =="
$fmt = "{0,-18} {1,12} {2,12}"
Write-Host ($fmt -f 'Metric', 'Synth', 'Reference')
Write-Host ($fmt -f '------', '-----', '---------')
Write-Host ($fmt -f 'Duration (s)', $synthStats.DurationSeconds, $refStats.DurationSeconds)
Write-Host ($fmt -f 'RMS', $synthStats.RMS, $refStats.RMS)
Write-Host ($fmt -f 'Peak', $synthStats.Peak, $refStats.Peak)
Write-Host ($fmt -f 'Mean', $synthStats.Mean, $refStats.Mean)
if ($synthScore -ne $null -or $refScore -ne $null) { Write-Host ($fmt -f 'Critic score', $synthScore, $refScore) }

