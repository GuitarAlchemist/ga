# GA.AI.Service — frozen 2026-05-07, do not extend

This project is **deprecated and not deployed**. It has been parked since at least 2026-04-25 (see [`docs/architecture/audit-2026-04-25.md`](../../../docs/architecture/audit-2026-04-25.md) and [`docs/architecture/chat-surfaces.md`](../../../docs/architecture/chat-surfaces.md)).

## Status

- **Aspire AppHost registration**: commented out at [`AllProjects.AppHost/Program.cs:87`](../../../AllProjects.AppHost/Program.cs). The service is not started by `pwsh Scripts/start-all.ps1`.
- **No live consumers**: no frontend, no other backend, no scripts call any controller in this project.
- **Compiles**: yes — kept building so the project graph stays clean while the deprecation decision was pending.
- **Wired to `ProductionOrchestrator`**: `ChatController` still resolves the orchestrator standalone. If anyone launched the service manually it would route chat the same way GaApi does (but no one does).

## What to do instead

For each capability this project nominally exposed, the canonical replacement is:

| GA.AI.Service controller | Use instead |
|---|---|
| `ChatController` (`/api/Chat`) | `GaApi.Controllers.ChatbotController` (`/api/chatbot/chat`) — host-neutral via `IChatApplicationService` (commit `947941c1`) |
| `AdaptiveAIController` | unimplemented; bring back into GaApi if needed |
| `AdvancedAIController` | unimplemented; bring back into GaApi if needed |
| `AIAnalysisController` | overlapping with GaApi's `MonadicHealthController` / `AlgedonicController` |
| `BenchmarkController` | covered by `Scripts/run-dsl-eval-soak.ps1` and `state/quality/` snapshots |
| `DocumentationController` | covered by Swagger on GaApi (`/swagger`) |
| `NotebookController` | mcp-servers/notebooklm (the live MCP server has the canonical surface) |
| `SearchController` | covered by GaApi's `SearchController` and the OPTIC-K / voicing search services |
| `TabAnalysisController` | `GaApi.Controllers.YouTubeTabController` + `TabAnalysisOrchestrationService` |

## Why not delete the project

Three reasons it stays in the tree for now:

1. **No urgent cost**: the project compiles, the AppHost doesn't start it, the only cost is build time when running `dotnet build AllProjects.slnx`.
2. **Reference value**: some patterns in here (eg. the parked semantic-search and personalization controllers) inform follow-up GaApi work; deleting them now removes context.
3. **Codex CLI 2026-05-07 review**: explicit recommendation was to *freeze* the parallel surfaces (canonical-surface decision Q1 — "make GaApi canonical, freeze the second host"). Same reasoning applies here.

When the GaChatbot.Api freeze decision flips (the secondary host is either revived for a real deploy reason or removed), GA.AI.Service should be removed in the same pass — both rely on the same "third controller layer" anti-pattern that the canonical-surface work is closing.

## Hard rules from now on

1. **Do not add new code** to `Controllers/`, `Services/`, or `Models/`. New endpoints belong in GaApi.
2. **Do not add ProjectReferences** to this project from other projects.
3. **Do not uncomment** the `AddProject("ai-service", …)` line in `AllProjects.AppHost/Program.cs:87`. Uncommenting it implies revival, which requires a fresh canonical-surface decision.
4. **PR comments touching this project** should reference this file and explain whether the change is preserving compilability or actively reviving the project.

## Decision trigger for the next state change

Either of:

- A concrete deploy reason emerges that splits AI workloads onto their own host (multi-tenant, GPU isolation, separate scaling). At that point: revive intentionally, refresh canonical-surface in [`chat-surfaces.md`](../../../docs/architecture/chat-surfaces.md), wire it back into the AppHost.
- The next chatbot-architecture review concludes the project's reference value has decayed below maintenance cost. At that point: delete the project, remove from `AllProjects.slnx`, drop the AppHost comment.

Until one of those triggers fires, this project is read-only.
