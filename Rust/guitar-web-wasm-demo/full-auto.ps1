param(
    [switch]$SkipNpmInstall
)

$ErrorActionPreference = 'Stop'

Write-Host "=== GA Guitar demo: full automatic pipeline ==="

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
Set-Location $scriptRoot

function Resolve-NpmCommand {
    $candidates = @('npm.cmd', 'npm')
    foreach ($name in $candidates) {
        try {
            $cmd = Get-Command $name -ErrorAction Stop
            if ($cmd -and $cmd.Source) {
                return $cmd.Source
            }
        } catch {
            continue
        }
    }
    throw 'Unable to locate npm on PATH. Please ensure Node.js is installed.'
}

function Stop-ViteProcesses {
    Write-Host "== Ensuring prior Vite/node processes are stopped =="
    try {
        $nodeProcs = Get-CimInstance Win32_Process -Filter "Name = 'node.exe'"
        foreach ($proc in $nodeProcs) {
            if ($proc.CommandLine -and $proc.CommandLine -match 'vite') {
                Write-Host "Killing lingering node.exe (PID $($proc.ProcessId)) running Vite"
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
            }
        }
    } catch {
        Write-Warning "Unable to enumerate node/Vite processes: $_"
    }
}

function Write-IterationMetadata {
    param(
        [string]$DestinationFile
    )

    $timestamp = Get-Date -Format 'yyyy-MM-ddTHH:mm:ssK'
    $gitHead = ''
    $gitStatus = ''
    try {
        $gitHead = (git -C $scriptRoot rev-parse HEAD).Trim()
        $gitStatus = (git -C $scriptRoot status --short).Trim()
    } catch {
        $gitHead = 'unknown'
        $gitStatus = 'git unavailable'
    }
    if (-not $gitStatus) {
        $gitStatus = 'clean'
    }

    $note = $env:GA_ITERATION_NOTE
    if (-not $note) {
        $note = 'Automated acoustic iteration'
    }

    $content = @(
        "timestamp: $timestamp",
        "git_head: $gitHead",
        "note: $note",
        "changes: $gitStatus"
    )

    Set-Content -Path $DestinationFile -Value $content -Encoding UTF8

    return [pscustomobject]@{
        Timestamp = $timestamp
        GitHead = $gitHead
        Note = $note
        Changes = $gitStatus
    }
}

function Update-SourceHeader {
    param(
        [string]$SourceFile,
        [pscustomobject]$Metadata
    )

    if (-not (Test-Path $SourceFile)) {
        return
    }

    $timestamp = $Metadata.Timestamp
    $gitHead = $Metadata.GitHead
    if ([string]::IsNullOrWhiteSpace($gitHead)) {
        $gitHead = 'unknown'
    }
    $shortHead = if ($gitHead.Length -ge 7) { $gitHead.Substring(0,7) } else { $gitHead }
    $note = $Metadata.Note
    $header = "// GA Guitar WASM demo – iteration ($timestamp, $shortHead, $note)"

    $content = Get-Content -Path $SourceFile
    if ($content.Count -eq 0) {
        $content = @($header)
    } elseif ($content[0] -like '// GA Guitar WASM demo*') {
        $content[0] = $header
    } else {
        $content = ,$header + $content
    }
    Set-Content -Path $SourceFile -Value $content -Encoding UTF8
}

function Get-WavStats {
    param(
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return $null
    }

    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    $br = New-Object System.IO.BinaryReader($fs)
    try {
        $encoding = [System.Text.Encoding]::ASCII
        $riff = $encoding.GetString($br.ReadBytes(4))
        if ($riff -ne 'RIFF') { throw 'Not RIFF' }
        $br.ReadUInt32() | Out-Null
        $wave = $encoding.GetString($br.ReadBytes(4))
        if ($wave -ne 'WAVE') { throw 'Not WAVE' }

        $bitsPerSample = 16
        $channels = 1
        $sampleRate = 48000
        $dataSize = 0
        $dataStart = 0

        while ($fs.Position -lt $fs.Length) {
            $chunkId = $encoding.GetString($br.ReadBytes(4))
            $chunkSize = $br.ReadUInt32()
            if ($chunkId -eq 'fmt ') {
                $formatTag = $br.ReadUInt16()
                if ($formatTag -ne 1) { throw 'Unsupported WAV format (expect PCM)' }
                $channels = $br.ReadUInt16()
                $sampleRate = $br.ReadUInt32()
                $br.ReadUInt32() | Out-Null # byte rate
                $blockAlign = $br.ReadUInt16()
                $bitsPerSample = $br.ReadUInt16()
                if ($chunkSize -gt 16) {
                    $br.ReadBytes($chunkSize - 16) | Out-Null
                }
            } elseif ($chunkId -eq 'data') {
                $dataStart = $fs.Position
                $dataSize = $chunkSize
                break
            } else {
                $br.ReadBytes($chunkSize) | Out-Null
            }
        }

        if ($dataSize -le 0 -or $bitsPerSample -ne 16) {
            throw 'Unsupported WAV (missing data chunk or non 16-bit)'
        }

        $fs.Position = $dataStart
        $remaining = $dataSize
        $minSample = [int]::MaxValue
        $maxSample = [int]::MinValue
        $sum = 0.0
        $sumSq = 0.0
        $count = 0
        $bufferSize = 8192

        while ($remaining -gt 0) {
            $toRead = [System.Math]::Min($bufferSize, $remaining)
            $bytes = $br.ReadBytes($toRead)
            if ($bytes.Length -eq 0) { break }
            for ($i = 0; $i -lt $bytes.Length; $i += 2) {
                $sample = [System.BitConverter]::ToInt16($bytes, $i)
                if ($sample -lt $minSample) { $minSample = $sample }
                if ($sample -gt $maxSample) { $maxSample = $sample }
                $sum += $sample
                $sumSq += $sample * $sample
                $count++
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
            SampleRate = $sampleRate
            Channels = $channels
            BitsPerSample = $bitsPerSample
            Samples = $count
            DurationSeconds = [Math]::Round($duration, 3)
            RMS = [Math]::Round($rms, 6)
            Peak = [Math]::Round($peak, 6)
            Mean = [Math]::Round($mean / 32768.0, 6)
        }
    } finally {
        $br.Dispose()
        $fs.Dispose()
    }
}

function Write-IterationReport {
    param(
        [string]$DestinationFile,
        [string]$WavPath,
        [string]$SpectrogramPath,
        [string]$IterationInfoPath,
        [string]$ProjectRoot,
        [string]$SourceFileRelative
    )

    $metaText = ''
    if (Test-Path $IterationInfoPath) {
        $metaText = Get-Content $IterationInfoPath -Raw
    }

    $stats = Get-WavStats -Path $WavPath

    $diffStat = ''
    $diffText = ''
    try {
        $diffStat = git -C $ProjectRoot diff --stat -- $SourceFileRelative | Out-String
        $diffText = git -C $ProjectRoot diff -- $SourceFileRelative | Out-String
    } catch {
        $diffStat = 'git diff unavailable'
        $diffText = 'git diff unavailable'
    }

    $reportLines = @()
    $reportLines += '# GA Acoustic Iteration Report'
    $reportLines += ''
    if ($stats) {
        $reportLines += "- WAV: $WavPath"
        $reportLines += "- Spectrogram: $SpectrogramPath"
        $reportLines += ("- Duration: {0:N2} s, SampleRate: {1} Hz, Channels: {2}" -f $stats.DurationSeconds, $stats.SampleRate, $stats.Channels)
        $reportLines += ("- RMS: {0}, Peak: {1}, Mean: {2}" -f $stats.RMS, $stats.Peak, $stats.Mean)
    } else {
        $reportLines += '- WAV stats unavailable'
    }

    $reportLines += ''
    $reportLines += '## Iteration Metadata'
    if ([string]::IsNullOrWhiteSpace($metaText)) {
        $metaBlock = 'n/a'
    } else {
        $metaBlock = $metaText.TrimEnd()
    }
    $reportLines += '```'
    $reportLines += $metaBlock
    $reportLines += '```'

    $reportLines += ''
    $reportLines += '## Diff Stat (rust-engine/src/lib.rs)'
    if ([string]::IsNullOrWhiteSpace($diffStat)) {
        $diffStatBlock = 'n/a'
    } else {
        $diffStatBlock = $diffStat.TrimEnd()
    }
    $reportLines += '```'
    $reportLines += $diffStatBlock
    $reportLines += '```'

    $reportLines += ''
    $reportLines += '## Diff Detail'
    if ([string]::IsNullOrWhiteSpace($diffText)) {
        $diffDetailBlock = 'n/a'
    } else {
        $diffDetailBlock = $diffText.TrimEnd()
    }
    $reportLines += '```diff'
    $reportLines += $diffDetailBlock
    $reportLines += '```'

    Set-Content -Path $DestinationFile -Value $reportLines -Encoding UTF8

    # Also emit a JSON report that multimodal models or tools can consume directly
    $jsonPath = [System.IO.Path]::ChangeExtension($DestinationFile, '.json')

    $jsonObject = [ordered]@{
        wav_path         = $WavPath
        spectrogram_path = $SpectrogramPath
        wav_stats        = $stats
        iteration_meta   = $metaBlock
        git_diff_stat    = $diffStatBlock
        git_diff         = $diffDetailBlock
        project_root     = $ProjectRoot
        lib_rs_path      = (Join-Path $ProjectRoot $SourceFileRelative)
    }

    $json = $jsonObject | ConvertTo-Json -Depth 6
    Set-Content -Path $jsonPath -Value $json -Encoding UTF8
}

Write-Host "== Ensuring wasm32-unknown-unknown target =="
rustup target add wasm32-unknown-unknown

Write-Host "== Building Rust WASM engine =="
Push-Location .\rust-engine
cargo build --release --target wasm32-unknown-unknown
$wasmPath = Join-Path (Resolve-Path .\target\wasm32-unknown-unknown\release) 'guitar_engine.wasm'
Pop-Location

Write-Host "== Copying WASM to public/guitar_engine.wasm =="
New-Item -ItemType Directory -Force -Path .\public | Out-Null
Copy-Item $wasmPath -Destination .\public\guitar_engine.wasm -Force

Write-Host "== Preparing iteration artifacts =="
New-Item -ItemType Directory -Force -Path .\playwright-downloads | Out-Null
$iterationInfoPath = '.\playwright-downloads\iteration-info.txt'
$meta = Write-IterationMetadata -DestinationFile $iterationInfoPath
Update-SourceHeader -SourceFile .\rust-engine\src\lib.rs -Metadata $meta

Write-Host "== Exporting latest lib.rs for downstream agents =="
Copy-Item .\rust-engine\src\lib.rs -Destination .\playwright-downloads\lib.rs -Force

if (-not $SkipNpmInstall) {
    Write-Host "== Running npm install =="
    npm install
}

Stop-ViteProcesses

Write-Host "== Starting Vite dev server on http://localhost:5173 =="
$npmCommand = Resolve-NpmCommand
$dev = Start-Process -FilePath $npmCommand -ArgumentList @('run', 'dev') -PassThru

try {
    # Wait for server to be ready
    Write-Host "Waiting for dev server to respond ..."
    $maxTries = 40
    $ok = $false
    for ($i = 0; $i -lt $maxTries; $i++) {
        try {
            $resp = Invoke-WebRequest -Uri 'http://localhost:5173' -UseBasicParsing -TimeoutSec 2
            if ($resp.StatusCode -eq 200) {
                $ok = $true
                break
            }
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $ok) {
        throw "Dev server not reachable."
    }

    Write-Host "== Running Playwright auto-record script =="
    $env:GA_DEMO_URL = 'http://localhost:5173/'
    node .\scripts\record-and-analyze.js
}
finally {
    Write-Host "== Stopping dev server =="
    if ($dev -and !$dev.HasExited) {
        $dev.Kill()
    }
    Stop-ViteProcesses
}

$wavPath = Join-Path $scriptRoot 'playwright-downloads\guitar-mix.wav'

# Optional DDSP timbre coloration
$useDdsp = $env:GA_USE_DDSP
$ddspCkptDir = $env:GA_DDSP_CKPT_DIR
if ($useDdsp -eq '1') {
    Write-Host "== Running DDSP timbre coloration (GA_USE_DDSP=1) =="
    $ddspOut = Join-Path $scriptRoot 'playwright-downloads\guitar-mix-ddsp.wav'
    try {
        if ($ddspCkptDir) {
            & (Join-Path $scriptRoot 'apply-ddsp-color.ps1') -InputWav $wavPath -OutputWav $ddspOut -CkptDir $ddspCkptDir
        } else {
            & (Join-Path $scriptRoot 'apply-ddsp-color.ps1') -InputWav $wavPath -OutputWav $ddspOut
        }
        if (Test-Path $ddspOut) {
            Write-Host "DDSP output found, using colored WAV for analysis: $ddspOut"
            $wavPath = $ddspOut
        } else {
            Write-Warning "DDSP output not found at $ddspOut; keeping original WAV."
        }
    } catch {
        Write-Warning "DDSP coloration failed: $_"
    }
}

$spectrogramPath = Join-Path $scriptRoot 'playwright-downloads\guitar-mix-spectrogram.png'
$iterationInfoPath = Join-Path $scriptRoot 'playwright-downloads\iteration-info.txt'
$reportPath = Join-Path $scriptRoot 'playwright-downloads\iteration-report.md'
Write-IterationReport -DestinationFile $reportPath -WavPath $wavPath -SpectrogramPath $spectrogramPath -IterationInfoPath $iterationInfoPath -ProjectRoot $scriptRoot -SourceFileRelative 'rust-engine/src/lib.rs'

Write-Host "Done. WAV and spectrogram PNG should be ready (see WAV_PATH / SPECTRO_PATH in output)."

$criticJsonPath = [System.IO.Path]::ChangeExtension($reportPath, '.json')
if (Test-Path $criticJsonPath) {
    Write-Host "== Running local critic on iteration report =="
    try {
        node .\scripts\run-critic.js --report $criticJsonPath
    } catch {
        Write-Warning "Local critic failed: $_"
    }
} else {
    Write-Warning "Critic JSON report not found at $criticJsonPath"
}

# Optional spectral critic (synth vs reference)
$spectralScript = Join-Path $scriptRoot 'scripts\run-spectral-critic.js'
if (Test-Path $spectralScript) {
    Write-Host "== Running spectral critic (synth vs reference) =="
    try {
        node $spectralScript
    } catch {
        Write-Warning "Spectral critic failed: $_"
    }
}

