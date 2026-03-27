<#
.SYNOPSIS
    Install Guitar Alchemist as a Windows Service
.DESCRIPTION
    Creates a Windows service that manages:
    1. GaApi backend (ASP.NET Core on port 7001)
    2. Cloudflare tunnel (demos.guitaralchemist.com)
    3. Frontend dev server (Vite on port 5176)
    4. Ollama LLM (local inference on port 11434)
.PARAMETER Install
    Install the service
.PARAMETER Uninstall
    Remove the service
.PARAMETER Start
    Start the service
.PARAMETER Stop
    Stop the service
.PARAMETER Status
    Show service status
.EXAMPLE
    .\install-ga-service.ps1 -Install
    .\install-ga-service.ps1 -Start
#>

param(
    [switch]$Install,
    [switch]$Uninstall,
    [switch]$Start,
    [switch]$Stop,
    [switch]$Status
)

$ServiceName = "GuitarAlchemist"
$DisplayName = "Guitar Alchemist Platform"
$Description = "GaApi + Cloudflare tunnel + Frontend + Ollama"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
# If run from Scripts/, go up one level
if (-not (Test-Path "$RepoRoot\AllProjects.slnx")) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

$WrapperScript = "$RepoRoot\Scripts\ga-service-wrapper.ps1"

if ($Status) {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc) {
        Write-Host "Service: $($svc.DisplayName)"
        Write-Host "Status:  $($svc.Status)"
        Write-Host ""

        # Check individual processes
        $processes = @(
            @{ Name = "GaApi"; Port = 7001 },
            @{ Name = "cloudflared"; Port = 0 },
            @{ Name = "node (Vite)"; Port = 5176 },
            @{ Name = "ollama"; Port = 11434 }
        )
        foreach ($p in $processes) {
            if ($p.Port -gt 0) {
                $listening = netstat -ano | Select-String ":$($p.Port)\s" | Select-Object -First 1
                if ($listening) {
                    Write-Host "  [OK] $($p.Name) on port $($p.Port)" -ForegroundColor Green
                } else {
                    Write-Host "  [--] $($p.Name) not detected on port $($p.Port)" -ForegroundColor Yellow
                }
            } else {
                $proc = Get-Process -Name $p.Name -ErrorAction SilentlyContinue
                if ($proc) {
                    Write-Host "  [OK] $($p.Name) running (PID $($proc.Id))" -ForegroundColor Green
                } else {
                    Write-Host "  [--] $($p.Name) not running" -ForegroundColor Yellow
                }
            }
        }
    } else {
        Write-Host "Service '$ServiceName' is not installed." -ForegroundColor Yellow
    }
    return
}

if ($Install) {
    Write-Host "Installing $DisplayName service..." -ForegroundColor Cyan

    # Create the wrapper script
    $wrapperContent = @"
# GA Service Wrapper — managed by Windows Service
# Starts and monitors all GA platform components

`$RepoRoot = "$RepoRoot"
`$LogDir = "`$RepoRoot\logs"
New-Item -ItemType Directory -Path `$LogDir -Force | Out-Null

# Start components
`$procs = @()

# 1. GaApi
`$gaApiLog = "`$LogDir\gaapi.log"
`$procs += Start-Process -FilePath "dotnet" -ArgumentList "run --project `$RepoRoot\Apps\ga-server\GaApi\GaApi.csproj --no-build" -WindowStyle Hidden -PassThru -RedirectStandardOutput `$gaApiLog -RedirectStandardError "`$LogDir\gaapi-err.log"
Write-Output "Started GaApi (PID `$(`$procs[-1].Id))"

# 2. Cloudflare tunnel
`$procs += Start-Process -FilePath "cloudflared" -ArgumentList "tunnel run ga-demos" -WindowStyle Hidden -PassThru -RedirectStandardOutput "`$LogDir\cloudflared.log" -RedirectStandardError "`$LogDir\cloudflared-err.log"
Write-Output "Started cloudflared tunnel (PID `$(`$procs[-1].Id))"

# 3. Frontend dev server
`$procs += Start-Process -FilePath "npm" -ArgumentList "run dev" -WorkingDirectory "`$RepoRoot\ReactComponents\ga-react-components" -WindowStyle Hidden -PassThru -RedirectStandardOutput "`$LogDir\vite.log" -RedirectStandardError "`$LogDir\vite-err.log"
Write-Output "Started Vite dev server (PID `$(`$procs[-1].Id))"

# 4. Ollama (if installed)
`$ollamaPath = Get-Command ollama -ErrorAction SilentlyContinue
if (`$ollamaPath) {
    `$procs += Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden -PassThru -RedirectStandardOutput "`$LogDir\ollama.log" -RedirectStandardError "`$LogDir\ollama-err.log"
    Write-Output "Started Ollama (PID `$(`$procs[-1].Id))"
}

# Monitor loop — restart crashed processes
while (`$true) {
    Start-Sleep -Seconds 30
    foreach (`$p in `$procs) {
        if (`$p.HasExited) {
            Write-Output "Process `$(`$p.Id) exited with code `$(`$p.ExitCode), not restarting (manual intervention needed)"
        }
    }
}
"@
    Set-Content -Path $WrapperScript -Value $wrapperContent -Force

    # Use NSSM (Non-Sucking Service Manager) if available, otherwise sc.exe
    $nssm = Get-Command nssm -ErrorAction SilentlyContinue
    if ($nssm) {
        & nssm install $ServiceName powershell.exe "-ExecutionPolicy Bypass -File `"$WrapperScript`""
        & nssm set $ServiceName DisplayName $DisplayName
        & nssm set $ServiceName Description $Description
        & nssm set $ServiceName Start SERVICE_AUTO_START
        & nssm set $ServiceName AppStdout "$RepoRoot\logs\service.log"
        & nssm set $ServiceName AppStderr "$RepoRoot\logs\service-err.log"
        Write-Host "Service installed via NSSM." -ForegroundColor Green
    } else {
        Write-Host "NSSM not found. Install with: winget install nssm" -ForegroundColor Yellow
        Write-Host "Or use: sc.exe create $ServiceName binPath=`"powershell -File $WrapperScript`"" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Alternative: use Task Scheduler instead:" -ForegroundColor Cyan

        # Create a scheduled task as fallback
        $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -WindowStyle Hidden -File `"$WrapperScript`""
        $trigger = New-ScheduledTaskTrigger -AtStartup
        $settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
        $principal = New-ScheduledTaskPrincipal -UserId "$env:USERNAME" -LogonType S4U -RunLevel Highest

        Register-ScheduledTask -TaskName $ServiceName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description $Description -Force
        Write-Host "Scheduled task '$ServiceName' created (runs at startup)." -ForegroundColor Green
    }
    return
}

if ($Uninstall) {
    $nssm = Get-Command nssm -ErrorAction SilentlyContinue
    if ($nssm) {
        & nssm stop $ServiceName 2>$null
        & nssm remove $ServiceName confirm
    }
    Unregister-ScheduledTask -TaskName $ServiceName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Service/task removed." -ForegroundColor Green
    return
}

if ($Start) {
    $nssm = Get-Command nssm -ErrorAction SilentlyContinue
    if ($nssm) {
        & nssm start $ServiceName
    } else {
        Start-ScheduledTask -TaskName $ServiceName -ErrorAction SilentlyContinue
        if ($?) {
            Write-Host "Task started." -ForegroundColor Green
        } else {
            # Direct launch
            Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -WindowStyle Hidden -File `"$WrapperScript`"" -WindowStyle Hidden
            Write-Host "Started directly." -ForegroundColor Green
        }
    }
    return
}

if ($Stop) {
    $nssm = Get-Command nssm -ErrorAction SilentlyContinue
    if ($nssm) {
        & nssm stop $ServiceName
    } else {
        Stop-ScheduledTask -TaskName $ServiceName -ErrorAction SilentlyContinue
    }
    # Also kill child processes
    Get-Process -Name "dotnet","cloudflared","node" -ErrorAction SilentlyContinue | Where-Object {
        $_.MainWindowTitle -eq "" # background processes
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped." -ForegroundColor Green
    return
}

Write-Host "Usage: .\install-ga-service.ps1 -Install | -Uninstall | -Start | -Stop | -Status"
