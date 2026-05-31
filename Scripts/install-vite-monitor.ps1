# install-vite-monitor.ps1
#
# Registers vite-health-monitor.ps1 as a user-context scheduled task that
# starts at logon and restarts on failure. No admin / UAC required.
#
# Run: pwsh -NoProfile -File Scripts\install-vite-monitor.ps1
# Uninstall: pwsh -NoProfile -File Scripts\install-vite-monitor.ps1 -Uninstall

[CmdletBinding()]
param([switch]$Uninstall)

$taskName = 'GA-Vite-Health-Monitor'
$scriptPath = Join-Path $PSScriptRoot 'vite-health-monitor.ps1'

if ($Uninstall) {
  schtasks /Delete /TN $taskName /F
  Write-Host "Uninstalled scheduled task: $taskName"
  exit 0
}

if (-not (Test-Path $scriptPath)) {
  throw "vite-health-monitor.ps1 not found at $scriptPath"
}

$tr = "pwsh -NoProfile -WindowStyle Hidden -File `"$scriptPath`""
schtasks /Create /TN $taskName /SC ONLOGON /TR $tr /RL LIMITED /F | Out-Null
schtasks /Change /TN $taskName /ENABLE | Out-Null

Write-Host "Registered scheduled task: $taskName"
Write-Host "  Trigger: At logon"
Write-Host "  Action:  $tr"
Write-Host ""
Write-Host "Verify:    schtasks /Query /TN $taskName"
Write-Host "Run now:   schtasks /Run /TN $taskName"
Write-Host "Uninstall: pwsh -NoProfile -File Scripts\install-vite-monitor.ps1 -Uninstall"
