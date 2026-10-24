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

# Ollama is NOT started here. The Ollama app in the user's Startup folder owns it.
# This wrapper runs elevated at boot; an elevated `ollama serve` locks ollama.exe,
# so the app's unelevated auto-updater cannot replace it. On 2026-09-27 the update
# aborted and its rollback deleted the model runtime (lib\ollama), which broke
# every embed and generate call. See docs/runbooks/chatbot-deploy.md.

# Monitor loop — restart crashed processes
while ($true) {
    Start-Sleep -Seconds 30
    foreach ($p in $procs) {
        if ($p.HasExited) {
            Write-Output "Process $($p.Id) exited with code $($p.ExitCode), not restarting (manual intervention needed)"
        }
    }
}
