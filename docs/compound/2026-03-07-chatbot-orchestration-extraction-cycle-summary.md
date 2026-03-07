---
date: 2026-03-07
branch: feat/chatbot-orchestration-extraction
commits:
  - 76842b00
  - 0c67d26c
status: completed
review_report: docs/solutions/reviews/ce-review-feat-chatbot-orchestration-extraction-2026-03-07.md
compound_doc: docs/solutions/compound-reviews/2026-03-07-ce-review-security-arch-hygiene.md
---

# Cycle Summary — Chatbot Orchestration Extraction (2026-03-07)

## What Changed

### Commit `76842b00` — fix(security): address /ce:review findings — security, layer violations, dead code

**Security hardening:**
- `GaEvalController` (`Apps/ga-server/GaApi/Controllers/GaEvalController.cs`): `POST /api/ga/eval` now returns HTTP 403 in any non-Development environment. Previously any deployed instance exposed unauthenticated FSI code execution (RCE).
- `GaDslTool` (`GaMcpServer/Tools/GaDslTool.cs`): Added `IsPermittedForMcp` prefix-check in `GaInvokeClosure` blocking `io.*` and `agent.*` closures from unauthenticated MCP callers. Side-effect categories are now unreachable from the MCP surface.
- `AgentClosures.fs` (`Common/GA.Business.DSL/Closures/BuiltinClosures/AgentClosures.fs`): SSL bypass conditionalized — `DangerousAcceptAnyServerCertificateValidator` now only activates when `DOTNET_ENVIRONMENT = "Development"`. Was unconditional before.

**Layer boundary enforcement:**
- `ChatModels.cs` (`Common/GA.Business.Core.Orchestration/Models/ChatModels.cs`): Introduced `VoicingExplanationDto` — a mirror DTO that decouples `CandidateVoicing` from `VoicingExplanation` in `GA.Business.ML`. Orchestration contracts no longer carry an ML layer type.
- `SpectralRagOrchestrator.cs`: Added `ToDto(VoicingExplanation)` mapping method at the Orchestration/ML boundary.

**Dead code removal:**
- `GaSurfaceSyntaxParser.fs`: Removed `WorkflowDecl` DU case, its parser combinator, and its desugar match arm. Never parsed or tested (S-1).
- `GaBlockDetector.fs`: Removed `toBlockRelativeLine` helper with zero callers (S-2).
- `LanguageServer.fs`: Removed `getDiagnostics` hollow stub with zero callers (S-3).

**Engineering hygiene:**
- `OllamaGroundedNarrator.cs` and `QueryUnderstandingService.cs`: `Console.WriteLine` replaced with `ILogger<T>` structured warnings (A-7).
- Double closure registration removed from all 5 `BuiltinClosures/*.fs` files: module-level `do register ()` bindings deleted; `GaClosureBootstrap.init()` in `Library.fs` is now the sole registration driver (A-8).
- `ChatbotSessionOrchestrator.cs`: `ArgumentNullException.ThrowIfNull(request)` replaced with a null-safe early return — brings the method into compliance with the project ROP policy (A-9).

---

### Commit `0c67d26c` — refactor(chatbot): address review findings — rename, rate limit, prompt sanitization, docs subagent

**Interface rename (A-5):**
- `IOllamaChatService` renamed to `IChatService` across 7 files: `IChatService.cs`, `OllamaChatService.cs`, `ClaudeChatService.cs`, `OllamaChatClientAdapter.cs`, `ChatbotController.cs`, `AIServiceExtensions.cs`, `ChatbotSessionOrchestrator.cs`. Git detected rename at 93% similarity.

**Rate limiting (M-2):**
- `Program.cs` (`Apps/ga-server/GaApi/Program.cs`): Fixed-window rate limiter wired — 60 requests/minute per IP, queue limit 5, rejections return HTTP 429. Middleware activated in the pipeline after `UseCors`.

**Prompt sanitization (M-1):**
- `GroundedPromptBuilder.cs` (`Common/GA.Business.Core.Orchestration/Services/GroundedPromptBuilder.cs`): Added `SanitizeQuery` (NFKD normalization + injection-pattern regex + 500-char cap) and `SanitizeField` (same, no length cap). All user input and DB-sourced candidate fields now pass through sanitization before being appended to the LLM prompt. Injection pattern covers `SYSTEM:`, `USER:`, `ASSISTANT:`, `\nHuman:`, `\nAssistant:`, `###`, triple-backtick.

**Compound flywheel infrastructure:**
- Added `codebase-documenter` agent spec at `.agent/agents/codebase-documenter.md`. The compound pipeline is extended from 6 to 7 steps: `Work → Reflect → Compound → Promote → Encode → Govern → Document`.

---

## Why

A multi-agent `/ce:review` report (`docs/solutions/reviews/ce-review-feat-chatbot-orchestration-extraction-2026-03-07.md`) produced a 27-finding risk matrix. The drivers were:

- **C-1, C-2 (CRITICAL)**: Unauthenticated RCE via eval endpoint and unrestricted MCP closure access presented immediate merge blockers.
- **A-2, A-5 (HIGH)**: Layer boundary violation (`VoicingExplanation` ML type in Orchestration contract) and wrong abstraction name on the chat interface were architecture debt with daily friction cost.
- **A-3 (HIGH)**: Unconditional SSL bypass in a module-static `HttpClient` was a correctness problem for all non-dev deployments.
- **M-1, M-2 (MEDIUM)**: Prompt injection and lack of rate limiting are both standard hardening requirements before any public exposure.
- **S-1, S-2, S-3 (MEDIUM)**: Dead code in the F# DSL layer was scaffolded for features that never arrived — safe to remove with no callers.
- **A-7, A-8, A-9 (LOW/MEDIUM)**: Engineering hygiene — structured logging, idempotent registration, ROP compliance — improve observability and maintainability.

The compound doc (`docs/solutions/compound-reviews/2026-03-07-ce-review-security-arch-hygiene.md`) drove the fix scope and records the rationale for each resolution.

---

## What Was Deferred and Why

| ID | Item | Reason |
|---|---|---|
| A-1 full | Full migration of `AgentClosures.fs` from `GA.Business.DSL` (Layer 2) to `GA.Business.Core.Orchestration` (Layer 5) with `IHttpClientFactory` | Breaking change — requires DI wiring across F#/C# boundary; partial fix (SSL guard) was sufficient to unblock merge |
| S-4 | Merge `MetaDecl`/`PolicyDecl` into `DirectiveDecl` | Low urgency; parsers stable with no active consumers of the distinction |
| S-5 | Consolidate `NoteNames`/`RootNames` duplicate arrays | Low risk as-is; duplication is cosmetic |
| S-6 | Remove `DocumentStore.GetAll()` dead API | Requires caller audit to confirm no external consumers |
| S-7 | Thread `_requestId` through function signature instead of JObject side-channel | Refactor scope; not security-critical |
| A-4 | Replace per-invocation `HttpClient` in `IoClosures.fs`/`TabClosures.fs` with `SocketsHttpHandler` | Performance improvement only; not blocking |
| L-1 | Anthropic API key exposure via SDK debug/trace logging | Needs SDK-level investigation to confirm scope before fix |
| L-2 | Silent exception swallowing in Ollama availability check | Observability improvement; no user-visible impact currently |

---

## Links

- **Review report**: `docs/solutions/reviews/ce-review-feat-chatbot-orchestration-extraction-2026-03-07.md`
- **Compound doc**: `docs/solutions/compound-reviews/2026-03-07-ce-review-security-arch-hygiene.md`
- **System snapshot**: `docs/snapshots/2026-03-07-system-snapshot.md`
- **Service inventory**: `docs/architecture/service-inventory.md`
