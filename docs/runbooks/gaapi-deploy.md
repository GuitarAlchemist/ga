---
title: GaApi production deploy runbook (:5262)
status: living
date: 2026-11-18
related:
  - docs/adr/0005-gaapi-single-canonical-chat-host.md
  - Scripts/deploy-gaapi.ps1
  - Scripts/ga-service-wrapper.ps1
---

# GaApi production deploy runbook

How to run GaApi as a production host on `localhost:5262`, built from a clean git ref. Since 2026-11-18 it serves the public chat routes (`/chatbot/*`, `/api/chatbot/*`, `/api/a2a/*`) of `https://demos.guitaralchemist.com`, which moved there from GaChatbot.Api (:5252); that host was then retired ([ADR-0005](../adr/0005-gaapi-single-canonical-chat-host.md)). There is no CI/CD workflow for this deploy: the demos host is a single Windows workstation, so every release is an operator running the script below.

## Topology

| Process | Port | Started by | Built from |
|---|---|---|---|
| GaApi, dev stack | 5232 | task `GA-Dev-Stack-Logon` → `Scripts/start-all.ps1` (Aspire, Development) | the main checkout, whatever branch it is on |
| **GaApi, production** | **5262** | task `GA-GaApi-5262` → `.deploy\run-gaapi.cmd` | `ga-deploy-gaapi` worktree, a pinned commit |

cloudflared routes the three chat paths to :5262. GaChatbot.Api (:5252, task `GA-Chatbot-5252`) was retired on 2026-11-18; the `ga-deploy-chatbot` worktree still holds its last release.

Ollama runs from the Ollama app in the user's Startup folder, unelevated. Do not start `ollama serve` from an elevated process: it locks `ollama.exe`, the app's auto-updater then cannot replace it, and on 2026-09-27 the aborted update's rollback deleted `lib\ollama` (the model runtime), so every embed and generate call failed while `/api/tags` still answered.

## Deploy

Run on the demos host from any checkout. The main checkout is not touched.

```powershell
pwsh -NoProfile -File Scripts/deploy-gaapi.ps1            # deploys origin/main
pwsh -NoProfile -File Scripts/deploy-gaapi.ps1 -Ref <sha> # any other commit
pwsh -NoProfile -File Scripts/deploy-gaapi.ps1 -PublicProbe  # after the flip: also probe demos.guitaralchemist.com
```

The steps: clean detached worktree `..\ga-deploy-gaapi` (refuses to run if it has changes or untracked files outside `.deploy`), `dotnet publish -c Release` of `Apps/ga-server/GaApi` into `.deploy\releases\<sha>`, launcher plus scheduled task, junction switch, verification, automatic rollback to the previous release when verification fails, and the three newest releases kept.

The scheduled task `GA-GaApi-5262` runs `conhost.exe --headless cmd.exe /c <DeployRoot>\.deploy\run-gaapi.cmd` as the logged-on user (Interactive, Limited), at logon and every 5 minutes, with `MultipleInstances=IgnoreNew` and no execution time limit. The launcher restarts GaApi 10 s after it exits and rotates `.deploy\logs\gaapi.{out,err}.log` and `restarts.log` past 10 MB.

### Verification

1. `GET /api/chatbot/status` reports `isAvailable` (GaApi's status only checks the chat provider; it has no embedding round-trip probe yet).
2. `POST /api/chatbot/chat` with `which arpeggio fits Am F C G` is answered by `skill.improvisation` with a non-empty answer.
3. Only when the build contains `wwwroot/chatbot/index.html` (the public page, PR #881): `GET /chatbot/` returns 200 `text/html`, and the page's own status call `GET /chatbot/api/chatbot/status` succeeds **without** an API key.
4. With `-PublicProbe`: the same GET probes as 1 and 3, through `https://demos.guitaralchemist.com`.

The status probe and the chat send `X-Api-Key: deploy-gaapi-smoke`. Outside Development, `Program.cs` answers 401 on `/api/chatbot/*` when that header is absent (it checks presence only), and only builds that ship the page exempt the page's own calls. With the key, the status probe passes on either kind of build. The probes come from `Get-GaApiProbes` in the script; `Tests/Apps/GaApi.Tests/Deploy/DeployGaApiProbeTests.cs` runs that function and replays its probes against GaApi in Production, so a probe the gate would reject (and the rollback it would cause) fails CI instead.

## Configuration

GaApi normally runs under Aspire, which injects `ConnectionStrings__guitar-alchemist`, `ConnectionStrings__redis` and `services__music-theory-service__*`. The production launcher sets none of them: the chat path needs no MongoDB or Redis (Redis is only registered when its connection string exists, and the Mongo client connects lazily), and the music-theory YARP route is not part of the public surface.

| Setting | Value | Why |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | No `appsettings.Development.json` (which selects Claude as chat provider), no user secrets, no Swagger; enables the `X-Api-Key` gate |
| `ASPNETCORE_URLS` | `http://localhost:5262` | loopback only; cloudflared is the public edge |
| `--contentRoot` | `.deploy\current` | `appsettings.json` and `wwwroot` come from the release |
| working directory | `.deploy\work` | GaApi writes `cache\`, `musical-knowledge.db` and similar relative to it; under the worktree root they would be untracked files that fail the next deploy's clean check |
| `AI__ChatProvider` | `ollama` | what the retired GaChatbot.Api used; explicit because `appsettings.Development.json` selects `claude`. The model comes from `local-env.cmd` below |
| `AI__CascadeProvider` | `mistral` | as on the retired GaChatbot.Api: the Ollama client is wrapped in the Mistral cascade, which answers when Ollama times out or is unreachable. `MISTRAL_API_KEY` comes from the user environment |
| `AI__Embedding__Routing__Model` | `bge-large` | the production routing embedder, also in `Apps/ga-server/GaApi/appsettings.Production.json`, which `RoutingEvalHarness` and the corpus host read |
| `AI__Routing__MinConfidence` | `0.64` | the threshold calibrated for bge-large; it only makes sense together with the line above (nomic's default is 0.55) |
| `VoicingSearch__OpticIndexPath` | `<main checkout>\state\voicings\optick.index` | read-only; the gitignored OPTK index only exists in the main checkout. GaApi reads this key, not `GA_OPTICK_INDEX_PATH` |
| `VoicingSearch__EnableIndexing` | `false` | the startup indexer embeds every voicing through Ollama, which the public chatbot shares. OPTK search needs no warmup, so it is not needed |
| `Governance__DemerzelRoot` | `.deploy\work\governance\demerzel` | unset, GaApi falls back to the main checkout's `governance\demerzel` and writes algedonic signals and beliefs there |
| `Proxy__PublicHost` | `demos.guitaralchemist.com` | GaApi's `appsettings.json` does not set it; without it, tunnel requests take the forwarded-header strip branch and the session cookie loses `Secure`. Never set it blank |

Secrets are never written by the script. The chat path needs none with the default Ollama provider. `ANTHROPIC_API_KEY`, `MISTRAL_API_KEY` and the other user-level variables reach GaApi through the scheduled task's environment. The `Authentication:*` values (JWT signing key, Google OAuth) live in the dev user secrets, which Production does not load, so `/api/auth/*` sign-in is not configured on :5262.

### Operator settings: `.deploy\local-env.cmd`

Optional `set` lines the launcher calls before the first start, never generated or overwritten by the script, never secrets. On the demos host it holds:

```bat
set Ollama__ChatModel=gpt-oss:120b-cloud
set Chatbot__Model=gpt-oss:120b-cloud
set GA_ROUTER_JEV_SHADOW=1
set Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics=Information
```

Without it GaApi uses `llama3.2:3b` from `appsettings.json`. The routing shadows live in the shared `SemanticIntentRouter`, so they work in GaApi too: `GA_ROUTER_JEV_SHADOW` (see [router-jev-shadow.md](router-jev-shadow.md)) spends `TYPESAFE_API_KEY` budget on each routed prompt, and `GA_ROUTER_SHADOW` / `GA_LEARNED_HEAD_PATH` are user-level variables the task inherits.

After editing it, run `Stop-ScheduledTask GA-GaApi-5262` then `Start-ScheduledTask GA-GaApi-5262`, or redeploy.

## Cutover: point the public chat routes at :5262 (done 2026-11-18)

Kept as a record of the flip. Prerequisites: the deployed build contains the public page (PR #881) and, for `/api/a2a/*`, the A2A endpoint (PR #882); `pwsh -NoProfile -File Scripts/deploy-gaapi.ps1` passes, including the page check.

GaChatbot.Api answered `POST /api/chatbot/chat` (the JSON endpoint) anonymously; GaApi keeps that call anonymous too (`AnonymousChatbotApi`), so external callers of the JSON endpoint kept working after the flip.

The edit, in `C:\Users\spare\.cloudflared\config.yml`: change the `service` of the three chat rules from `5252` to `5262`, leaving their order and every other rule unchanged.

```yaml
  - hostname: demos.guitaralchemist.com
    path: ^/chatbot(/.*)?$
    service: http://localhost:5262
  - hostname: demos.guitaralchemist.com
    path: ^/api/chatbot/.*$
    service: http://localhost:5262
  - hostname: demos.guitaralchemist.com
    path: ^/api/a2a/.*$
    service: http://localhost:5262
```

Then validate and restart the tunnel, and probe through it:

```powershell
cloudflared tunnel ingress validate --config C:\Users\spare\.cloudflared\config.yml
cloudflared tunnel ingress rule https://demos.guitaralchemist.com/chatbot/ --config C:\Users\spare\.cloudflared\config.yml
Restart-Service cloudflared   # elevated; see reference_cloudflared_recovery if it sticks in STOP_PENDING
pwsh -NoProfile -File Scripts/deploy-gaapi.ps1 -PublicProbe
```

### Rollback of the flip

GaChatbot.Api was retired after the flip had been verified, so there is no :5252 to fall back to. To roll back a bad GaApi release, redeploy the last known-good commit (`Scripts/deploy-gaapi.ps1 -Ref <sha>`). To restore the old host, see [chatbot-deploy.md](chatbot-deploy.md).

## Common failures

| Symptom | Most likely cause | Fix |
|---|---|---|
| `GaApi did not answer .../status within 180 s` | startup crash | read `.deploy\logs\gaapi.err.log` and `restarts.log` |
| 401 on `/api/chatbot/*` | no `X-Api-Key` header; expected outside Development | send one, or use the page's exempt calls |
| `/chatbot/` 404 | the build predates the public page (PR #881) | deploy a commit that contains it |
| `Port 5262 is held by <process>` | another program on :5262 | the script only stops GaApi on its own port; stop the other program yourself |
| `Deploy worktree has local changes or untracked files` | someone edited or added files in `ga-deploy-gaapi` | clean them (`git -C ..\ga-deploy-gaapi status`); only `.deploy` may differ |
| smoke answered by another agent | routing changed in the deployed commit, or the router's embedder is unreachable | read the `SemanticIntentRouter: query=...` line in `gaapi.out.log` for the scores, and check that Ollama lists `nomic-embed-text` |
| first smoke chat takes over a minute | the router embeds its intent examples on first use | expected once per start; the script allows 180 s |
