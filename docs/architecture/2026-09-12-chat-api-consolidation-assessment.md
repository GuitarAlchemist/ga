# Chat API consolidation assessment — 2026-09-12

Status: advisory implementation assessment; no endpoint removal or deployment change authorized by this document.

## Existing decision

[ADR-0005](../adr/0005-gaapi-single-canonical-chat-host.md) already selects GaApi as the canonical chat host. Resume the [deepening campaign](../plans/2026-06-21-arch-deepening-campaign-plan.md); do not create another host-selection decision or a third chat interface. GaChatbot.Api retires only after behavior parity, ingress migration, and live verification.

The older [chat surface inventory](chat-surfaces.md) contains historical topology and implementation descriptions. The accepted ADR governs host selection; current source must verify migration completion.

## Baseline gaps (2026-09-12, before implementation)

| Surface | Baseline evidence | Consequence |
| --- | --- | --- |
| GaApi REST `/api/chatbot/chat` and `/chat/stream` | `Apps/ga-server/GaApi/Controllers/ChatbotController.cs` constructs `ChatIntakeRequest` without `ConversationHistory`; the request and frontend callers supply history. GaChatbot.Api maps it to `ConversationTurn`. | Follow-up requests lose caller-supplied context. |
| GaApi AG-UI streaming | `Apps/ga-server/GaApi/Controllers/AgUiChatController.cs` calls `IHarmonicChatOrchestrator.AnswerStreamingAsync` directly. `IChatIntake` currently exposes only `IntakeAsync`. | Streaming bypasses the common application decorators and does not carry the same history path as AG-UI JSON. |
| Readiness/fallback | `Common/GA.Business.Core.Orchestration/Extensions/ChatbotOrchestrationExtensions.cs` registers `PermissiveChatReadinessProbe` and `NoOpFallbackChatHandler` by default. | A decorator chain exists, but its presence is not proof of provider-readiness and fallback parity. Verify host overrides before declaring completion. |
| Clients | `Apps/ga-client/src/services/chatApi.ts`, `chatService.ts`, and `ReactComponents/ga-react-components/src/components/PrimeRadiant/ChatWidget.tsx` still use legacy REST/SSE routes. | Removing routes would break existing callers unless migrated or adapted. |
| Deployment | GaChatbot.Api source and CONTEXT.md describe public tunnel routing to port 5252. No live ingress configuration was verified. | Source-level retirement intent is not evidence that traffic has moved. |

MCP's chatbot bridge consumes AG-UI HTTP; it is not an interchangeable HTTP host. GraphQL has a separate query surface and is not shown to duplicate chat. This assessment does not establish a need to merge all protocols or cross-repository contracts.

## Proposed sequence

1. Smallest vertical slice: add a deterministic request-to-intake contract test for REST history and forward validated conversation turns through both GaApi REST actions. Preserve existing routes and wire shapes. Verify empty/history-free requests and ordering of prior turns.
2. Separate slice: implement streaming intake and migrate AG-UI streaming through it. Define cancellation, busy/error framing, history, and completion tests before changing dispatch.
3. Verify actual readiness/fallback registrations and bring GaApi to the behavior required by ADR-0005, with deterministic provider-failure and recovery tests.
4. Verify live ingress and migrate traffic only with operational approval and a rollback route. Retire GaChatbot.Api last. Migrate legacy clients before removing supported route shapes.

## Hypothesis review

- Assumption: duplicated hosts are migration residue, while their deployed behavior still differs. Supported by ADR-0005 and current controller paths.
- Strongest counterargument: separate public-host deployment may provide useful isolation. Revisit ADR-0005 only with current operational requirements, not interface-name similarity.
- Hidden costs: session identity, streaming event ordering, cancellation, error/status mapping, readiness, fallback, trace/grounding, and public tunnel routing can change independently of DTO names.
- Simpler alternative: retain both hosts temporarily and add parity tests around observed user behavior. No new shared package or generic facade is required for the first history fix.
- Falsifier: request-level tests prove caller history already reaches the orchestrator through another path, or deployed callers intentionally require divergent behavior.
- Rejection criterion: reject a proposed consolidation slice if it removes an active consumer contract or cannot preserve session/history/error behavior without unapproved public API changes.
- Evidence required before deletion: passing transport parity tests, verified production ingress ownership, live demo smoke tests, and a documented rollback route.

An independent read-only review confirmed the existing decision and the three implementation gaps. It suggested combining streaming and history; this assessment separates them to keep the first change small and independently verifiable. JetBrains semantic search was unavailable because authentication was required; findings were verified by direct source reads. No live deployment claims were verified.

## Implementation update (2026-09-13)

REST history forwarding is committed in `2521d5af`. AG-UI streaming now uses `IChatIntake.IntakeStreamingAsync`, preserving history and traversing readiness, fallback, and tracing under one concurrency-gate owner. Busy rejection remains a first-event `RUN_ERROR`; cancellation and writer failures release the gate. Responses returned without tokens are emitted once. Fallback is suppressed after nonempty text has been sent, avoiding replacement of an already visible answer.

GaApi supplies provider-backed readiness and fallback adapters. `Chatbot:Readiness:ProviderCheck` is opt-in (false by default): a global provider check would otherwise block deterministic skills that require no model. When enabled, availability checking is bounded to 750 ms and caller cancellation propagates. `Chatbot:Fallback:Enabled` remains disabled by default. The fallback interface accepts only the current message; this does not establish history-aware fallback parity with GaChatbot.Api.

The HTTP chat contract fixture now replaces intake and provider availability with deterministic dependencies. Its previous SSE test could hang while invoking the actual Ollama pipeline. These tests validate transport behavior, not live provider operation.

Four reproduced integration failures are in `MonadicChordsControllerTests`: `GetByExtension_WithValidExtension_ShouldReturnChords`, `GetById_WithValidId_ShouldReturnChord`, `GetStatistics_ShouldReturnStatistics`, and `GetTotalCount_ShouldReturnCount_WhenDatabaseIsAvailable`. The captured database error reports a 30-second server-selection timeout against `localhost:27017`, with a disconnected MongoDB cluster and an end-of-stream during handshake. Database integration is not validated; do not broaden assertions to accept server errors as successful behavior.

Live ingress, provider recovery, and full deployed host parity remain unverified. Public routing and GaChatbot.Api retirement remain subsequent operational steps under ADR-0005.