# GaChatbot.Api Host Context

> Fresh-session orientation for `Apps/GaChatbot.Api/`. Read this BEFORE touching anything in this subsystem.

## What this subsystem is

The chatbot API host. Standalone ASP.NET process on **port 5252**, separate from `GaApi` (port 5232). Wires the orchestration stack (`AddChatbotOrchestration` from layer 5) plus its own controllers (`ChatbotController`, `AgUiChatController`, `A2AController`). Public-facing via Cloudflare Tunnel at `demos.guitaralchemist.com/chatbot/*` and `/api/chatbot/*` — `Chatbot:PathBase=/chatbot` strips the prefix server-side so localhost direct access continues to work. Chat provider defaults to **Ollama** (`llama3.2:3b` + `nomic-embed-text`); the same host can run "direct", "routed", or "full" (orchestrated) modes via `Chatbot:Mode`.

## Key invariants (DO NOT VIOLATE)

- **`Chatbot__PathBase=/chatbot` env var (or appsettings key `Chatbot:PathBase`) is required in production.** Without it, the Cloudflare ingress strips `/chatbot` but ASP.NET never reattaches it; `fetch('api/chatbot/chat')` from the SPA resolves to `/api/chatbot/chat` at the host root and bypasses ingress. See lines 44-78 of `Program.cs`.
- **The trailing-slash 308 redirect runs BEFORE `UsePathBase`.** It MUST stay in that order. A user landing on `/chatbot` (no slash) resolves `index.html` relative URLs against the parent dir → tunnel 404 cascade. Class of bug from PR #111 review.
- **GaChatbot.Api is NOT started by `Scripts/start-dev.ps1`.** That script launches GaApi (5232) + Vite (5176) only. Memory rule `reference_dev_stack_three_services` documents this gap — 502 on `/chatbot/` while root stays 200 = this host isn't running.
- **Never restart Ollama on `demos.guitaralchemist.com` remotely.** Shared infra; memory rule `feedback_shared_infra_auth`. If `winget upgrade Ollama.Ollama` is needed (e.g. 0.20.0 segfault from 2026-05-05), ask the user first and hand them a `!` one-liner.
- **`pwsh` from this Bash spawns ConstrainedLanguage.** `Start-Job` silently fails → GaChatbot.Api never starts. Run the slot exe directly with env vars instead. Memory: `feedback_pwsh_constrained_language`.
- **CORS allow-list is config-driven.** `Cors:AllowedOrigins` covers the dev Vite ports (5173/5174/5176/5177). Adding a new dev origin = `appsettings.json` edit, not code.
- **`Chatbot:Mode = "full"` enables orchestration.** "direct" routes straight to a chat client; "routed" uses `LightweightChatRouter` only; "full"/"orchestrated" wires the full `OrchestratedChatApplicationService` + `SemanticIntentRouter` + skills. Don't ship "direct" to prod by accident.
- **`AppContext.BaseDirectory` is the content root.** Set explicitly on line 6 of `Program.cs` because the host runs with a non-source working dir under the slot deploy.
- **`GA_STATE_DIR` is forced to `<base>/state`.** Memory store, transcript log, and voicing index all live under there. Don't move state outside this directory without coordinating with the slot-deploy scripts.
- **`VoicingSearchWarmupService` is hosted only when orchestrated.** First-request latency in "full" mode depends on this — don't disable it as a "perf optimization."

## The 5-10 files that matter

- `Program.cs` — host composition, `UsePathBase`, trailing-slash redirect, CORS. 100 lines, all load-bearing.
- `appsettings.json` — Ollama URLs, `Chatbot:Mode`, `Chatbot:PathBase`, vector-store path, CORS allow-list. Read second.
- `Extensions/ServiceCollectionExtensions.cs` — `AddMinimalChatbotApi` wires mode → application service, Ollama HTTP client, orchestration stack. The branching on `chatbotMode` is the entire host's behavior surface.
- `Controllers/ChatbotController.cs` — the canonical `/api/chatbot/chat` endpoint the SPA hits.
- `Controllers/AgUiChatController.cs` — AG-UI streaming surface (`/api/chatbot/agui/stream`).
- `Controllers/A2AController.cs` — agent-to-agent endpoint (used by Demerzel ACP bridge).
- `Services/OrchestratedChatApplicationService.cs` — full-mode chat pipeline (routing → skill → narrate → emit).
- `Services/DirectChatApplicationService.cs` — bare-bones LLM fallback when orchestration is off.
- `Services/VoicingSearchWarmupService.cs` — IHostedService that pre-builds the voicing index on boot.
- `wwwroot/` — minimal SPA shim for `/chatbot/` browser access; the real UI is served by `ga-react-components` at `/chatbot` route.

## How to add a new chatbot endpoint or service

1. **Controllers** — add `[ApiController] [Route("api/chatbot/<verb>")]` in `Controllers/`. Inject the application-service interface, not concrete services. The `Chatbot:PathBase` strips `/chatbot` from incoming requests, so route attributes use `api/chatbot/...` (the Cloudflare-ingress-stripped form).
2. **App service** — extend `IChatApplicationService` only if all three modes (direct/routed/orchestrated) need it. Otherwise add a service on the orchestrated path inside the `if (usesOrchestration)` block in `ServiceCollectionExtensions.cs`.
3. **New config key** — add it to `appsettings.json`, read via `configuration["Section:Key"]` in `AddMinimalChatbotApi`. Do not introduce options classes for one-off values.
4. **CORS for a new dev origin** — append to `Cors:AllowedOrigins` in `appsettings.json`. Don't add `AllowAnyOrigin()` for "convenience."
5. **Hosted service** — `services.AddHostedService<YourService>()` inside the `usesOrchestration` block if it depends on the orchestration stack; otherwise unconditionally. Match `VoicingSearchWarmupService`'s pattern.

## What NOT to do here

- Don't remove the trailing-slash redirect or reorder it relative to `UsePathBase`. Both are load-bearing under tunnel ingress (PR #111).
- Don't hardcode `localhost:11434` in code. Read `Ollama:BaseUrl` / `Ollama:Endpoint` — the `winget` upgrade path can move the URL.
- Don't add a Voxtral TTS call here. TTS is server-side via Mistral and is gated on `MISTRAL_API_KEY` in the Vite proxy (see `vite.config.ts` `/proxy/voxtral`). The chatbot host does not own TTS auth.
- Don't merge `GaChatbot.Api.Services.IChatApplicationService` with the orchestration-layer interface of the same name. Codex C-prime explicitly recommended keeping the host-richer one (Trace, readiness, `ChatExecutionResult`). Lines 36-45 of `ServiceCollectionExtensions.cs` document this.
- Don't put state outside `GA_STATE_DIR`. Slot deploys snapshot only that directory; everything else evaporates.
- Don't restart `cloudflared` without checking for `STOP_PENDING`. Recovery doc: `reference_cloudflared_recovery` in memory — force-kill PID, then `Start-Service`.
- Don't edit `Tests/Apps/GaChatbot.Api.Tests/Corpus/prompts.yaml` directly (standing rule). Run the chatbot-improvement-loop runbook.
- Don't add a new chatbot deploy mode without updating `ChatProviderReadinessProbe` to surface it on `/api/health`.

## Where to look for related context

- Parent: [/CLAUDE.md](../../CLAUDE.md) — build/test commands, OPTIC-K invariants, Karpathy rules.
- Sibling: [`../../Common/GA.Business.ML/Agents/CONTEXT.md`](../../Common/GA.Business.ML/Agents/CONTEXT.md) — the skills this host exposes.
- Sibling: [`../../Common/GA.Business.Core.Orchestration/Plugins/CONTEXT.md`](../../Common/GA.Business.Core.Orchestration/Plugins/CONTEXT.md) — the orchestration stack `AddChatbotOrchestration` wires up.
- Runbook (don't edit, but reference): `docs/runbooks/chatbot-improvement-loop.md`.
- Solutions: `docs/solutions/architecture/2026-05-07-process-wide-memory-store-leaks-into-anonymous-prompts.md`, `docs/solutions/architecture/2026-05-07-slot-build-stale-static-web-assets-manifest.md`, `docs/solutions/integration-issues/2026-03-10-ollama-client-extraction-hot-alloc-fix.md`.
- Memory references: `reference_dev_stack_three_services` (PathBase), `reference_ollama_runner_crash_2026_05_05`, `reference_cloudflared_recovery`, `reference_mistral_voxtral`.
