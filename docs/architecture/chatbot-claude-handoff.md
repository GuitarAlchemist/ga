---
title: GA Chatbot Claude Code Handoff
scope: Prompt-ready context for Claude Code or another coding agent before chatbot work.
status: authoritative
last_verified: 2026-05-12
parent: docs/architecture/README.md
---

# GA Chatbot Claude Code Handoff

Use this before assigning chatbot work to Claude Code.

## Read Order

1. `CLAUDE.md`
2. `docs/architecture/chatbot-overview.md`
3. `docs/architecture/chat-surfaces.md`
4. `docs/plans/2026-05-07-chatbot-roadmap.md`
5. `docs/plans/2026-05-06-skills-orchestration-architecture.md`
6. `docs/chatbot/Chatbot_Technical_Roadmap.md` only for long-term product context

Treat `chat-surfaces.md` as authoritative for runtime wiring. Treat the
2026-05-07 roadmap as authoritative for shipped orchestration decisions.

## Current Shape

- Harmonic Nebula uses `POST /api/nebula/chat`.
- The public `/chatbot/` demo uses GaApi SignalR `/hubs/chatbot`.
- AG-UI surfaces use `/api/chatbot/agui/stream`.
- REST/SSE siblings exist at `/api/chatbot/chat` and `/api/chatbot/chat/stream`.
- `IChatApplicationService` is the host-neutral orchestration boundary.
- `ProductionOrchestrator` owns the main skill/router/RAG path.
- `GaChatbot.Api` compiles but is not the public deployed host.
- `GA.AI.Service` is frozen.

## Do Not Do Silently

- Do not promote `GaChatbot.Api` without updating AppHost, ingress, docs, and
  the public `/chatbot/` SPA route.
- Do not add chatbot behavior to `GA.AI.Service`.
- Do not change public chat wire fields (`Grounding`, `Trace`, routing
  metadata, failure reasons) without updating `chat-surfaces.md`.
- Do not let deterministic skills silently degrade when `ga_dsl_eval` or a
  keyhole MCP tool was expected.
- Do not treat simulated word-split streaming as true model streaming without
  documenting the limitation.

## Common Traps

- `IChatService` and `IChatClient` are different abstractions.
- `NebulaSidekickService` talks to providers directly and is not the same path
  as `ProductionOrchestrator`.
- `ChatbotHub` is not dead: the public `/chatbot/` SPA calls `/hubs/chatbot`.
- `POST /api/chatbot/ask` was removed from `TriageDropZone`; do not reintroduce
  it without adding a tested server route.
- `ChatbotSessionOrchestrator.GetResponseAsync` and `StreamResponseAsync` are
  cleanup candidates, not the canonical request path.

## Verification Commands

```powershell
rg -n "/hubs/chatbot|HubConnectionBuilder|signalR" Apps/ga-server/GaApi/wwwroot/chatbot
rg -n "MapHub<ChatbotHub>|/hubs/chatbot" Apps/ga-server/GaApi/Program.cs
rg -n "api/chatbot/ask|chatbot/ask" Apps ReactComponents
rg -n "GaChatbot.Api|GA.AI.Service|ai-service" AllProjects.AppHost Apps Scripts docker-compose.yml
```

## Test Commands

```powershell
dotnet build AllProjects.slnx -c Debug
dotnet test AllProjects.slnx
pwsh Scripts/test-chatbot-quality.ps1
```

For narrower changes, prefer targeted tests first, then run the full build/test
pair before claiming success.
