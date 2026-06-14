# Non-Admin Service Install Runbook

**Status:** Scaffold ready for install. Not yet executed.
**Scope:** Harness item #8 from `docs/plans/2026-05-23-arch-harness-engineering-adoption-plan.md`.
**Date:** 2026-05-23
**Owner:** Stephane (must run, requires admin).

## Why this matters

This session (2026-05-23) we lost ~25 minutes of demos.guitaralchemist.com uptime because two related bugs collided:

1. **Port displacement** — Codex started a stray Vite from `Apps/ga-client` which stole port 5176 from the canonical `ga-react-components` instance (fixed in PR #289).
2. **Elevated-PID lock** — GaApi PID 21036 was running with an elevated token. No agent in the user's PowerShell context could `Stop-Process` it to unblock an AppHost rebuild. The user had to manually kill it from an admin shell.

Running Vite, GaApi, and GaChatbot.Api as Windows services owned by a **non-admin service account** breaks this class of bug:

- The agent's user-mode PowerShell can run `Restart-Service GA-Api-5232` (allowed by Service Control Manager ACLs once configured) without needing to spawn an elevated child process or pass UAC.
- The bin-lock problem disappears because the service owner is the only process holder; restarting the service releases the lock cleanly.
- Each of the three production processes (Vite/GaApi/Chatbot) is independent — restarting one doesn't take the other two down. Today, killing the AppHost takes everything with it.

The plan tagged this as **L effort / H impact** — high impact because it eliminates a recurring outage class, but L effort because it requires admin-level OS changes, a service account decision, and irreversible registration.

## Architectural options considered

### Option A (REJECTED): Wrap Aspire AppHost in a single service

This is what the previous version of `install-ga-service.ps1` did. It registers one service that runs `dotnet run --project AllProjects.AppHost`. Looks simple, but:

- AppHost itself is a dev-orchestration layer. It spawns GaApi/GaChatbot/MongoDB as child processes and routes traffic through the Aspire dashboard. Running it as a service in prod is off-label.
- One service = one lifecycle. Restarting "the platform" restarts everything, including the in-process Aspire dashboard. The whole point of this exercise is **independent lifecycles**.
- The child processes inherit the AppHost's token. If AppHost runs as NetworkService, that's fine; but if it ever needs admin (which it will, the moment someone adds a debugger or a port < 1024), the children become un-killable again — exactly the bug we're trying to solve.

### Option B (REJECTED): `New-Service` with a dedicated AD account

Native Windows `New-Service`/`sc.exe create` with a domain account. Robust for shops with Active Directory but:

- Requires AD setup (not present on this machine).
- Requires creating + managing service account credentials (rotation, vaulting).
- `sc.exe` doesn't supervise — if the wrapped process exits, the service is "stopped" with no auto-restart. You end up writing a wrapper that loops `Start-Process` and `Wait-Process`, which is what NSSM already does correctly.

### Option C (CHOSEN): NSSM + built-in virtual account

Three independent services, each wrapping one process, all running under `NT AUTHORITY\NetworkService` by default:

| Service | Working dir | Command | Port |
|---|---|---|---|
| `GA-Vite-5176` | `ReactComponents/ga-react-components` | `pnpm dev --host --port 5176` | 5176 |
| `GA-Api-5232` | `GaApi` | `dotnet run --project GaApi.csproj --no-build --urls http://0.0.0.0:5232` | 5232 |
| `GA-Chatbot-5252` | `GaChatbot.Api` | `dotnet run --project GaChatbot.Api.csproj --no-build --urls http://0.0.0.0:5252` | 5252 |

Rationale:

- **NSSM** supervises (auto-restart on crash), rotates logs, captures stdout/stderr to disk, accepts service-account credentials.
- **NetworkService** is a built-in low-privilege account: no password to rotate, can bind to TCP ports > 1024, has network egress, can't elevate. Zero AD setup.
- **Three services** = three independent lifecycles. An agent can `Restart-Service GA-Api-5232` without touching Vite or Chatbot.
- **Service Control Manager ACLs** can be tuned per-service to grant the developer user `Start/Stop/Restart` rights without granting admin elsewhere (`sc sdset GA-Api-5232 ...`). Out of scope for this scaffold; default ACL already lets the local Administrators group restart.
- **No AppHost in prod path.** AppHost stays a dev tool. Production runs the same processes Aspire would have launched, just without the orchestrator wrapper.

Cloudflared is **not** wrapped — it ships its own native Windows service installer (`cloudflared service install`) which is well-tested and runs as LocalSystem. The runbook installs both.

## Prerequisites

1. **NSSM installed.** `winget install nssm` (or download from <https://nssm.cc/>).
2. **Admin PowerShell.** Service registration requires it; the post-install lifecycle does not.
3. **`AllProjects.sln` built once.** `--no-build` is in the args so the service starts fast; this assumes a prior `dotnet build`.
4. **pnpm install run once** in `ReactComponents/ga-react-components/` so `node_modules` exists.
5. **Cloudflared installed** if you want the public tunnel managed too: `winget install cloudflare.cloudflared`.

## Service account permissions

The service account (default `NT AUTHORITY\NetworkService`) needs:

| Permission | Why | How |
|---|---|---|
| Read access to repo path (`C:\Users\spare\source\repos\ga`) | Read csproj, source, node_modules, dlls | `icacls "C:\Users\spare\source\repos\ga" /grant "NT AUTHORITY\NetworkService:(OI)(CI)RX" /T` |
| Write access to `logs\services\` | NSSM stdout/stderr capture | Created by the install script; inherits from parent or grant explicitly |
| Bind ports 5176/5232/5252 | Listen for HTTP | NetworkService can bind > 1024 by default; no action needed |
| Network egress | LLM API calls, NuGet, npm | NetworkService has this by default |
| Read access to `~/.aspire/`, `~/.nuget/`, `%LOCALAPPDATA%\pnpm-store` | Tooling caches | NetworkService can't read the developer's profile — see "Profile caches" below |

### Profile caches (gotcha)

`dotnet` and `pnpm` cache packages in the *invoking user's* profile by default. NetworkService has its own profile at `C:\Windows\ServiceProfiles\NetworkService\`. The first start under NetworkService will repopulate these caches there, which is fine but adds ~30-60s to the first start. Two ways to avoid:

- **(A)** Pre-stage by running once as NetworkService via `psexec -s` before starting the service (advanced).
- **(B)** Switch to a dedicated local user (`.\ga-service`) whose profile is `C:\Users\ga-service` and pre-warm the caches as that user. Required if you want reproducible cold-start times.

For initial install, accept the slow first start.

## Step-by-step install (user runs this)

```powershell
# 1. Pull latest main, build once.
cd C:\Users\spare\source\repos\ga
git pull
dotnet build AllProjects.slnx -c Debug
pnpm --filter ga-react-components install

# 2. Dry-run the installer. CHANGES NOTHING.
.\Scripts\install-ga-service.ps1 -Install -WhatIf
# Inspect the output. You should see three "What if: Performing the operation..."
# lines per service (nssm install, set DisplayName, etc.) plus the log-dir creation.

# 3. Open an ADMIN PowerShell. Re-run for real.
Start-Process powershell.exe -Verb RunAs
# (in the new admin window:)
cd C:\Users\spare\source\repos\ga
.\Scripts\install-ga-service.ps1 -Install
# Three services should report "Installed ... as NT AUTHORITY\NetworkService".

# 4. Grant repo read access (one-time).
icacls "C:\Users\spare\source\repos\ga" /grant "NT AUTHORITY\NetworkService:(OI)(CI)RX" /T

# 5. Install cloudflared as a service (skip if you don't expose publicly).
cloudflared service install
# (cloudflared reads its tunnel config from ~/.cloudflared/config.yml)

# 6. Start everything (this can be done from a NON-admin shell once installed).
.\Scripts\install-ga-service.ps1 -Start

# 7. Verify.
.\Scripts\install-ga-service.ps1 -Status
# Expect: three "Running" services, three live port-listeners with NetworkService PIDs.
```

## Verification

After step 7, do all of these:

1. **Status check:** `Scripts\install-ga-service.ps1 -Status` shows three `Running` services with PIDs and ports occupied.
2. **Port owner is NetworkService:**
   ```powershell
   $portPid = (netstat -ano | sls ':5232\s' | select -First 1).ToString().Split()[-1]
   Get-Process -Id $portPid | Select Name, Id, UserName
   ```
   `UserName` should be `NT AUTHORITY\NETWORK SERVICE`, not your developer account.
3. **Non-admin can restart:** Open a *regular* (non-admin) PowerShell and run:
   ```powershell
   Restart-Service GA-Api-5232
   ```
   Should succeed without UAC prompt. If it fails with "access denied," your developer account isn't in the service ACL — fix with:
   ```powershell
   # From admin shell, one-time:
   sc.exe sdset GA-Api-5232 "D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)(A;;RPWPCR;;;<your-user-SID>)"
   ```
4. **Smoke test the three URLs:**
   ```powershell
   curl http://localhost:5176/         # Vite, expect HTML
   curl http://localhost:5232/health   # GaApi
   curl http://localhost:5252/health   # Chatbot
   ```
5. **Public tunnel smoke (if cloudflared installed):**
   ```powershell
   curl https://demos.guitaralchemist.com/api/health
   ```
6. **Log files appear:** `dir C:\Users\spare\source\repos\ga\logs\services\` should show `vite.log`, `gaapi.log`, `chatbot.log` with recent timestamps.
7. **Reboot test (optional but recommended):** restart the machine, log in, check that all three services come up automatically (Start type = Automatic per NSSM config).

## Rollback / uninstall

```powershell
# Dry-run first.
.\Scripts\uninstall-ga-service.ps1 -WhatIf

# Real uninstall (admin shell).
.\Scripts\uninstall-ga-service.ps1
# Pass -IncludeCloudflared if you also want the tunnel removed.

# Revert repo ACL grant (optional, harmless to leave).
icacls "C:\Users\spare\source\repos\ga" /remove "NT AUTHORITY\NetworkService" /T

# Clean log dir (optional).
Remove-Item C:\Users\spare\source\repos\ga\logs\services -Recurse -Force
```

Post-uninstall, go back to running the platform via `Scripts\start-all.ps1` or the Aspire dashboard for development.

## What this scaffold does NOT do

These are deliberately deferred until after the first install proves the model works:

- **Per-service ACL setup** (`sc sdset`) for non-admin restart. Documented in Verification step 3.
- **Cloudflared tunnel config templating.** The user's existing `~/.cloudflared/config.yml` is reused as-is.
- **Health-check HTTP probes** wired into NSSM `AppEvents` for auto-restart on /health 5xx (NSSM can't do this natively; would need a sidecar).
- **Cache pre-warming** as NetworkService (see "Profile caches" gotcha above).
- **Aspire dashboard equivalent.** No equivalent service — use the three `*.log` files in `logs/services/` plus Windows Event Viewer (NSSM logs there).
- **MongoDB.** Today it runs in a Docker container managed by Aspire. Running it as a Windows service would require either (a) MongoDB Community installed natively as a service or (b) Docker Desktop running as a service. Out of scope; the developer keeps Aspire/Docker for the data layer.

## Surfaced to harness item #8

This scaffold lands the install machinery and runbook. The plan-doc dashboard tracks item #8 as **ready-for-install** (not ✅ shipped) until the user has:

1. Run `-Install` for real.
2. Verified all seven verification steps pass.
3. Confirmed the next time an agent needs to restart GaApi, no UAC prompt fires and no manual admin kill is required.

The "shipped" trigger is observational: the next session where a process-lock or elevated-PID incident *should* have happened, but didn't, because the service model handled it.
