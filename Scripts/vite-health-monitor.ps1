# vite-health-monitor.ps1
#
# Polls localhost:5176 every 30s. If Vite is down, restarts `npm run dev` in
# ReactComponents/ga-react-components and emits an algedonic signal so the
# operator sees the restart on the dashboard. Designed to run as a logon-time
# scheduled task (see Scripts/install-vite-monitor.ps1).
#
# Why this exists: the demos.guitaralchemist.com tunnel terminates at local
# Vite on 5176. Vite occasionally crashes/exits leaving CF returning 502.
# Until the Tier-2 NSSM service install lands (requires admin UAC), this
# is the runtime safety net.

[CmdletBinding()]
param(
  [int]$IntervalSeconds = 30,
  [int]$Port = 5176,
  [string]$WorkingDir = (Join-Path $PSScriptRoot '..\ReactComponents\ga-react-components'),
  [int]$StartupWaitSeconds = 15
)

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot
$logFile = Join-Path $repoRoot 'state\health\vite-monitor.log'
$algedonicInbox = Join-Path $repoRoot 'state\algedonic\inbox.jsonl'
New-Item -ItemType Directory -Force -Path (Split-Path $logFile) | Out-Null

function Write-Log {
  param([string]$Level, [string]$Message)
  $line = "$(Get-Date -Format 'o') $Level $Message"
  Add-Content -Path $logFile -Value $line
  Write-Host $line
}

function Test-ViteUp {
  try {
    $r = Invoke-WebRequest -Uri "http://localhost:$Port" -Method Head -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
    return $r.StatusCode -eq 200
  } catch {
    return $false
  }
}

function Emit-Algedonic {
  param([string]$Severity, [string]$Summary, [string]$Details)
  $signal = [PSCustomObject]@{
    id = [guid]::NewGuid().ToString()
    schema = 'algedonic-signal-v0.1.0'
    emitted_at = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    repo = 'ga'
    source = 'vite-health-monitor'
    severity = $Severity
    summary = $Summary
    details = $Details
    evidence_url = $null
    affected_artifacts = @('Scripts/vite-health-monitor.ps1')
    ttl_hours = 24
    escalation = @{ on_unack_after_hours = $null; route_to = 'operator' }
    ack = @{ acked = $false; acked_by = $null; acked_at = $null; resolution = $null }
    supersedes = @()
  }
  $json = $signal | ConvertTo-Json -Compress -Depth 4
  Add-Content -Path $algedonicInbox -Value $json
}

function Restart-Vite {
  Write-Log 'WARN' "Vite down on :$Port — restarting `npm run dev` in $WorkingDir"
  Push-Location $WorkingDir
  try {
    Start-Process -FilePath 'cmd' -ArgumentList '/c', 'npm', 'run', 'dev' -WindowStyle Hidden
  } finally {
    Pop-Location
  }
  Emit-Algedonic -Severity 'warn' -Summary "[vite-monitor] Vite restarted on :$Port" -Details "Vite dev server was unreachable; restarted via npm run dev. If this fires repeatedly within 5 minutes, investigate the underlying crash."
  Start-Sleep -Seconds $StartupWaitSeconds
}

Write-Log 'INFO' "vite-health-monitor started — polling :$Port every $IntervalSeconds s"
$consecutiveFailures = 0
while ($true) {
  if (Test-ViteUp) {
    if ($consecutiveFailures -gt 0) {
      Write-Log 'INFO' "Vite back up after $consecutiveFailures consecutive failures"
      $consecutiveFailures = 0
    }
  } else {
    $consecutiveFailures++
    Write-Log 'WARN' "Vite probe failed (consecutive=$consecutiveFailures)"
    if ($consecutiveFailures -ge 2) {
      Restart-Vite
      $consecutiveFailures = 0
    }
  }
  Start-Sleep -Seconds $IntervalSeconds
}
