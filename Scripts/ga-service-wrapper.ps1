# GA Service Wrapper — managed by Windows Service
# Starts and monitors all GA platform components

$RepoRoot = "C:\Users\spare\source\repos\ga"
$LogDir = "$RepoRoot\logs"
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null

# Start components
$procs = @()

# 1. GaApi
$gaApiLog = "$LogDir\gaapi.log"
$procs += Start-Process -FilePath "dotnet" -ArgumentList "run --project $RepoRoot\Apps\ga-server\GaApi\GaApi.csproj --no-build" -WindowStyle Hidden -PassThru -RedirectStandardOutput $gaApiLog -RedirectStandardError "$LogDir\gaapi-err.log"
Write-Output "Started GaApi (PID $($procs[-1].Id))"

# 2. Cloudflare tunnel
$procs += Start-Process -FilePath "cloudflared" -ArgumentList "tunnel run ga-demos" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$LogDir\cloudflared.log" -RedirectStandardError "$LogDir\cloudflared-err.log"
Write-Output "Started cloudflared tunnel (PID $($procs[-1].Id))"

# 3. Frontend dev server
$procs += Start-Process -FilePath "npm" -ArgumentList "run dev" -WorkingDirectory "$RepoRoot\ReactComponents\ga-react-components" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$LogDir\vite.log" -RedirectStandardError "$LogDir\vite-err.log"
Write-Output "Started Vite dev server (PID $($procs[-1].Id))"

# 4. Ollama (if installed)
$ollamaPath = Get-Command ollama -ErrorAction SilentlyContinue
if ($ollamaPath) {
    $procs += Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$LogDir\ollama.log" -RedirectStandardError "$LogDir\ollama-err.log"
    Write-Output "Started Ollama (PID $($procs[-1].Id))"
}

# Monitor loop — restart crashed processes
while ($true) {
    Start-Sleep -Seconds 30
    foreach ($p in $procs) {
        if ($p.HasExited) {
            Write-Output "Process $($p.Id) exited with code $($p.ExitCode), not restarting (manual intervention needed)"
        }
    }
}
