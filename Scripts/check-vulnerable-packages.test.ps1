# check-vulnerable-packages.test.ps1 — the security scan check fails when it should.
#
# Run:
#   pwsh Scripts/check-vulnerable-packages.test.ps1
#
# Verifies, on hand-written reports in the format of `dotnet list package --vulnerable --format json`:
#   1. A vulnerable package the baseline lists passes.
#   2. A vulnerable package the baseline doesn't list fails, and the error names it.
#   3. A baseline line that is no longer found passes with a warning.
#   4. A report with problems (no restore) fails.
#   5. A report with no vulnerable package passes.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$check = Join-Path $PSScriptRoot 'check-vulnerable-packages.ps1'
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("vulnerable-test-{0}" -f ([Guid]::NewGuid().ToString('N').Substring(0, 8)))
New-Item -ItemType Directory $tmp | Out-Null
# The fixtures' tables stay out of the real job summary
Remove-Item Env:GITHUB_STEP_SUMMARY -ErrorAction SilentlyContinue

$failures = @()
function Assert($cond, $msg) {
    if (-not $cond) {
        $script:failures += $msg
        Write-Host "  FAIL: $msg" -ForegroundColor Red
    } else {
        Write-Host "  ok:   $msg" -ForegroundColor Green
    }
}

function Invoke-Check($report, $baseline) {
    $reportPath = Join-Path $tmp 'report.json'
    $baselinePath = Join-Path $tmp 'baseline.txt'
    $report | ConvertTo-Json -Depth 10 | Set-Content $reportPath
    $baseline | Set-Content $baselinePath
    $output = pwsh -NoProfile -File $check -Report $reportPath -Baseline $baselinePath 2>&1 | Out-String
    [pscustomobject]@{ Exit = $LASTEXITCODE; Output = $output }
}

function Get-Report($packages) {
    @{
        version = 1
        projects = @(
            @{ path = 'C:/repo/Clean/Clean.csproj' },
            @{ path = 'C:/repo/App/App.csproj'; frameworks = @(@{ framework = 'net10.0'; transitivePackages = $packages }) }
        )
    }
}

$snappier = @{ id = 'Snappier'; resolvedVersion = '1.0.0'
               vulnerabilities = @(@{ severity = 'High'; advisoryurl = 'https://github.com/advisories/GHSA-pggp-6c3x-2xmx' }) }
$hotChocolate = @{ id = 'HotChocolate.Language'; resolvedVersion = '13.9.14'
                   vulnerabilities = @(@{ severity = 'Critical'; advisoryurl = 'https://github.com/advisories/GHSA-qr3m-xw4c-jqw3' }) }
$baseline = @('# comment', 'Snappier https://github.com/advisories/GHSA-pggp-6c3x-2xmx  # High')

try {
    Write-Host '1. a listed package passes'
    $r = Invoke-Check (Get-Report @($snappier)) $baseline
    Assert ($r.Exit -eq 0) "exit 0 (got $($r.Exit))"
    Assert ($r.Output -match '1 package advisory\(ies\) found, all in the baseline') 'reports the count'

    Write-Host '2. an unlisted package fails'
    $r = Invoke-Check (Get-Report @($snappier, $hotChocolate)) $baseline
    Assert ($r.Exit -eq 1) "exit 1 (got $($r.Exit))"
    Assert ($r.Output -match '::error::HotChocolate\.Language 13\.9\.14 has a Critical vulnerability .* in App') 'the error names the package and the project'
    Assert ($r.Output -notmatch '::error::Snappier') 'the listed package is not an error'

    Write-Host '3. a stale baseline line warns'
    $r = Invoke-Check (Get-Report @()) $baseline
    Assert ($r.Exit -eq 0) "exit 0 (got $($r.Exit))"
    Assert ($r.Output -match '::warning::Snappier https://github.com/advisories/GHSA-pggp-6c3x-2xmx is no longer found') 'warns about the stale line'

    Write-Host '4. a report with problems fails'
    $r = Invoke-Check @{ version = 1; problems = @(@{ project = 'C:/repo/App/App.csproj'; level = 'error'; text = 'No assets file was found' }) } $baseline
    Assert ($r.Exit -eq 1) "exit 1 (got $($r.Exit))"
    Assert ($r.Output -match '::error::C:/repo/App/App.csproj: No assets file was found') 'prints the problem'

    Write-Host '5. no vulnerable package passes'
    $r = Invoke-Check (Get-Report @()) @()
    Assert ($r.Exit -eq 0) "exit 0 (got $($r.Exit))"
    Assert ($r.Output -match '0 package advisory\(ies\) found') 'reports zero'
} finally {
    Remove-Item -Recurse -Force $tmp
}

if ($failures) {
    Write-Host "$($failures.Count) check(s) failed" -ForegroundColor Red
    exit 1
}
Write-Host 'All checks passed' -ForegroundColor Green
