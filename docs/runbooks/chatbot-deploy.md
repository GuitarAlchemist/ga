---
title: Chatbot deploy runbook — demos.guitaralchemist.com
status: living
date: 2026-05-16
related:
  - docs/architecture/apps-and-processes.md
  - docs/architecture/chat-surfaces.md
  - Scripts/ga-service-wrapper.ps1
  - Scripts/install-ga-service.ps1
  - Scripts/start-chatbot-api.ps1
---

# Chatbot deploy runbook

How to redeploy `https://demos.guitaralchemist.com/chatbot/` past the current main. There is **no CI/CD workflow** for this deploy — every push to main requires the operator to pull + build + restart on the demos host.

## Topology

| Surface | Host | Port | Notes |
|---|---|---|---|
| Public chat HTML | `GaChatbot.Api` wwwroot | 5252 | `Apps/GaChatbot.Api/wwwroot/index.html` |
| Public chat REST | `POST /api/chatbot/chat` on GaChatbot.Api | 5252 | requires `Chatbot__PathBase=/chatbot` env var |
| AG-UI streaming + hubs | `GaApi` | 5232 | unchanged by chatbot redeploy |
| Cloudflare ingress | cloudflared `ga-demos` tunnel | n/a | routes `/chatbot/*` + `/api/chatbot/*` → :5252; root → :5232 |
| Frontend (rest of site) | Vite | 5176 | unchanged by chatbot redeploy |

The Windows service `GuitarAlchemist` (installed via `Scripts/install-ga-service.ps1`) manages GaApi + cloudflared + Vite + Ollama but **NOT** GaChatbot.Api — that needs to be launched separately. See memory `reference_dev_stack_three_services` for the long version.

## Redeploy procedure

Run on the demos host. Assumes repo at `C:\Users\spare\source\repos\ga`.

```powershell
# 1. Stop the currently-running chatbot API. The service-managed surfaces
#    (GaApi, cloudflared, Vite, Ollama) can keep running — they don't
#    depend on GaChatbot.Api's binary version.
$chatbotPid = (Get-Process -Name 'GaChatbot.Api' -ErrorAction SilentlyContinue).Id
if ($chatbotPid) { Stop-Process -Id $chatbotPid -Force }

# 2. Pull main + verify head.
Set-Location C:\Users\spare\source\repos\ga
git fetch origin
git checkout main
git pull --ff-only origin main
git log -1 --format='%h %s'   # confirm expected head sha

# 3. Build the chatbot API (Release).
dotnet build Apps/GaChatbot.Api/GaChatbot.Api.csproj -c Release --nologo
# Build MUST exit 0. If a DLL lock error appears, step 1 missed a process.

# 4. Set required env vars + launch.
$env:Chatbot__PathBase = '/chatbot'
$env:AI__CascadeProvider = 'mistral'      # optional but recommended; needs MISTRAL_API_KEY
$env:ASPNETCORE_URLS = 'http://localhost:5252'
Start-Process -FilePath dotnet `
  -ArgumentList 'run --project Apps/GaChatbot.Api/GaChatbot.Api.csproj -c Release --no-build' `
  -WindowStyle Hidden `
  -RedirectStandardOutput "$PWD\logs\gachatbot-api.log" `
  -RedirectStandardError "$PWD\logs\gachatbot-api-err.log"

# 5. Smoke-check locally before declaring done.
Start-Sleep -Seconds 5
$health = Invoke-WebRequest -Uri http://localhost:5252/api/chatbot/health -UseBasicParsing
if ($health.StatusCode -ne 200) { throw "Local health check failed: $($health.StatusCode)" }

# 6. Probe the public surface end-to-end.
Invoke-RestMethod `
  -Uri https://demos.guitaralchemist.com/api/chatbot/chat `
  -Method POST `
  -ContentType 'application/json' `
  -Body (@{ prompt = 'What is the difference between major and minor?' } | ConvertTo-Json) |
  Select-Object -ExpandProperty trace |
  Select-Object -ExpandProperty steps |
  Select-Object name, status, @{n='agentId';e={$_.attributes.'agent.id'}} |
  Format-Table -AutoSize
```

The probe should show the 6-step canonical shape:
`chat.request → orchestration.answer → orchestration.route → agent.semantic_result → notation.vextab → response.emit`,
with `agent.id = skill.theorycomparison` on the orchestration steps once #221 ships and the cascade is wired. **No `orchestration.fallback` step** means cascade isn't being triggered — that's the healthy path.

## Rollback

If step 5 health check or step 6 probe fails:

```powershell
# Kill the new process
Stop-Process -Name 'GaChatbot.Api' -Force

# Reset to the prior commit (verify $oldSha first)
git checkout $oldSha
dotnet build Apps/GaChatbot.Api/GaChatbot.Api.csproj -c Release --nologo

# Relaunch with the same env vars from step 4
```

Cloudflare ingress is unchanged by a rollback — only the local process binary changes. The Cloudflare tunnel keeps routing /chatbot/\* to :5252 regardless of which build runs there.

## Common failures

| Symptom | Most likely cause | Fix |
|---|---|---|
| `502` on `https://demos.guitaralchemist.com/chatbot/` while root returns 200 | GaChatbot.Api not running on :5252 | step 4 (relaunch) |
| Build fails with `MSB3027 The file is locked by GaChatbot.Api (<pid>)` | step 1 missed a process | `Stop-Process -Id <pid> -Force` then retry step 3 |
| Probe returns answer but `agentId = skill.modes` or fallback | new build not actually running (cached process) OR cascade not configured | check `gachatbot-api.log` head for "Now listening on: 5252" and verify env vars |
| `/api/chatbot/chat` returns 404 | `Chatbot__PathBase` env var missing | step 4 — must be set BEFORE Start-Process |
| Ollama timeout cascades produce `orchestration.fallback` step | Ollama is down OR no cascade configured | verify `ollama:11434` reachable AND `AI__CascadeProvider=mistral` set with valid `MISTRAL_API_KEY` |

## Why no CI/CD?

The demos host is a single workstation (Windows, not Linux). A GitHub Actions workflow would need either (a) a self-hosted Windows runner on this box, or (b) an SSH-based push pipeline. Both are bigger lifts than the operator-touch this runbook captures. Re-evaluate when the demo moves to a cloud VM.

## Related

- `docs/architecture/apps-and-processes.md` — full topology of every running process
- `docs/architecture/chat-surfaces.md` — section "Canonical surfaces matrix (post-2026-05-13)" — which endpoint serves which surface
- `Scripts/ga-service-wrapper.ps1` — what the `GuitarAlchemist` Windows service actually starts (and does NOT start)
- memory `reference_dev_stack_three_services` — the missing-third-service gap that this runbook plugs
