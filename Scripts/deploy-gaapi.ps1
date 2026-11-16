#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy GaApi as a production host on :5262 from a clean git ref.
.DESCRIPTION
    Production twin of the GaApi that the Aspire dev stack runs on :5232. It exists so
    the public chat routes (/chatbot/*, /api/chatbot/*, /api/a2a/*) can move from
    GaChatbot.Api (:5252) to GaApi (ADR-0005) without pointing the tunnel at a dev
    process. Same mechanics as Scripts/deploy-chatbot.ps1: builds a git ref (default
    origin/main) in a dedicated detached worktree, publishes it to a release folder,
    switches the `current` junction to it, restarts the scheduled task that runs it and
    smoke-tests it with a real chat request. A failed smoke test switches back to the
    previous release.

    Layout under -DeployRoot (a worktree of this repository):
      .deploy/releases/<sha>/   one `dotnet publish` output per deployed commit
      .deploy/current           junction to the running release
      .deploy/work/             working directory of the host (see below)
      .deploy/run-gaapi.cmd     launcher run by the scheduled task (restarts on exit)
      .deploy/local-env.cmd     optional, operator-owned: `set` lines the launcher calls
                                before the first start (e.g. Ollama__ChatModel).
                                Never generated or overwritten; never put secrets in it.
      .deploy/logs/             stdout, stderr and restart logs (rotated at 10 MB, one old copy)

    GaApi writes caches and a SQLite file relative to its working directory, so it runs
    from .deploy/work, which survives releases and is outside the clean-worktree check.
    Its content root (appsettings.json, wwwroot) is the release folder.

    See docs/runbooks/gaapi-deploy.md.
.PARAMETER Ref
    Git ref to deploy. Fetched from origin first.
.PARAMETER DeployRoot
    Deploy worktree. Created on first use. Default: ga-deploy-gaapi next to the main checkout.
.PARAMETER TaskName
    Scheduled task that runs the launcher. Created or updated; see step 3.
.PARAMETER SmokePrompt
    Chat message sent to the new release.
.PARAMETER ExpectedAgent
    Agent that must answer -SmokePrompt.
.PARAMETER PublicProbe
    Also probe https://<PublicHost>/chatbot/ and /api/chatbot/status. Only meaningful once
    cloudflared routes those paths to this port.
.PARAMETER KeepReleases
    Number of release folders kept after a successful deploy.
#>

param(
    [string]$Ref = 'origin/main',
    [string]$DeployRoot = '',
    [string]$TaskName = 'GA-GaApi-5262',
    [int]$Port = 5262,
    [string]$SmokePrompt = 'which arpeggio fits Am F C G',
    [string]$ExpectedAgent = 'skill.improvisation',
    [string]$PublicHost = 'demos.guitaralchemist.com',
    [int]$StartupTimeoutSeconds = 180,
    [int]$KeepReleases = 3,
    [switch]$PublicProbe
)

$ErrorActionPreference = 'Stop'
$repoRoot = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
# The main checkout holds the gitignored OPTK index; worktrees do not.
$mainCheckout = Split-Path -Parent (git -C $repoRoot rev-parse --path-format=absolute --git-common-dir).Trim()
if (-not $DeployRoot) { $DeployRoot = Join-Path (Split-Path -Parent $mainCheckout) 'ga-deploy-gaapi' }
$deployDir = Join-Path $DeployRoot '.deploy'
$releasesDir = Join-Path $deployDir 'releases'
$currentLink = Join-Path $deployDir 'current'
$workDir = Join-Path $deployDir 'work'
$logDir = Join-Path $deployDir 'logs'
$launcher = Join-Path $deployDir 'run-gaapi.cmd'
$localBase = "http://localhost:$Port"
# Outside Development, GaApi answers 401 on /api/chatbot/* unless an X-Api-Key header is
# present (any value; see Program.cs). The smoke test is a direct local caller, so it sends one.
$smokeHeaders = @{ 'X-Api-Key' = 'deploy-gaapi-smoke' }

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

function Stop-GaApi {
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    # A launcher loop that outlived its task would restart the host behind our back.
    Get-CimInstance Win32_Process -Filter "Name = 'cmd.exe' OR Name = 'conhost.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($launcher) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    # Then stop whatever still listens on the port. Only this port: the dev GaApi on :5232
    # has the same process name and must never be touched.
    $owners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $owners) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process -and $process.ProcessName -eq 'GaApi') {
            Write-Host "  Stopping GaApi (PID $processId, $($process.Path))"
            Stop-Process -Id $processId -Force
        }
        elseif ($process) {
            throw "Port $Port is held by $($process.ProcessName) (PID $processId), not GaApi; refusing to stop it."
        }
    }
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
    }
}

function Start-GaApi {
    Start-ScheduledTask -TaskName $TaskName
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            return Invoke-RestMethod -Uri "$localBase/api/chatbot/status" -Headers $smokeHeaders -TimeoutSec 30
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "GaApi did not answer $localBase/api/chatbot/status within $StartupTimeoutSeconds s. See $logDir."
}

function Test-Page([string]$Uri, [hashtable]$Headers = @{}) {
    try {
        $response = Invoke-WebRequest -Uri $Uri -Headers $Headers -UseBasicParsing -TimeoutSec 60
        if ($response.StatusCode -ne 200) { return "${Uri}: HTTP $($response.StatusCode)" }
        return $null
    }
    catch {
        return "${Uri}: $($_.Exception.Message)"
    }
}

function Test-Release($Status, [string]$ReleaseDir) {
    $failures = @()
    if (-not $Status.isAvailable) { $failures += "status: isAvailable=false ($($Status.message))" }

    try {
        $body = @{ Message = $SmokePrompt } | ConvertTo-Json
        $chat = Invoke-RestMethod -Uri "$localBase/api/chatbot/chat" -Method Post -ContentType 'application/json' `
            -Headers $smokeHeaders -Body $body -TimeoutSec 180
        Write-Host "  Smoke: agent=$($chat.agentId) method=$($chat.routingMethod) elapsed=$($chat.elapsedMs)ms"
        if ([string]::IsNullOrWhiteSpace($chat.naturalLanguageAnswer)) { $failures += 'chat: empty answer' }
        if ($chat.agentId -ne $ExpectedAgent) { $failures += "chat: answered by '$($chat.agentId)', expected '$ExpectedAgent'" }
    }
    catch {
        $failures += "chat: $($_.Exception.Message)"
    }

    # The public chatbot page ships with GaApi from the commit that adds
    # wwwroot/chatbot/index.html. When this build has it, check the page and the anonymous
    # status call it makes, exactly as a browser on /chatbot/ would (no X-Api-Key).
    if (Test-Path -LiteralPath (Join-Path $ReleaseDir 'wwwroot\chatbot\index.html')) {
        try {
            $page = Invoke-WebRequest -Uri "$localBase/chatbot/" -UseBasicParsing -TimeoutSec 60
            if ($page.StatusCode -ne 200) { $failures += "page /chatbot/: HTTP $($page.StatusCode)" }
            elseif ($page.Headers['Content-Type'] -notmatch 'text/html') { $failures += "page /chatbot/: Content-Type $($page.Headers['Content-Type'])" }
            else { Write-Host '  Page: /chatbot/ 200 text/html' }
        }
        catch {
            $failures += "page /chatbot/: $($_.Exception.Message)"
        }
        $anonymous = Test-Page "$localBase/chatbot/api/chatbot/status"
        if ($anonymous) { $failures += "page status call (anonymous): $anonymous" }
    }
    else {
        Write-Host '  Page: this build has no wwwroot/chatbot/index.html; skipping the /chatbot/ check.'
    }

    if ($PublicProbe) {
        foreach ($path in '/chatbot/', '/api/chatbot/status') {
            $failure = Test-Page "https://$PublicHost$path"
            if ($failure) { $failures += "public $failure" }
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
    # Untracked files count too: SDK projects compile any stray *.cs under the project
    # directory, so the binary would no longer match the ref. Only .deploy is ours.
    $dirty = git -C $DeployRoot status --porcelain -- . ':(exclude).deploy'
    if ($dirty) { throw "Deploy worktree has local changes or untracked files; it must match the ref exactly:`n$dirty" }
    Invoke-Git -C $DeployRoot checkout --detach --quiet $sha
}
Write-Host "  Commit: $(git -C $DeployRoot log -1 --format='%h %s')"

# 2. Publish to a release folder. The running release is untouched, so no file locks.
$release = Join-Path $releasesDir $shortSha
dotnet publish (Join-Path $DeployRoot 'Apps/ga-server/GaApi/GaApi.csproj') -c Release -o $release --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# 3. Launcher and scheduled task (idempotent). The launcher loops so a crash restarts
#    the host, and rotates each log past 10 MB (one old copy) before every start. The
#    task starts it at logon and again every 5 minutes in case the launcher itself
#    died; IgnoreNew makes a repetition a no-op while it runs.
#
#    Configuration outside Aspire (Aspire would inject ConnectionStrings__* and
#    services__*; none of them is needed by the chat path):
#    - Production: no appsettings.Development.json, no user secrets, no Swagger, and the
#      X-Api-Key gate on /api/chatbot/* (the public page's own calls are exempt in builds
#      that ship the page).
#    - Routing embedder and threshold as on GaChatbot.Api (its appsettings.json), so the
#      public chat routes the same way after the cutover.
#    - OPTIC-K voicing search reads the index in the main checkout, read-only. With it,
#      voicing search needs no warmup, so the startup indexer (which embeds every voicing
#      through the Ollama instance the public chatbot shares) stays off.
#    - Governance:DemerzelRoot pins governance state under .deploy/work. Unset, GaApi
#      falls back to the main checkout's governance/demerzel and writes signals there.
#    - Proxy:PublicHost makes the session cookie Secure behind the tunnel; GaApi's
#      appsettings.json does not set it. Never set it blank.
#    - No MongoDB or Redis: GaApi starts without them, Redis is skipped when unset.
New-Item -ItemType Directory -Path $logDir, $workDir, (Join-Path $workDir 'governance\demerzel') -Force | Out-Null
$opticIndex = Join-Path $mainCheckout 'state\voicings\optick.index'
$indexLine = if (Test-Path -LiteralPath $opticIndex) { "set VoicingSearch__OpticIndexPath=$opticIndex" } else { 'rem optick.index not found; voicing search is unavailable (startup indexing is off)' }
@"
@echo off
rem Generated by Scripts/deploy-gaapi.ps1. Rerun the script instead of editing this file.
set ASPNETCORE_ENVIRONMENT=Production
set ASPNETCORE_URLS=$localBase
set AI__Embedding__Routing__Model=bge-large
set AI__Routing__MinConfidence=0.64
$indexLine
set VoicingSearch__EnableIndexing=false
set Governance__DemerzelRoot=$workDir\governance\demerzel
set Proxy__PublicHost=$PublicHost
if exist "$deployDir\local-env.cmd" call "$deployDir\local-env.cmd"
cd /d "$workDir"
:run
call :rotate "$logDir\gaapi.out.log"
call :rotate "$logDir\gaapi.err.log"
call :rotate "$logDir\restarts.log"
"$currentLink\GaApi.exe" --contentRoot "$currentLink" >> "$logDir\gaapi.out.log" 2>> "$logDir\gaapi.err.log"
echo %date% %time% GaApi exited with code %errorlevel%, restarting in 10 s >> "$logDir\restarts.log"
ping -n 11 127.0.0.1 > nul
goto run

:rotate
if exist "%~1" for %%F in ("%~1") do if %%~zF GTR 10485760 move /y "%~1" "%~1.1" > nul
exit /b
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
    -Principal $taskPrincipal -Description "GaApi (production) on :$Port, deployed by Scripts/deploy-gaapi.ps1" -Force | Out-Null

# 4. Switch releases and verify; roll back on failure.
$previous = Get-CurrentRelease
Stop-GaApi
Set-CurrentRelease $release

$failures = @()
try {
    $failures = @(Test-Release (Start-GaApi) $release)
}
catch {
    $failures = @($_.Exception.Message)
}

if ($failures.Count -gt 0) {
    Write-Host "Release $shortSha failed verification:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    if ($previous -and $previous -ne $release) {
        Write-Host "Rolling back to $previous" -ForegroundColor Yellow
        Stop-GaApi
        Set-CurrentRelease $previous
        $null = Start-GaApi
    }
    else {
        Write-Host 'No previous release to roll back to; GaApi stays on the failed release.' -ForegroundColor Yellow
    }
    exit 1
}

# 5. Keep the newest releases, never the running or previous one.
Get-ChildItem -LiteralPath $releasesDir -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $KeepReleases |
    Where-Object { $_.FullName -ne $release -and $_.FullName -ne $previous } |
    Remove-Item -Recurse -Force

Write-Host "Deployed $shortSha; GaApi is serving on $localBase." -ForegroundColor Green
