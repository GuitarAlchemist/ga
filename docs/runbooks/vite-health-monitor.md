# Vite health monitor

Restarts the local Vite dev server when it crashes. Closes a recurring
failure mode: `demos.guitaralchemist.com` returns 502 because the
cloudflared tunnel can't reach Vite on port 5176.

## What it does

`Scripts/vite-health-monitor.ps1` runs forever in a loop:

1. Polls `http://localhost:5176` every 30s with a 3s timeout
2. On 2 consecutive failures, runs `npm run dev` in
   `ReactComponents/ga-react-components`
3. Emits a `severity=warn` algedonic signal so the restart shows up on
   `/test#dev/summary`
4. Sleeps 15s for Vite warmup, then resumes polling

Logs land at `state/health/vite-monitor.log`.

## Install (no admin needed)

```powershell
pwsh -NoProfile -File Scripts\install-vite-monitor.ps1
```

Registers a user-context scheduled task `GA-Vite-Health-Monitor` that
starts at logon. Verify with:

```powershell
schtasks /Query /TN GA-Vite-Health-Monitor
```

Run it now (without waiting for logon):

```powershell
schtasks /Run /TN GA-Vite-Health-Monitor
```

## Uninstall

```powershell
pwsh -NoProfile -File Scripts\install-vite-monitor.ps1 -Uninstall
```

## Relationship to the NSSM service install (Tier 2)

This is the **interim** safety net. The proper fix is the NSSM service
install in `Scripts/install-ga-service.ps1` which runs Vite + GaApi +
GaChatbot.Api as Windows services with auto-restart-on-failure. That
needs admin UAC, which is the only reason this watchdog exists.

Once NSSM is installed, uninstall this monitor — duplicate restart
attempts can fight over the port.

## Algedonic visibility

Every restart emits a signal like:

```json
{
  "schema": "algedonic-signal-v0.1.0",
  "repo": "ga",
  "source": "vite-health-monitor",
  "severity": "warn",
  "summary": "[vite-monitor] Vite restarted on :5176"
}
```

If you see ≥3 of these within a 5-minute window, that's a real symptom
— Vite is crashing rapidly, not just dying once. Investigate the npm
log instead of trusting the watchdog to mask the issue.
