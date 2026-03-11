---
status: complete
priority: p3
issue_id: "023"
tags: [performance, concurrency, architecture, code-review]
dependencies: []
---

# 023 — Concurrency Gate Missing on ChatbotController (REST Path)

## Problem Statement
`ChatbotHub` limits concurrent LLM calls to 3 via a `SemaphoreSlim(3, 3)` field. `ChatbotController` (REST/SSE path) has no equivalent gate. REST callers bypass the limit entirely, can saturate Ollama, and cause SignalR clients to receive "service busy" rejections while REST callers proceed unthrottled.

Secondary issue: `AllProjects.slnx` mis-categorizes `GA.Business.Core.Orchestration` under the `/4 - AI & ML/` solution folder; it belongs in `/5 - Orchestration/`.

## Findings
- `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs:21` — `private static readonly SemaphoreSlim _concurrencyGate = new(3, 3);`
- `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` — no semaphore or rate-limiting middleware present.
- `AllProjects.slnx:59–61` — `GA.Business.Core.Orchestration` listed under AI & ML folder.

## Proposed Solutions
1. Extract an `ILlmConcurrencyGate` abstraction (thin wrapper around `SemaphoreSlim`) and register it as a singleton in DI.
2. Inject `ILlmConcurrencyGate` into both `ChatbotHub` and `ChatbotController`; remove the static field from the hub.
3. Apply `WaitAsync` / `Release` in `ChatbotController` using the same pattern currently used in `ChatbotHub`.
4. Fix `AllProjects.slnx` by moving the `GA.Business.Core.Orchestration` entry from the AI & ML folder to the Orchestration folder (one-line change).

## Recommended Action

## Technical Details
- **Affected files:**
  - `Apps/ga-server/GaApi/Hubs/ChatbotHub.cs` (line 21 — static semaphore)
  - `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` (missing gate)
  - `Apps/ga-server/GaApi/Program.cs` (DI registration for new interface)
  - `AllProjects.slnx` (lines 59–61 — wrong solution folder)

## Acceptance Criteria
- [ ] A single shared `ILlmConcurrencyGate` singleton governs both SignalR and REST paths.
- [ ] REST callers receive the same "service busy" response as SignalR callers when the gate is exhausted.
- [ ] Static `SemaphoreSlim` field removed from `ChatbotHub`.
- [ ] `GA.Business.Core.Orchestration` appears under the Orchestration solution folder in `AllProjects.slnx`.
- [ ] Build and all tests pass after the change.

## Work Log
- 2026-03-07 — Identified in /ce:review of PR #2

## Resources
- PR: https://github.com/GuitarAlchemist/ga/pull/2
