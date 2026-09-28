---
title: Chatbot deploy runbook — demos.guitaralchemist.com
status: living
date: 2026-05-16
related:
  - docs/architecture/apps-and-processes.md
  - docs/architecture/chat-surfaces.md
  - Scripts/deploy-chatbot.ps1
  - Scripts/ga-service-wrapper.ps1
  - Scripts/install-ga-service.ps1
  - Scripts/start-chatbot-api.ps1
---

# Chatbot deploy runbook

How to redeploy `https://demos.guitaralchemist.com/chatbot/` past the current main. There is **no CI/CD workflow** for this deploy — every push to main requires the operator to run `Scripts/deploy-chatbot.ps1` on the demos host.

## Topology

| Surface | Host | Port | Notes |
|---|---|---|---|
| Public chat HTML | `GaChatbot.Api` wwwroot | 5252 | `Apps/GaChatbot.Api/wwwroot/index.html` |
| Public chat REST | `POST /api/chatbot/chat` on GaChatbot.Api | 5252 | requires `Chatbot__PathBase=/chatbot` env var |
| AG-UI streaming + hubs | `GaApi` | 5232 | unchanged by chatbot redeploy |
| Cloudflare ingress | cloudflared `ga-demos` tunnel | n/a | routes `/chatbot/*` + `/api/chatbot/*` → :5252; root → :5232 |
| Frontend (rest of site) | Vite | 5176 | unchanged by chatbot redeploy |

The boot task `GuitarAlchemist` (installed via `Scripts/install-ga-service.ps1`, runs `Scripts/ga-service-wrapper.ps1` elevated) starts GaApi + cloudflared + Vite. It does **not** start GaChatbot.Api or Ollama:

- **GaChatbot.Api** runs from the scheduled task `GA-Chatbot-5252`, which `Scripts/deploy-chatbot.ps1` creates and updates. Its launcher restarts the host when it exits; the task starts the launcher at logon and again every 5 minutes in case the launcher itself died. The older task `GA-Chatbot-5252-Codex` (a one-shot trigger that ran a Debug build from the main checkout) is superseded; it cannot be overwritten or deleted without elevation, so delete it from an elevated shell with `schtasks /delete /tn GA-Chatbot-5252-Codex /f` and never start it.
- **Ollama** runs from the Ollama app in the user's Startup folder, unelevated. Do not start `ollama serve` from an elevated process: it locks `ollama.exe`, the app's auto-updater then cannot replace it, and on 2026-09-27 the aborted update's rollback deleted `lib\ollama` (the model runtime), so every embed and generate call failed while `/api/tags` still answered.

See memory `reference_dev_stack_three_services` for the long version.

## Redeploy procedure

Run on the demos host, from any checkout of this repository. The main checkout is not touched: the script builds in its own clean worktree.

```powershell
# 1-5. Build the ref in the deploy worktree, publish a release, switch to it,
#      restart the scheduled task and verify. A failed verification switches
#      back to the previous release and exits 1.
pwsh -NoProfile -File Scripts/deploy-chatbot.ps1            # deploys origin/main
pwsh -NoProfile -File Scripts/deploy-chatbot.ps1 -Ref <sha> # any other commit
```

What the script does:

1. Fetches `origin` and checks out the ref, detached, in the deploy worktree `..\ga-deploy-chatbot` (created on first use). It refuses to run if that worktree has changes or untracked files outside `.deploy`: SDK projects compile stray `*.cs` files, so the binary would no longer match the ref.
2. Runs `dotnet publish -c Release` into `.deploy\releases\<sha>`. The running release is not touched, so there are no file locks and no downtime during the build.
3. Writes `.deploy\run-chatbot.cmd`, which sets `Chatbot__PathBase=/chatbot`, `AI__CascadeProvider=mistral`, `ASPNETCORE_URLS=http://localhost:5252` and `GA_OPTICK_INDEX_PATH` (the gitignored OPTK index in the main checkout), then runs the host in a loop so a crash restarts it. Before every start it rotates each log in `.deploy\logs` past 10 MB, keeping one old copy. It also registers the scheduled task `GA-Chatbot-5252` to run that launcher headless, with no execution time limit, at logon and every 5 minutes; `MultipleInstances=IgnoreNew` makes a repetition a no-op while the launcher runs.
4. Stops the task and whatever GaChatbot.Api listens on :5252, points the `.deploy\current` junction at the new release and starts the task. The host runs with the worktree root as its working directory, so `state/` (telemetry) and the QA summary survive a release switch.
5. Verifies: `/api/chatbot/status` must report `isAvailable` (and `embeddingRoundTripOk`, which is one real embedding, on builds that have it); a real `POST /api/chatbot/chat` with `which arpeggio fits Am F C G` must be answered by `skill.improvisation`; `https://demos.guitaralchemist.com/chatbot/` and `/api/chatbot/status` must return 200.

`Proxy__PublicHost` is not set by the launcher: it ships as `demos.guitaralchemist.com` in `Apps/GaChatbot.Api/appsettings.json`. It is the host the forwarded-header guard pins `X-Forwarded-Host` to, and it is what makes the session cookie Secure behind the TLS-terminating tunnel. Never set it to an empty or whitespace value in the launcher or in the user environment — environment variables outrank `appsettings.json`, so a blank one makes every tunnel request take the strip branch and the public cookie silently ships without Secure. If the public hostname moves, change `appsettings.json`. Step 6 asserts the cookie really kept Secure.

A 200 from `/status` alone does not prove chat works on builds without the embedding probe: before it, `/status` checked only that Ollama listed the configured models.

```powershell
# 6. After the script succeeds, probe the public surface end-to-end. Body field is Message (see
#    ChatRequest in Apps/GaChatbot.Api/Controllers/ChatbotController.cs
#    line 264) — NOT 'prompt'. Wrong field name returns 400
#    "Message cannot be empty.".
Invoke-RestMethod `
  -Uri https://demos.guitaralchemist.com/api/chatbot/chat `
  -Method POST `
  -ContentType 'application/json' `
  -Body (@{ Message = 'What is the difference between major and minor?' } | ConvertTo-Json) |
  Select-Object -ExpandProperty trace |
  Select-Object -ExpandProperty steps |
  Select-Object name, status, @{n='agentId';e={$_.attributes.'agent.id'}} |
  Format-Table -AutoSize

#    Same surface, but assert the session cookie survived the tunnel WITH
#    Secure. If Proxy:PublicHost is missing or blanked, the forwarded-header
#    guard takes the strip branch, Request.IsHttps stays false, and the cookie
#    ships without Secure — a silent failure neither the status check (step 5)
#    nor the trace shape above can see.
#
#    A Set-Cookie block can hold more than one cookie, and the cookies that are
#    NOT ga_chat_session are the likelier ones to be Secure: Cloudflare's
#    __cf_bm / cf_clearance are issued Secure whenever the zone emits them, and
#    anything the app starts setting later lands in the same block. So the
#    assertion has to pick the ga_chat_session header out of the set and test
#    THAT header. Matching /secure/ across the joined block reports success off
#    somebody else's cookie while the session cookie ships bare — the exact
#    silent pass this step exists to prevent.
function Assert-GaChatSessionSecure {
  param([string[]] $SetCookieHeaders)

  # -cmatch: cookie names are case-sensitive, mirroring the ordinal match in
  # ForwardedHeadersSessionCookieTests.SessionSetCookie.
  $session = @($SetCookieHeaders) | Where-Object { $_ -cmatch '^ga_chat_session=' } | Select-Object -First 1
  if (-not $session) {
    throw "No ga_chat_session cookie issued. Got: $(@($SetCookieHeaders) -join ' | ')"
  }
  # Whole attribute, not a substring: the protected session id is base64url and
  # could otherwise spell 'secure' by accident.
  if ($session -notmatch '(?i)(^|;)\s*secure\s*(;|$)') {
    throw "Session cookie lacks Secure — check Proxy:PublicHost in Apps/GaChatbot.Api/appsettings.json. Got: $session"
  }
}

$cookieProbe = Invoke-WebRequest `
  -Uri https://demos.guitaralchemist.com/api/chatbot/chat `
  -Method POST `
  -ContentType 'application/json' `
  -Body (@{ Message = 'cookie probe' } | ConvertTo-Json) `
  -UseBasicParsing
Assert-GaChatSessionSecure -SetCookieHeaders @($cookieProbe.Headers['Set-Cookie'])
```

`Assert-GaChatSessionSecure` is not decoration: `ShippedRunbook_CookieAssertion_TestsTheSessionCookieNotTheWholeHeaderBlock` in `Tests/Apps/GaChatbot.Api.Tests/Controllers/ForwardedHeadersSessionCookieTests.cs` lifts this exact function out of this file and runs it against a two-cookie fixture. Renaming or inlining it turns that guard red with an actionable message; changing what it asserts turns it red on behaviour.

The probe should show the 6-step canonical shape:
`chat.request → orchestration.answer → orchestration.route → agent.semantic_result → notation.vextab → response.emit`,
with `agent.id = skill.theorycomparison` on the orchestration steps once #221 ships and the cascade is wired. **No `orchestration.fallback` step** means cascade isn't being triggered — that's the healthy path.

## Rollback

The script rolls back by itself when its verification fails. To go back by hand (for example after step 6 fails), redeploy the last known-good commit — its release folder is reused if it was kept:

```powershell
pwsh -NoProfile -File Scripts/deploy-chatbot.ps1 -Ref <last-good-sha>
```

The deployed commit is the name of the folder `.deploy\current` points to (`(Get-Item ..\ga-deploy-chatbot\.deploy\current).Target`). The script keeps the three newest releases.

Cloudflare ingress is unchanged by a rollback — only the local process binary changes. The Cloudflare tunnel keeps routing /chatbot/\* to :5252 regardless of which build runs there.

## Common failures

| Symptom | Most likely cause | Fix |
|---|---|---|
| `502` on `https://demos.guitaralchemist.com/chatbot/` while root returns 200 | GaChatbot.Api not running on :5252 | `Start-ScheduledTask GA-Chatbot-5252`; if the task is missing, run the deploy script. `.deploy\logs\restarts.log` records crash loops |
| `/status` reports `embeddingRoundTripOk=false` or `Embedding: ... llama-server binary not found` | Ollama's model runtime is broken, typically after an interrupted auto-update | repair or reinstall Ollama unelevated, then check `ollama run` and an embed call; `/status` turns green within 30 s (the probe result is cached) |
| Script fails with `Port 5252 is held by <process>` | another program listens on :5252 | the script only stops GaChatbot.Api; stop the other program yourself |
| Script fails with `Deploy worktree has local changes or untracked files` | someone edited or added files in `ga-deploy-chatbot` | remove them there (`git -C ..\ga-deploy-chatbot status`); the deploy worktree must match the ref exactly |
| Step 6 returns 400 `"Message cannot be empty."` | body field name is `Message`, not `prompt` (case-sensitive) | this runbook now sends `Message`; fork callers should mirror it |
| Probe returns answer but `agentId = skill.modes` or fallback | new build not actually running (cached process) OR cascade not configured | check `.deploy\logs\chatbot.out.log` for "Now listening on: http://localhost:5252" and the variables in `.deploy\run-chatbot.cmd` |
| `/api/chatbot/chat` returns 404 | `Chatbot__PathBase` missing from the launcher | rerun the deploy script, which regenerates `.deploy\run-chatbot.cmd` |
| Step 6 throws `Session cookie lacks Secure`, or public chat answers but the session resets every turn | `Proxy:PublicHost` missing/blank in `Apps/GaChatbot.Api/appsettings.json`, or a blank `Proxy__PublicHost` in the user environment — the forwarded-header guard takes the strip branch | restore `Proxy.PublicHost` in `appsettings.json` (it ships as `demos.guitaralchemist.com`), remove a blank `Proxy__PublicHost` user variable if it is set, then redeploy and re-run the step 6 cookie assertion |
| Ollama timeout cascades produce `orchestration.fallback` step | Ollama is down OR no cascade configured | verify `ollama:11434` reachable AND `AI__CascadeProvider=mistral` set with valid `MISTRAL_API_KEY` |

## Why no CI/CD?

The demos host is a single workstation (Windows, not Linux). A GitHub Actions workflow would need either (a) a self-hosted Windows runner on this box, or (b) an SSH-based push pipeline. Both are bigger lifts than the operator-touch this runbook captures. Re-evaluate when the demo moves to a cloud VM.

## Related

- `docs/architecture/apps-and-processes.md` — full topology of every running process
- `docs/architecture/chat-surfaces.md` — section "Canonical surfaces matrix (post-2026-05-13)" — which endpoint serves which surface
- `Scripts/deploy-chatbot.ps1` — the deploy, the launcher and the scheduled task
- `Scripts/ga-service-wrapper.ps1` — what the `GuitarAlchemist` boot task actually starts (and does NOT start)
- memory `reference_dev_stack_three_services` — the missing-third-service gap that this runbook plugs
