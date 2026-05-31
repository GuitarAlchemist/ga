<#
.SYNOPSIS
    Uninstall the Guitar Alchemist non-admin Windows services.

.DESCRIPTION
    Convenience wrapper around `install-ga-service.ps1 -Uninstall`.
    Removes the three NSSM-managed services (GA-Vite-5176, GA-Api-5232,
    GA-Chatbot-5252). Does NOT touch the Cloudflared service - remove
    that separately with `cloudflared service uninstall`.

    Always dry-run with -WhatIf first.

.PARAMETER NssmPath
    Path to nssm.exe. Defaults to 'nssm' on PATH.

.PARAMETER IncludeCloudflared
    Also run `cloudflared service uninstall`. Off by default because
    the tunnel often outlives the local services.

.EXAMPLE
    .\uninstall-ga-service.ps1 -WhatIf

.EXAMPLE
    .\uninstall-ga-service.ps1

.EXAMPLE
    .\uninstall-ga-service.ps1 -IncludeCloudflared

.NOTES
    See docs/runbooks/non-admin-service-install.md for the rollback
    procedure including log-file cleanup and ACL revert.
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$NssmPath = 'nssm',
    [switch]$IncludeCloudflared
)

$ErrorActionPreference = 'Stop'

$installScript = Join-Path $PSScriptRoot 'install-ga-service.ps1'
if (-not (Test-Path $installScript)) {
    throw "install-ga-service.ps1 not found next to this script at $installScript"
}

# Forward -WhatIf via $WhatIfPreference (CmdletBinding inherits common params).
& $installScript -Uninstall -NssmPath $NssmPath -WhatIf:$WhatIfPreference

if ($IncludeCloudflared) {
    $cf = Get-Service -Name 'Cloudflared' -ErrorAction SilentlyContinue
    if ($cf) {
        if ($PSCmdlet.ShouldProcess('Cloudflared', 'cloudflared service uninstall')) {
            & cloudflared service uninstall
            if ($LASTEXITCODE -ne 0) {
                Write-Host "cloudflared service uninstall exited with $LASTEXITCODE" -ForegroundColor Yellow
            } else {
                Write-Host "Removed Cloudflared service" -ForegroundColor Green
            }
        }
    } else {
        Write-Host "Cloudflared service not installed - nothing to remove." -ForegroundColor DarkGray
    }
}
