#requires -RunAsAdministrator
<#
.SYNOPSIS
    Fix the cloudflared service so it dials the ga-demos tunnel.

.DESCRIPTION
    `cloudflared service install` registers the service binary path but
    drops the --config argument silently, leaving the daemon running with
    no tunnel target. This script edits the service ImagePath in the
    registry to include `--config <user config> tunnel run`, restarts
    the service, and verifies the tunnel comes up.

    Must be run from an elevated PowerShell.
#>

[CmdletBinding()]
param(
    [string]$ConfigPath = 'C:\Users\spare\.cloudflared\config.yml',
    [string]$ServiceKey = 'HKLM:\SYSTEM\CurrentControlSet\Services\cloudflared',
    [string]$BinaryPath = 'C:\Program Files (x86)\cloudflared\cloudflared.exe'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}
if (-not (Test-Path $BinaryPath)) {
    throw "cloudflared binary not found: $BinaryPath"
}

$desired = '"{0}" --config "{1}" tunnel run' -f $BinaryPath, $ConfigPath
$current = (Get-ItemProperty -Path $ServiceKey -Name ImagePath).ImagePath

Write-Host "Current ImagePath: $current"
Write-Host "Desired ImagePath: $desired"
Write-Host ''

if ($current -eq $desired) {
    Write-Host '[skip] ImagePath already correct.'
} else {
    Write-Host '[1/3] Stopping cloudflared service...'
    Stop-Service cloudflared -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    # If still wedged in STOP_PENDING, force-kill the process
    $proc = Get-Process -Name cloudflared -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "    process still running (PID $($proc.Id)) — killing"
        Stop-Process -Id $proc.Id -Force
        Start-Sleep -Seconds 2
    }

    Write-Host '[2/3] Patching ImagePath in registry...'
    Set-ItemProperty -Path $ServiceKey -Name ImagePath -Value $desired

    Write-Host '[3/3] Starting cloudflared service...'
    Start-Service cloudflared
}

Start-Sleep -Seconds 4
$svc = Get-Service cloudflared
Write-Host "Service status: $($svc.Status)"

Write-Host ''
Write-Host '=== tunnel info (waiting up to 30s for connections) ==='
for ($i = 0; $i -lt 6; $i++) {
    $info = & $BinaryPath tunnel info ga-demos 2>&1
    if ($info -notmatch 'does not have any active connection') {
        $info | Select-Object -First 12 | ForEach-Object { Write-Host $_ }
        break
    }
    Write-Host "tick $($i+1): no connections yet"
    Start-Sleep -Seconds 5
}

Write-Host ''
Write-Host '=== curl https://demos.guitaralchemist.com ==='
try {
    $resp = Invoke-WebRequest -Uri 'https://demos.guitaralchemist.com' -Method Head -TimeoutSec 10
    Write-Host "HTTP $($resp.StatusCode) $($resp.StatusDescription)"
} catch {
    Write-Host "Failed: $_"
}
