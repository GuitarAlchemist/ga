# M1 run of the BRUTAL evaluation (GuitarAlchemist/gaia#257): checks the host's memory headroom first, then restores and
# builds HelloBrutal once, runs it once (one window, one key, Esc), and writes the evidence to evidence/m1-<stamp>.md
# with a screenshot. Every check ends PASS, FAIL or NOT RUN. Windows only (SendKeys and the screenshot use Win32).
# Usage: pwsh -File run-m1.ps1 [-MaxCommitGB 125] [-MinFreeGB 8]
param([double]$MaxCommitGB = 125, [double]$MinFreeGB = 8)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'HelloBrutal'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $root "evidence"
New-Item -ItemType Directory -Force $out | Out-Null
$md = Join-Path $out "m1-$stamp.md"
$checks = [ordered]@{}
$lines = [System.Collections.Generic.List[string]]::new()
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
function Finish {
    $lines.Add('')
    $lines.Add('| Check | Result |')
    $lines.Add('|---|---|')
    foreach ($k in $checks.Keys) { $lines.Add("| $k | $($checks[$k]) |") }
    Set-Content -Path $md -Value $lines -Encoding utf8
    Write-Host "evidence: $md"
}

Note "# M1 run, $(Get-Date -Format 'yyyy-MM-dd HH:mm zzz')"
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
    foreach ($k in 'Restore and build', 'Window opens', 'Key input logged', 'Esc closes, exit code 0', 'No leftover process', 'RTX view untouched') { $checks[$k] = 'NOT RUN' }
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
Get-ChildItem $pkgs -Directory -Filter 'ahwoo.brutal.*' | ForEach-Object {
    Get-ChildItem $_.FullName -Recurse -File -Include '*.dll', '*.so', '*.dylib' | Where-Object FullName -Match '\\runtimes\\' | ForEach-Object {
        $sig = Get-AuthenticodeSignature $_.FullName
        $signer = if ($sig.SignerCertificate) { $sig.SignerCertificate.GetNameInfo('SimpleName', $false) } else { 'unsigned' }
        Note "- ``$(Private $_.FullName)`` sha256 $((Get-FileHash $_.FullName).Hash.Substring(0, 16))… signer: $signer ($($sig.Status))"
    }
}

# One run: wait for the window, send one key, take a screenshot, send Esc.
$exe = Join-Path $proj 'bin\Release\net10.0\HelloBrutal.exe'
$log = Join-Path $out "m1-$stamp.log.md"
$t = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $exe -ArgumentList '10' -RedirectStandardOutput $log -PassThru -WindowStyle Normal
$hwnd = [IntPtr]::Zero
while ($t.Elapsed.TotalSeconds -lt 5 -and $hwnd -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 100; $p.Refresh(); $hwnd = $p.MainWindowHandle }
$checks['Window opens'] = if ($hwnd -ne [IntPtr]::Zero) { "PASS: window handle after $([math]::Round($t.Elapsed.TotalMilliseconds)) ms" } else { 'FAIL: no window within 5 s' }
$shell = New-Object -ComObject WScript.Shell
$focused = $shell.AppActivate('Hello BRUTAL')
Start-Sleep -Milliseconds 300
$shell.SendKeys('b')
Start-Sleep -Milliseconds 700
if ($hwnd -ne [IntPtr]::Zero) {
    Add-Type -AssemblyName System.Drawing
    Add-Type -Namespace M1 -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
public struct RECT { public int L, T, R, B; }
'@
    # Physical pixels, and the window alone (PW_RENDERFULLCONTENT): nothing else on the screen gets into the evidence.
    [void][M1.Win]::SetProcessDPIAware()
    $r = New-Object M1.Win+RECT
    [void][M1.Win]::GetWindowRect($hwnd, [ref]$r)
    $bmp = New-Object Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    [void][M1.Win]::PrintWindow($hwnd, $dc, 2)
    $g.ReleaseHdc($dc)
    $bmp.Save((Join-Path $out "m1-$stamp.png"))
    $g.Dispose(); $bmp.Dispose()
}
$p.Refresh()
$peakMB = [math]::Round($p.PeakWorkingSet64 / 1MB)
$shell.SendKeys('{ESC}')
$exited = $p.WaitForExit(15000)
Start-Sleep -Milliseconds 500
$stdout = Get-Content $log
$checks['Key input logged'] = if ($stdout -match 'M1 key B ') { "PASS (focus taken: $focused)" } else { "FAIL (focus taken: $focused)" }
$checks['Esc closes, exit code 0'] = if ($exited -and $p.ExitCode -eq 0 -and ($stdout -match 'closed by Esc')) { 'PASS' } else { "FAIL (exited: $exited, code: $(if ($exited) { $p.ExitCode }))" }
if (-not $exited) { Stop-Process -Id $p.Id -Force }
$left = @(Get-Process -Name 'HelloBrutal' -ErrorAction SilentlyContinue)
$checks['No leftover process'] = if ($left.Count -eq 0) { 'PASS' } else { "FAIL: $($left.Id -join ', ')" }
$after = Memory
$godotAfter = @(Get-Process -Name 'godot*' -ErrorAction SilentlyContinue | ForEach-Object Id)
$checks['RTX view untouched'] = if (-not (Compare-Object $godot $godotAfter)) { 'PASS: same Godot processes before and after' } else { "FAIL: before $($godot -join ','), after $($godotAfter -join ',')" }

Note ''
Note "## Run ($([math]::Round($t.Elapsed.TotalSeconds, 1)) s, peak working set $peakMB MB)"
Note '```'
$stdout | ForEach-Object { Note $_ }
Note '```'
Note ''
Note "- Memory after: commit $($after.CommitGB) GB, free RAM $($after.FreeGB) GB"
Note "- Screenshot: m1-$stamp.png. A blank window proves the lifecycle only, not rendering."
Remove-Item $log
Finish
