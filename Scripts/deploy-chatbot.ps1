#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy the public chatbot (GaChatbot.Api on :5252) from a clean git ref.
.DESCRIPTION
    Builds a git ref (default origin/main) in a dedicated detached worktree, publishes
    it to a release folder, switches the `current` junction to it, restarts the
    scheduled task that runs it and smoke-tests it with a real chat request. A failed
    smoke test switches back to the previous release.

    Layout under -DeployRoot (a worktree of this repository):
      .deploy/releases/<sha>/   one `dotnet publish` output per deployed commit
      .deploy/current           junction to the running release
      .deploy/run-chatbot.cmd   launcher run by the scheduled task (restarts on exit)
      .deploy/logs/             stdout, stderr and restart logs
    The host runs with the worktree root as its working directory, so it finds
    `state/` (telemetry) and `.git` (QA summary) there, and keeps them across releases.

    See docs/runbooks/chatbot-deploy.md.
.PARAMETER Ref
    Git ref to deploy. Fetched from origin first.
.PARAMETER DeployRoot
    Deploy worktree. Created on first use. Default: ga-deploy-chatbot next to the main checkout.
.PARAMETER TaskName
    Scheduled task that runs the launcher. Created or updated; see step 3.
.PARAMETER SmokePrompt
    Chat message sent to the new release.
.PARAMETER ExpectedAgent
    Agent that must answer -SmokePrompt.
.PARAMETER SkipPublicProbe
    Skip the probe through https://demos.guitaralchemist.com.
.PARAMETER KeepReleases
    Number of release folders kept after a successful deploy.
#>

param(
    [string]$Ref = 'origin/main',
    [string]$DeployRoot = '',
    [string]$TaskName = 'GA-Chatbot-5252',
    [int]$Port = 5252,
    [string]$SmokePrompt = 'which arpeggio fits Am F C G',
    [string]$ExpectedAgent = 'skill.improvisation',
    [string]$PublicHost = 'demos.guitaralchemist.com',
    [int]$StartupTimeoutSeconds = 120,
    [int]$KeepReleases = 3,
    [switch]$SkipPublicProbe
)

$ErrorActionPreference = 'Stop'
$repoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
# The main checkout holds the gitignored OPTK index; worktrees do not.
$mainCheckout = Split-Path -Parent (git -C $repoRoot rev-parse --path-format=absolute --git-common-dir).Trim()
if (-not $DeployRoot) { $DeployRoot = Join-Path (Split-Path -Parent $mainCheckout) 'ga-deploy-chatbot' }
$deployDir = Join-Path $DeployRoot '.deploy'
$releasesDir = Join-Path $deployDir 'releases'
$currentLink = Join-Path $deployDir 'current'
$logDir = Join-Path $deployDir 'logs'
$launcher = Join-Path $deployDir 'run-chatbot.cmd'
$localBase = "http://localhost:$Port"

function Invoke-Git {
    git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed with exit code $LASTEXITCODE" }
}

function Get-CurrentRelease {
    $item = Get-Item -LiteralPath $currentLink -ErrorAction SilentlyContinue
    if ($item -and $item.LinkType -eq 'Junction') { return [string]$item.Target }
    return $null
}

function Set-CurrentRelease([string]$Target) {
    # rmdir removes the junction itself, never the release it points to.
    if (Test-Path -LiteralPath $currentLink) { cmd /c rmdir "$currentLink" | Out-Null }
    New-Item -ItemType Junction -Path $currentLink -Target $Target | Out-Null
}

function Stop-Chatbot {
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    # A launcher loop that outlived its task would restart the host behind our back.
    Get-CimInstance Win32_Process -Filter "Name = 'cmd.exe' OR Name = 'conhost.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($launcher) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    # Then stop whatever still listens on the port.
    $owners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $owners) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process -and $process.ProcessName -eq 'GaChatbot.Api') {
            Write-Host "  Stopping GaChatbot.Api (PID $processId, $($process.Path))"
            Stop-Process -Id $processId -Force
        }
        elseif ($process) {
            throw "Port $Port is held by $($process.ProcessName) (PID $processId), not GaChatbot.Api; refusing to stop it."
        }
    }
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
}

function Start-Chatbot {
    Start-ScheduledTask -TaskName $TaskName
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            return Invoke-RestMethod -Uri "$localBase/api/chatbot/status" -TimeoutSec 30
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "GaChatbot.Api did not answer $localBase/api/chatbot/status within $StartupTimeoutSeconds s. See $logDir."
}

function Test-Release($Status) {
    $failures = @()
    if (-not $Status.isAvailable) { $failures += "status: isAvailable=false ($($Status.message))" }
    if ($Status | Get-Member -Name embeddingRoundTripOk -MemberType NoteProperty) {
        if ($Status.embeddingRoundTripOk -ne $true) { $failures += "status: embeddingRoundTripOk=$($Status.embeddingRoundTripOk)" }
    }
    else {
        Write-Warning 'This build predates the embedding readiness probe; relying on the chat smoke test.'
    }

    try {
        $body = @{ Message = $SmokePrompt } | ConvertTo-Json
        $chat = Invoke-RestMethod -Uri "$localBase/api/chatbot/chat" -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 180
        Write-Host "  Smoke: agent=$($chat.agentId) method=$($chat.routingMethod) elapsed=$($chat.elapsedMs)ms"
        if ([string]::IsNullOrWhiteSpace($chat.naturalLanguageAnswer)) { $failures += 'chat: empty answer' }
        if ($chat.agentId -ne $ExpectedAgent) { $failures += "chat: answered by '$($chat.agentId)', expected '$ExpectedAgent'" }
    }
    catch {
        $failures += "chat: $($_.Exception.Message)"
    }

    if (-not $SkipPublicProbe) {
        foreach ($path in '/chatbot/', '/api/chatbot/status') {
            try {
                $response = Invoke-WebRequest -Uri "https://$PublicHost$path" -UseBasicParsing -TimeoutSec 60
                if ($response.StatusCode -ne 200) { $failures += "public ${path}: HTTP $($response.StatusCode)" }
            }
            catch {
                $failures += "public ${path}: $($_.Exception.Message)"
            }
        }
    }
    return $failures
}

# 1. Resolve the ref and prepare the clean deploy worktree.
Write-Host "Deploying $Ref to $DeployRoot" -ForegroundColor Cyan
Invoke-Git -C $repoRoot fetch origin --quiet
$sha = (git -C $repoRoot rev-parse --verify "$Ref^{commit}").Trim()
if ($LASTEXITCODE -ne 0) { throw "Cannot resolve $Ref" }
$shortSha = $sha.Substring(0, 8)

if (-not (Test-Path -LiteralPath $DeployRoot)) {
    Invoke-Git -C $repoRoot worktree add --detach $DeployRoot $sha
}
else {
    if (-not (Test-Path -LiteralPath (Join-Path $DeployRoot '.git') -PathType Leaf)) {
        throw "$DeployRoot exists but is not a git worktree."
    }
    $dirty = git -C $DeployRoot status --porcelain --untracked-files=no
    if ($dirty) { throw "Deploy worktree has tracked changes; it must stay clean:`n$dirty" }
    Invoke-Git -C $DeployRoot checkout --detach --quiet $sha
}
Write-Host "  Commit: $(git -C $DeployRoot log -1 --format='%h %s')"

# 2. Publish to a release folder. The running release is untouched, so no file locks.
$release = Join-Path $releasesDir $shortSha
dotnet publish (Join-Path $DeployRoot 'Apps/GaChatbot.Api/GaChatbot.Api.csproj') -c Release -o $release --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# 3. Launcher and scheduled task (idempotent). The launcher loops so a crash restarts
#    the host. The task starts it at logon and again every 5 minutes in case the
#    launcher itself died; IgnoreNew makes a repetition a no-op while it runs.
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$opticIndex = Join-Path $mainCheckout 'state\voicings\optick.index'
$indexLine = if (Test-Path -LiteralPath $opticIndex) { "set GA_OPTICK_INDEX_PATH=$opticIndex" } else { 'rem optick.index not found; voicing search degrades to CPU' }
@"
@echo off
rem Generated by Scripts/deploy-chatbot.ps1. Rerun the script instead of editing this file.
set Chatbot__PathBase=/chatbot
set AI__CascadeProvider=mistral
set ASPNETCORE_URLS=$localBase
$indexLine
cd /d "$DeployRoot"
:run
"$currentLink\GaChatbot.Api.exe" >> "$logDir\chatbot.out.log" 2>> "$logDir\chatbot.err.log"
echo %date% %time% GaChatbot.Api exited with code %errorlevel%, restarting in 10 s >> "$logDir\restarts.log"
ping -n 11 127.0.0.1 > nul
goto run
"@ | Set-Content -LiteralPath $launcher -Encoding ascii

$user = "$env:USERDOMAIN\$env:USERNAME"
$taskAction = New-ScheduledTaskAction -Execute 'conhost.exe' -Argument "--headless cmd.exe /c `"$launcher`""
$taskTriggers = @(
    New-ScheduledTaskTrigger -AtLogOn -User $user
    New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(5) -RepetitionInterval (New-TimeSpan -Minutes 5)
)
$taskSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew `
    -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -StartWhenAvailable `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$taskPrincipal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited

# Registered before anything stops, so a refused registration leaves the old host serving.
Register-ScheduledTask -TaskName $TaskName -Action $taskAction -Trigger $taskTriggers -Settings $taskSettings `
    -Principal $taskPrincipal -Description "GaChatbot.Api on :$Port, deployed by Scripts/deploy-chatbot.ps1" -Force | Out-Null

# 4. Switch releases and verify; roll back on failure.
$previous = Get-CurrentRelease
Stop-Chatbot
Set-CurrentRelease $release

$failures = @()
try {
    $failures = @(Test-Release (Start-Chatbot))
}
catch {
    $failures = @($_.Exception.Message)
}

if ($failures.Count -gt 0) {
    Write-Host "Release $shortSha failed verification:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    if ($previous -and $previous -ne $release) {
        Write-Host "Rolling back to $previous" -ForegroundColor Yellow
        Stop-Chatbot
        Set-CurrentRelease $previous
        $null = Start-Chatbot
    }
    else {
        Write-Host 'No previous release to roll back to; the chatbot stays on the failed release.' -ForegroundColor Yellow
    }
    exit 1
}

# 5. Keep the newest releases, never the running or previous one.
Get-ChildItem -LiteralPath $releasesDir -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $KeepReleases |
    Where-Object { $_.FullName -ne $release -and $_.FullName -ne $previous } |
    Remove-Item -Recurse -Force

Write-Host "Deployed $shortSha; GaChatbot.Api is serving on $localBase." -ForegroundColor Green
Write-Host 'Next: run the session-cookie assertion (step 6) in docs/runbooks/chatbot-deploy.md.'
