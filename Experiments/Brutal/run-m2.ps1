# M2 run of the BRUTAL evaluation (GuitarAlchemist/gaia#257): checks the host's memory headroom first, then builds
# HallSlice once and runs it once: the window opens, three camera keys go in, a screenshot is taken, the GPU is sampled
# each second, and Esc closes it. The evidence goes to evidence/m2-<stamp>.md with the screenshot. Every check ends
# PASS, FAIL or NOT RUN. Windows only (SendKeys and the screenshot use Win32).
# -FpsLog: a log where the Godot view running beside it prints "FPSLOG <local time> <fps>" each second; the run then
# also checks that the view kept its frame rate (>= -MinFps) while HallSlice drew.
# -Vsync: HallSlice presents in FIFO mode (vsync) instead of immediate.
# Usage: pwsh -File run-m2.ps1 [-MaxCommitGB 125] [-MinFreeGB 8] [-Seconds 12] [-FpsLog <path>] [-MinFps 60] [-Vsync]
param([double]$MaxCommitGB = 125, [double]$MinFreeGB = 8, [int]$Seconds = 12, [string]$FpsLog = '', [double]$MinFps = 60, [switch]$Vsync)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'HallSlice'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $root "evidence"
New-Item -ItemType Directory -Force $out | Out-Null
$md = Join-Path $out "m2-$stamp.md"
$checks = [ordered]@{}
$lines = [System.Collections.Generic.List[string]]::new()
$all = 'Resource headroom', 'Restore and build', 'Window opens', 'First frame', 'Hall drawn', 'Camera keys logged', 'Frame times recorded', 'Esc closes, exit code 0', 'No leftover process', 'RTX view untouched', 'RTX view frame rate'
function Note([string]$s) { $lines.Add($s); Write-Host $s }
function Private([string]$s) { $s.Replace($env:USERPROFILE, '~') }
function Memory {
    $os = Get-CimInstance Win32_OperatingSystem
    [pscustomobject]@{
        CommitGB = [math]::Round(($os.TotalVirtualMemorySize - $os.FreeVirtualMemory) / 1MB, 1)
        LimitGB = [math]::Round($os.TotalVirtualMemorySize / 1MB, 1)
        FreeGB = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
    }
}
function Gpu { (nvidia-smi --query-gpu=utilization.gpu,memory.used,power.draw --format=csv,noheader,nounits) -split ',\s*' | ForEach-Object { [double]$_ } }
function Finish {
    foreach ($k in $all) { if (-not $checks.Contains($k)) { $checks[$k] = 'NOT RUN' } }
    $lines.Add('')
    $lines.Add('| Check | Result |')
    $lines.Add('|---|---|')
    foreach ($k in $checks.Keys) { $lines.Add("| $k | $($checks[$k]) |") }
    Set-Content -Path $md -Value $lines -Encoding utf8
    Write-Host "evidence: $md"
}
# Distinct colours (5 bits a channel) over a 64 x 36 grid: a black or uniform capture has one or two.
function Colours($bmp) {
    $seen = @{}
    for ($y = 0; $y -lt 36; $y++) {
        for ($x = 0; $x -lt 64; $x++) {
            $c = $bmp.GetPixel([int](($x + 0.5) * $bmp.Width / 64), [int](($y + 0.5) * $bmp.Height / 36))
            $seen[(($c.R -shr 3) -shl 10) -bor (($c.G -shr 3) -shl 5) -bor ($c.B -shr 3)] = 1
        }
    }
    $seen.Count
}

Note "# M2 run, $(Get-Date -Format 'yyyy-MM-dd HH:mm zzz')"
Note ''
Note "- Revision: $(git -C $root rev-parse --short HEAD) on $(git -C $root branch --show-current) (uncommitted changes: $([bool](git -C $root status --porcelain -- .)))"
Push-Location $proj
Note "- .NET SDK: $(dotnet --version)"
Pop-Location
Note "- GPU: $((nvidia-smi --query-gpu=name,driver_version --format=csv,noheader) -join '; ')"
$before = Memory
Note "- Memory before: commit $($before.CommitGB) / $($before.LimitGB) GB, free RAM $($before.FreeGB) GB (budget: commit <= $MaxCommitGB GB, free >= $MinFreeGB GB)"
$godot = @(Get-Process -Name 'godot*' -ErrorAction SilentlyContinue | ForEach-Object Id)
Note "- Godot processes before (the RTX view must be left alone): $($godot -join ', ')"

if ($before.CommitGB -gt $MaxCommitGB -or $before.FreeGB -lt $MinFreeGB) {
    $checks['Resource headroom'] = "BLOCKED: commit $($before.CommitGB) GB, free $($before.FreeGB) GB"
    Finish
    exit 2
}
$checks['Resource headroom'] = "PASS: commit $($before.CommitGB) GB, free $($before.FreeGB) GB"

# Restore and build, once.
Push-Location $proj
$t = [Diagnostics.Stopwatch]::StartNew()
$build = dotnet build -c Release -nologo 2>&1
$code = $LASTEXITCODE
Pop-Location
Note ''
Note "## Build ($([math]::Round($t.Elapsed.TotalSeconds, 1)) s, exit $code)"
Note '```'
$build | Select-Object -Last 6 | ForEach-Object { Note (Private $_) }
Note '```'
if ($code -ne 0) {
    $checks['Restore and build'] = 'FAIL (see Build)'
    Finish
    exit 1
}
$checks['Restore and build'] = 'PASS'

# What was restored: package versions and the native binaries they ship, hashed, with their signer.
Note ''
Note '## Restored packages and native binaries'
$lock = Get-Content (Join-Path $proj 'packages.lock.json') -Raw | ConvertFrom-Json
foreach ($fw in $lock.dependencies.PSObject.Properties) {
    foreach ($p in $fw.Value.PSObject.Properties) { Note "- $($p.Name) $($p.Value.resolved) ($($p.Value.type))" }
}
$pkgs = Join-Path $env:USERPROFILE '.nuget\packages'
foreach ($p in $lock.dependencies.PSObject.Properties.Value.PSObject.Properties | Where-Object Name -Like 'Ahwoo.Brutal.*') {
    $dir = Join-Path $pkgs "$($p.Name.ToLowerInvariant())\$($p.Value.resolved)"
    Get-ChildItem $dir -Recurse -File -Include '*.dll', '*.so', '*.dylib' -ErrorAction SilentlyContinue | Where-Object FullName -Match '\\runtimes\\win-x64\\' | ForEach-Object {
        $sig = Get-AuthenticodeSignature $_.FullName
        $signer = if ($sig.SignerCertificate) { $sig.SignerCertificate.GetNameInfo('SimpleName', $false) } else { 'unsigned' }
        Note "- ``$(Private $_.FullName)`` sha256 $((Get-FileHash $_.FullName).Hash.Substring(0, 16))… signer: $signer ($($sig.Status))"
    }
}

# One run: wait for the window, send three camera keys, take a screenshot, sample the GPU each second, send Esc.
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace M2 -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
public struct RECT { public int L, T, R, B; }
'@
[void][M2.Win]::SetProcessDPIAware()
$gpuIdle = Gpu
$exe = Join-Path $proj 'bin\Release\net10.0\HallSlice.exe'
$log = Join-Path $out "m2-$stamp.log.md"
$start = Get-Date
$t = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $exe -ArgumentList (@("$($Seconds + 15)") + @(if ($Vsync) { '--vsync' })) -RedirectStandardOutput $log -PassThru -WindowStyle Normal
$hwnd = [IntPtr]::Zero
while ($t.Elapsed.TotalSeconds -lt 5 -and $hwnd -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 100; $p.Refresh(); $hwnd = $p.MainWindowHandle }
$checks['Window opens'] = if ($hwnd -ne [IntPtr]::Zero) { "PASS: window handle after $([math]::Round($t.Elapsed.TotalMilliseconds)) ms" } else { 'FAIL: no window within 5 s' }
$shell = New-Object -ComObject WScript.Shell
$focused = $shell.AppActivate('Hall slice (BRUTAL)')
Start-Sleep -Milliseconds 800
# Arrows as extended keys with their scan codes: SendKeys' {LEFT} reaches GLFW as the keypad's 4.
foreach ($k in @(0x25, 0x4B), @(0x26, 0x48)) { [M2.Win]::keybd_event($k[0], $k[1], 1, [UIntPtr]::Zero); [M2.Win]::keybd_event($k[0], $k[1], 3, [UIntPtr]::Zero); Start-Sleep -Milliseconds 300 }
$shell.SendKeys('w')
Start-Sleep -Milliseconds 800
$shot = 'none'
if ($hwnd -ne [IntPtr]::Zero) {
    # Physical pixels, and the window alone (PW_RENDERFULLCONTENT). If that comes back blank, as it can for a window a
    # GPU presents to directly, copy the window's rectangle from the screen instead: the window was just brought to front.
    $r = New-Object M2.Win+RECT
    [void][M2.Win]::GetWindowRect($hwnd, [ref]$r)
    $bmp = New-Object Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    [void][M2.Win]::PrintWindow($hwnd, $dc, 2)
    $g.ReleaseHdc($dc)
    $colours = Colours $bmp
    $shot = "PrintWindow, $colours colours"
    if ($colours -lt 8) {
        $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
        $colours = Colours $bmp
        $shot += "; screen copy, $colours colours"
    }
    $bmp.Save((Join-Path $out "m2-$stamp.png"))
    $g.Dispose(); $bmp.Dispose()
    $checks['Hall drawn'] = if ($colours -ge 8) { "PASS: $shot" } else { "FAIL: $shot" }
}
$samples = [System.Collections.Generic.List[double[]]]::new()
while ($t.Elapsed.TotalSeconds -lt $Seconds -and -not $p.HasExited) { $samples.Add((Gpu)); Start-Sleep -Milliseconds 700 }
$p.Refresh()
$peakMB = [math]::Round($p.PeakWorkingSet64 / 1MB)
[void]$shell.AppActivate('Hall slice (BRUTAL)')
Start-Sleep -Milliseconds 200
$shell.SendKeys('{ESC}')
$exited = $p.WaitForExit(15000)
$end = Get-Date
Start-Sleep -Milliseconds 500
$stdout = Get-Content $log
$first = $stdout | Select-String 'M2 first frame presented after (\d+) ms' | Select-Object -First 1
$checks['First frame'] = if ($first) { "PASS: $($first.Matches[0].Groups[1].Value) ms after start" } else { 'FAIL: no frame presented' }
$keys = @('Left', 'Up', 'W' | Where-Object { $stdout -match "M2 key $_\b" })
$checks['Camera keys logged'] = if ($keys.Count -eq 3) { "PASS: Left, Up, W (focus taken: $focused)" } else { "FAIL: $($keys -join ', ') of Left, Up, W (focus taken: $focused)" }
$frames = $stdout | Select-String '^M2 frames .*' | Select-Object -First 1
$checks['Frame times recorded'] = if ($frames) { "PASS: $($frames.Line.Substring(3))" } else { 'FAIL: no frame-time line' }
$checks['Esc closes, exit code 0'] = if ($exited -and $p.ExitCode -eq 0 -and ($stdout -match 'closed by Esc')) { 'PASS' } else { "FAIL (exited: $exited, code: $(if ($exited) { $p.ExitCode }))" }
if (-not $exited) { Stop-Process -Id $p.Id -Force }
$left = @(Get-Process -Name 'HallSlice' -ErrorAction SilentlyContinue)
$checks['No leftover process'] = if ($left.Count -eq 0) { 'PASS' } else { "FAIL: $($left.Id -join ', ')" }
$after = Memory
$godotAfter = @(Get-Process -Name 'godot*' -ErrorAction SilentlyContinue | ForEach-Object Id)
$checks['RTX view untouched'] = if (-not (Compare-Object $godot $godotAfter)) { 'PASS: same Godot processes before and after' } else { "FAIL: before $($godot -join ','), after $($godotAfter -join ',')" }

# The Godot view's own frame rate, from its log: the 20 s before the run, and while HallSlice ran.
if ($FpsLog -and (Test-Path $FpsLog)) {
    $fps = Select-String -Path $FpsLog -Pattern '^FPSLOG (\S+) ([\d.]+)' | ForEach-Object {
        [pscustomobject]@{ At = [datetime]::ParseExact($_.Matches[0].Groups[1].Value, 'yyyy-MM-ddTHH:mm:ss', $null); Fps = [double]$_.Matches[0].Groups[2].Value }
    }
    $base = @($fps | Where-Object { $_.At -ge $start.AddSeconds(-20) -and $_.At -lt $start } | ForEach-Object Fps)
    $during = @($fps | Where-Object { $_.At -ge $start -and $_.At -le $end } | ForEach-Object Fps)
    if ($during.Count -gt 0) {
        $min = ($during | Measure-Object -Minimum).Minimum
        $med = ($during | Sort-Object)[[int][math]::Floor($during.Count / 2)]
        $baseMed = if ($base.Count) { ($base | Sort-Object)[[int][math]::Floor($base.Count / 2)] } else { 'n/a' }
        $verdict = if ($min -ge $MinFps) { 'PASS' } else { 'FAIL' }
        $checks['RTX view frame rate'] = "${verdict}: min $min, median $med fps over $($during.Count) s while HallSlice ran (median $baseMed over the 20 s before; threshold $MinFps)"
    } else { $checks['RTX view frame rate'] = 'NOT RUN: no FPSLOG line during the run' }
}

Note ''
Note "## Run ($([math]::Round($t.Elapsed.TotalSeconds, 1)) s, peak working set $peakMB MB)"
Note '```'
$stdout | ForEach-Object { Note $_ }
Note '```'
Note ''
if ($samples.Count) {
    $u = $samples | ForEach-Object { $_[0] } | Measure-Object -Average -Maximum
    $m = $samples | ForEach-Object { $_[1] } | Measure-Object -Maximum
    $w = $samples | ForEach-Object { $_[2] } | Measure-Object -Average -Maximum
    Note "- GPU, whole card (the RTX view included), $($samples.Count) samples: utilisation mean $([math]::Round($u.Average)) % (max $($u.Maximum) %), memory up to $($m.Maximum) MiB (idle $($gpuIdle[1]) MiB before the start), power mean $([math]::Round($w.Average)) W (max $([math]::Round($w.Maximum)) W; $([math]::Round($gpuIdle[2])) W before)"
}
Note "- Memory after: commit $($after.CommitGB) GB, free RAM $($after.FreeGB) GB"
Note "- Screenshot: m2-$stamp.png ($shot)."
Remove-Item $log
Finish
