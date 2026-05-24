<#
.SYNOPSIS
    Install Guitar Alchemist platform processes as non-admin Windows services.

.DESCRIPTION
    Registers three NSSM-managed Windows services so the production trio
    (Vite frontend, GaApi backend, GaChatbot.Api backend) runs under a
    dedicated low-privilege service account. Each service is independent
    so an agent can kill or restart one without elevation and without
    taking the other two down.

    Services registered:
      GA-Vite-5176       (Apps/ga-react-components, pnpm dev, port 5176)
      GA-Api-5232        (GaApi, dotnet run, port 5232)
      GA-Chatbot-5252    (GaChatbot.Api, dotnet run, port 5252)

    The Cloudflared tunnel is intentionally NOT wrapped here. Use
    `cloudflared service install` for that — it ships its own native
    Windows service installer and runs as LOCAL SYSTEM by design.

    This script is the RESEARCH + SCAFFOLD output for harness item #8
    in docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md.
    It MUST be run by the user (requires admin to register services).
    Always dry-run with -WhatIf first.

.PARAMETER Install
    Register the three services. Honours -WhatIf.

.PARAMETER Uninstall
    Remove the three services. Honours -WhatIf. Prefer the dedicated
    Scripts/uninstall-ga-service.ps1 for symmetry.

.PARAMETER Start
    Start all three services (or those installed). Honours -WhatIf.

.PARAMETER Stop
    Stop all three services. Honours -WhatIf.

.PARAMETER Status
    Show service status, port listeners, and PID per service. Read-only.

.PARAMETER ServiceAccount
    Windows account that owns the service processes. Defaults to
    'NT AUTHORITY\NetworkService' — a built-in low-privilege account
    that does NOT require AD setup. To use a dedicated local user
    instead, pass e.g. '.\ga-service' and supply -ServiceAccountPassword.

.PARAMETER ServiceAccountPassword
    Password for ServiceAccount if it's not a built-in virtual account.
    SecureString. Ignored for NetworkService / LocalService.

.PARAMETER NssmPath
    Path to nssm.exe. Defaults to 'nssm' (looked up on PATH).
    Install NSSM with: winget install nssm

.EXAMPLE
    # DRY RUN — see what would happen, change nothing.
    .\install-ga-service.ps1 -Install -WhatIf

.EXAMPLE
    # Real install under NetworkService (no AD account needed).
    .\install-ga-service.ps1 -Install

.EXAMPLE
    # Real install under a dedicated local account.
    $pwd = Read-Host -AsSecureString "ga-service password"
    .\install-ga-service.ps1 -Install -ServiceAccount '.\ga-service' -ServiceAccountPassword $pwd

.EXAMPLE
    .\install-ga-service.ps1 -Status

.NOTES
    See docs/runbooks/non-admin-service-install.md for the full runbook,
    rationale, and rollback procedure.
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Start,
    [switch]$Stop,
    [switch]$Status,
    [string]$ServiceAccount = 'NT AUTHORITY\NetworkService',
    [System.Security.SecureString]$ServiceAccountPassword,
    [string]$NssmPath = 'nssm'
)

$ErrorActionPreference = 'Stop'

# ---------- repo paths ----------
$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$RepoRoot\AllProjects.slnx")) {
    throw "Cannot locate AllProjects.slnx from $RepoRoot. Run this script from Scripts/."
}
$LogDir = Join-Path $RepoRoot 'logs\services'

# ---------- service definitions ----------
# Each entry describes one independent NSSM-managed service.
# WorkingDir, Exe and Args are passed verbatim to nssm.
$Services = @(
    @{
        Name        = 'GA-Vite-5176'
        Display     = 'Guitar Alchemist - Vite Frontend (5176)'
        Description = 'pnpm dev for ga-react-components, port 5176. Part of demos.guitaralchemist.com.'
        WorkingDir  = Join-Path $RepoRoot 'Apps\ga-react-components'
        Exe         = 'pnpm.cmd'
        Args        = 'dev --host --port 5176'
        Port        = 5176
        StdOut      = 'vite.log'
        StdErr      = 'vite.err.log'
    }
    @{
        Name        = 'GA-Api-5232'
        Display     = 'Guitar Alchemist - GaApi (5232)'
        Description = 'GaApi backend, dotnet run, port 5232. Music theory + voicings API.'
        WorkingDir  = Join-Path $RepoRoot 'GaApi'
        Exe         = 'dotnet.exe'
        Args        = 'run --project GaApi.csproj --no-build --urls http://0.0.0.0:5232'
        Port        = 5232
        StdOut      = 'gaapi.log'
        StdErr      = 'gaapi.err.log'
    }
    @{
        Name        = 'GA-Chatbot-5252'
        Display     = 'Guitar Alchemist - GaChatbot.Api (5252)'
        Description = 'Public chatbot host, dotnet run, port 5252.'
        WorkingDir  = Join-Path $RepoRoot 'GaChatbot.Api'
        Exe         = 'dotnet.exe'
        Args        = 'run --project GaChatbot.Api.csproj --no-build --urls http://0.0.0.0:5252'
        Port        = 5252
        StdOut      = 'chatbot.log'
        StdErr      = 'chatbot.err.log'
    }
)

# ---------- helpers ----------
function Test-NssmAvailable {
    $cmd = Get-Command $NssmPath -ErrorAction SilentlyContinue
    if (-not $cmd) {
        Write-Host "NSSM not found on PATH. Install with: winget install nssm" -ForegroundColor Yellow
        Write-Host "Or download from https://nssm.cc/ and pass -NssmPath C:\path\to\nssm.exe" -ForegroundColor Yellow
        return $false
    }
    return $true
}

function Test-IsElevated {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = New-Object System.Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-Nssm {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$NssmArgs)
    & $NssmPath @NssmArgs
    if ($LASTEXITCODE -ne 0) {
        throw "nssm exited with code $LASTEXITCODE - args: $($NssmArgs -join ' ')"
    }
}

function Get-PortPid {
    param([int]$Port)
    $line = netstat -ano | Select-String -Pattern (":{0}\s" -f $Port) | Select-Object -First 1
    if (-not $line) { return $null }
    $tokens = -split $line.ToString()
    return $tokens[-1]
}

# ---------- STATUS (read-only, always safe) ----------
if ($Status) {
    Write-Host ""
    Write-Host "Guitar Alchemist service status" -ForegroundColor Cyan
    Write-Host "Account: $ServiceAccount" -ForegroundColor DarkGray
    Write-Host ""
    foreach ($svc in $Services) {
        $win = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
        $portPid = Get-PortPid -Port $svc.Port
        $svcState = if ($win) { $win.Status } else { 'NOT INSTALLED' }
        $portInfo = if ($portPid) { "port $($svc.Port) PID $portPid" } else { "port $($svc.Port) idle" }
        $color = if ($win -and $win.Status -eq 'Running') { 'Green' } elseif (-not $win) { 'Yellow' } else { 'Red' }
        Write-Host ("  {0,-18} {1,-13} {2}" -f $svc.Name, $svcState, $portInfo) -ForegroundColor $color
    }
    Write-Host ""
    Write-Host "Cloudflared tunnel (managed separately):" -ForegroundColor DarkGray
    $cf = Get-Service -Name 'Cloudflared' -ErrorAction SilentlyContinue
    if ($cf) {
        $cfColor = if ($cf.Status -eq 'Running') { 'Green' } else { 'Red' }
        Write-Host ("  {0,-18} {1}" -f 'Cloudflared', $cf.Status) -ForegroundColor $cfColor
    } else {
        Write-Host "  Cloudflared       NOT INSTALLED - install with: cloudflared service install" -ForegroundColor Yellow
    }
    return
}

# ---------- INSTALL ----------
if ($Install) {
    $nssmOk = Test-NssmAvailable
    if (-not $nssmOk -and -not $WhatIfPreference) { return }
    if (-not $nssmOk) {
        Write-Host "(continuing in -WhatIf mode for dry-run preview without NSSM)" -ForegroundColor DarkGray
    }
    if (-not (Test-IsElevated)) {
        Write-Host "Service registration requires an elevated PowerShell." -ForegroundColor Yellow
        Write-Host "Re-run this script from an admin PowerShell." -ForegroundColor Yellow
        if (-not $WhatIfPreference) { return }
        Write-Host "(continuing in -WhatIf mode for dry-run preview)" -ForegroundColor DarkGray
    }

    if ($PSCmdlet.ShouldProcess($LogDir, "Create log directory")) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }

    foreach ($svc in $Services) {
        $target = "service $($svc.Name) [$($svc.Exe) $($svc.Args)] in $($svc.WorkingDir) as $ServiceAccount"
        if (-not $PSCmdlet.ShouldProcess($target, "nssm install")) { continue }

        $existing = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
        if ($existing) {
            Write-Host "Service $($svc.Name) already exists - skipping install. Remove with -Uninstall first." -ForegroundColor Yellow
            continue
        }

        # Resolve exe path (NSSM needs absolute)
        $exeCmd = Get-Command $svc.Exe -ErrorAction SilentlyContinue
        if (-not $exeCmd) { throw "Cannot find $($svc.Exe) on PATH for service $($svc.Name)." }
        $exeAbs = $exeCmd.Source

        Invoke-Nssm install $svc.Name $exeAbs $svc.Args
        Invoke-Nssm set $svc.Name DisplayName $svc.Display
        Invoke-Nssm set $svc.Name Description $svc.Description
        Invoke-Nssm set $svc.Name AppDirectory $svc.WorkingDir
        Invoke-Nssm set $svc.Name AppStdout (Join-Path $LogDir $svc.StdOut)
        Invoke-Nssm set $svc.Name AppStderr (Join-Path $LogDir $svc.StdErr)
        Invoke-Nssm set $svc.Name AppRotateFiles 1
        Invoke-Nssm set $svc.Name AppRotateBytes 10485760  # 10MB
        Invoke-Nssm set $svc.Name Start SERVICE_AUTO_START
        Invoke-Nssm set $svc.Name AppExit Default Restart
        Invoke-Nssm set $svc.Name AppRestartDelay 5000

        # ObjectName = service account.
        $builtIn = @('NT AUTHORITY\NetworkService','NT AUTHORITY\LocalService','LocalSystem')
        if ($ServiceAccount -in $builtIn) {
            Invoke-Nssm set $svc.Name ObjectName $ServiceAccount
        }
        elseif ($ServiceAccountPassword) {
            $plain = [System.Net.NetworkCredential]::new('', $ServiceAccountPassword).Password
            Invoke-Nssm set $svc.Name ObjectName $ServiceAccount $plain
        }
        else {
            throw "ServiceAccount '$ServiceAccount' is not a built-in virtual account; pass -ServiceAccountPassword."
        }

        Write-Host "Installed $($svc.Name) as $ServiceAccount" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Grant the service account read access to $RepoRoot (icacls if needed)."
    Write-Host "  2. Install cloudflared as a service:  cloudflared service install"
    Write-Host "  3. Start the platform:                .\install-ga-service.ps1 -Start"
    Write-Host "  4. Verify:                            .\install-ga-service.ps1 -Status"
    return
}

# ---------- UNINSTALL ----------
if ($Uninstall) {
    $nssmOk = Test-NssmAvailable
    if (-not $nssmOk -and -not $WhatIfPreference) { return }
    foreach ($svc in $Services) {
        $existing = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
        if (-not $existing) {
            Write-Host "$($svc.Name) not installed - skipping." -ForegroundColor DarkGray
            continue
        }
        if (-not $PSCmdlet.ShouldProcess($svc.Name, "nssm stop + remove")) { continue }
        try { Invoke-Nssm stop $svc.Name } catch { Write-Host "stop failed (already stopped?): $_" -ForegroundColor DarkGray }
        Invoke-Nssm remove $svc.Name confirm
        Write-Host "Removed $($svc.Name)" -ForegroundColor Green
    }
    return
}

# ---------- START ----------
if ($Start) {
    if (-not (Test-NssmAvailable)) { return }
    foreach ($svc in $Services) {
        $existing = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
        if (-not $existing) {
            Write-Host "$($svc.Name) not installed - run -Install first." -ForegroundColor Yellow
            continue
        }
        if (-not $PSCmdlet.ShouldProcess($svc.Name, "nssm start")) { continue }
        Invoke-Nssm start $svc.Name
        Write-Host "Started $($svc.Name)" -ForegroundColor Green
    }
    return
}

# ---------- STOP ----------
if ($Stop) {
    if (-not (Test-NssmAvailable)) { return }
    foreach ($svc in $Services) {
        $existing = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
        if (-not $existing) { continue }
        if (-not $PSCmdlet.ShouldProcess($svc.Name, "nssm stop")) { continue }
        try { Invoke-Nssm stop $svc.Name } catch { Write-Host "stop failed: $_" -ForegroundColor DarkGray }
        Write-Host "Stopped $($svc.Name)" -ForegroundColor Green
    }
    return
}

Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  .\install-ga-service.ps1 -Install [-WhatIf] [-ServiceAccount '.\ga-service' -ServiceAccountPassword <secure>]"
Write-Host "  .\install-ga-service.ps1 -Uninstall [-WhatIf]"
Write-Host "  .\install-ga-service.ps1 -Start | -Stop | -Status"
Write-Host ""
Write-Host "Always dry-run with -WhatIf before -Install. See docs/runbooks/non-admin-service-install.md." -ForegroundColor DarkGray
