---
title: Chatbot — entry point
status: index
last_updated: 2026-05-02
---

# Chatbot — entry point

Single landing page for everything chatbot-related across the Guitar Alchemist
solution. Earlier sessions left ~3 roadmap docs scattered across `docs/`,
`docs/plans/`, and `Common/GA.Business.ML/Documentation/Architecture/`. This
index is the consolidation.

## What is the chatbot?

The user-facing chatbot is the **Harmonic Nebula React UI** backed by the
**`POST /api/nebula/chat`** endpoint (`NebulaChatController` →
`NebulaSidekickService` → Anthropic Claude Haiku 4.5 or local Ollama). Every
other chat surface in the solution is parallel-to-canonical or deprecated.

For the full live architecture — every endpoint, every orchestrator, every
agent, with status flags — see:

- **`docs/architecture/chat-surfaces.md`** ← authoritative, last verified
  2026-04-25. Cross-referenced from `docs/architecture/README.md`.

If `chat-surfaces.md` says one thing and any doc here says another, trust
`chat-surfaces.md`.

## Forward-looking vision (still relevant)

- **`Chatbot_Technical_Roadmap.md`** — long-term OPTIC-K + wavelet + LLM
  architecture. Aspirational; sections 1.1.x track real implementation
  status.
- **`Chatbot_Backlog.md`** — spikes/epics/stories/tasks derived from the
  roadmap, with acceptance criteria. Some items are checked off; treat the
  unchecked ones as a working backlog rather than a hard plan.

These two were previously buried under
`Common/GA.Business.ML/Documentation/Architecture/`. Moved here on
2026-05-02 so they live next to the cleanup index.

## Historical / executed (do not edit)

- `docs/plans/2026-03-02-feat-functional-chatbot-agentic-routing-plan.md` —
  Phase 4 orchestration extraction. Status: completed (frontmatter).
- `docs/plans/2026-03-08-feat-ag-ui-protocol-integration-plan.md` — AG-UI
  protocol bridge for the agentic frontend.
- `docs/archive/architecture-pre-2026-04/implementation_plan_chatbot_rewrite.md`
  — the original "kill GuitarAlchemistChatbot, build GaChatbot" plan.
  Executed; archived 2026-05-02.

## Cleanup history

- `docs/plans/2026-05-02-chatbot-cleanup-inventory.md` — surface inventory +
  6-step cleanup that produced this directory.

## What lives where (apps, after 2026-05-02 cleanup)

| Surface | Status | Notes |
|---|---|---|
| `Apps/ga-server/GaApi/Controllers/NebulaChatController.cs` (+ `NebulaSidekickService`) | ✅ canonical | The user-facing path. |
| `Apps/ga-server/GaApi/Controllers/AgUiChatController.cs` | 🟡 parallel-to-canonical | AG-UI bridge for ga-react-components. |
| `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` + `Hubs/ChatbotHub.cs` | 🟡 parallel-to-canonical | Pre-Nebula SSE + SignalR, still used by Prime Radiant ChatWidget. |
| `Apps/GaChatbot/` (console) | ✅ secondary | Local-dev REPL; not started by Aspire. |
| `Apps/GaChatbotCli/` | ✅ secondary | Production CLI; supports `--json` and `--interactive`. |
| `GaMcpServer/Tools/ChatTool.cs` | ✅ live | MCP tool (`AskChatbot`) calling `/api/chatbot/agui/json`. |
| `Apps/GuitarAlchemistChatbot/` | 🪦 deleted 2026-05-02 | Replaced by `GaChatbot` per implementation_plan_chatbot_rewrite. |
| `Experiments/ChatbotExample1/` | 🪦 deleted 2026-05-02 | Blazor sandbox using GitHub Models; never on canonical path. |
| `Apps/ga-dashboard/src/app/chat/` | 🪦 deleted 2026-05-02 | Angular shim calling a non-running microservice. |

## Out of scope here

The Demerzel **GitHub-discussions bot** at
`governance/demerzel/docs/superpowers/plans/2026-03-17-live-mcp-chatbot.md`
is a *different* chatbot — it lives in
`.github/workflows/ga-chatbot-discussions.yml` and answers GitHub
discussions threads via the Anthropic API. Same name, completely
unrelated codebase. If you're looking for it, follow that link, not this
directory.

The Rust harness at `ix/crates/ga-chatbot/` is also unrelated — it's a
deterministic QA harness + MCP bridge living in the IX workspace. See its
`lib.rs` for the disambiguation comment added 2026-05-02.
