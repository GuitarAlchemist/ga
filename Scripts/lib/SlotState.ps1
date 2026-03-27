# Scripts/lib/SlotState.ps1
# Shared functions for blue/green slot management

$Script:SolutionRoot = (Resolve-Path "$PSScriptRoot\..").Path
$Script:SlotTargetPath = Join-Path $SolutionRoot ".slot-target"
$Script:SlotStatePath = Join-Path $SolutionRoot ".slot-state.json"

function Get-SlotTarget {
    if (Test-Path $Script:SlotTargetPath) {
        return (Get-Content $Script:SlotTargetPath -Raw).Trim()
    }
    return $null
}

function Set-SlotTarget {
    param([ValidateSet("blue","green")][string]$Slot)
    $temp = "$Script:SlotTargetPath.tmp"
    Set-Content -Path $temp -Value $Slot -NoNewline
    Move-Item -Path $temp -Destination $Script:SlotTargetPath -Force
}

function Get-SlotState {
    if (Test-Path $Script:SlotStatePath) {
        return Get-Content $Script:SlotStatePath -Raw | ConvertFrom-Json
    }
    return $null
}

function Set-SlotState {
    param([PSCustomObject]$State)
    $temp = "$Script:SlotStatePath.tmp"
    $State | ConvertTo-Json -Depth 5 | Set-Content -Path $temp
    Move-Item -Path $temp -Destination $Script:SlotStatePath -Force
}

function Get-ActiveSlot {
    $state = Get-SlotState
    if ($state) { return $state.activeSlot }
    return "blue"
}

function Get-InactiveSlot {
    $active = Get-ActiveSlot
    return $(if ($active -eq "blue") { "green" } else { "blue" })
}

function Get-GaApiBinPath {
    return Join-Path $Script:SolutionRoot "Apps\ga-server\GaApi\bin"
}

function Get-SlotBinPath {
    param([ValidateSet("blue","green")][string]$Slot)
    return Join-Path (Get-GaApiBinPath) $Slot
}

function Get-ActiveBinPath {
    return Join-Path (Get-GaApiBinPath) "active"
}

function Test-Junction {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $false }
    $item = Get-Item $Path -Force
    return ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
}

function New-Junction {
    param([string]$Link, [string]$Target)
    cmd /c "mklink /J `"$Link`" `"$Target`"" | Out-Null
    return $LASTEXITCODE -eq 0
}

function Remove-Junction {
    param([string]$Path)
    if (Test-Junction $Path) {
        cmd /c rmdir "`"$Path`"" | Out-Null
        return $LASTEXITCODE -eq 0
    }
    return $true
}

function Test-ServerHealth {
    param([int]$TimeoutSeconds = 30, [int]$Port = 5232)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:$Port/api/health/ping" -TimeoutSec 3 -ErrorAction Stop
            return $true
        } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Get-GaApiProcess {
    Get-Process -Name "GaApi" -ErrorAction SilentlyContinue
}

function Stop-GaApiServer {
    param([int]$GracePeriod = 10)
    $proc = Get-GaApiProcess
    if ($proc) {
        $proc | Stop-Process -Force:$false
        if (-not $proc.WaitForExit($GracePeriod * 1000)) {
            $proc | Stop-Process -Force
        }
        Write-Host "  Server stopped (PID $($proc.Id))" -ForegroundColor Yellow
    }
}

function New-SlotState {
    return [PSCustomObject]@{
        activeSlot      = "blue"
        lastSwap        = $null
        lastBuild       = $null
        buildTarget     = "green"
        trustedBaseline = $null
        slots           = [PSCustomObject]@{
            blue  = [PSCustomObject]@{ builtAt = $null; healthy = $null; commitHash = $null }
            green = [PSCustomObject]@{ builtAt = $null; healthy = $null; commitHash = $null }
        }
    }
}
