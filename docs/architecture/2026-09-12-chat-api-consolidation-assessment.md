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
## Integration follow-up (2026-09-13)

The local MongoDB container became healthy before this follow-up; no database restart or data migration was performed. The existing chord fixture then passed all 12 tests against a database containing 25,900 chords. The shared HTTP test factory now sets `VoicingSearch:EnableIndexing=false`, preventing each host from generating the corpus and requesting its embeddings. Production indexing configuration is unchanged.

Inspection of successful-test output exposed a separate HTTP 500 masked by permissive assertions. GaDataCLI exports `Intervals` as documents containing `Semitones`, `Function`, and `IsEssential`, while GaApi's public model uses numeric intervals. A property-scoped BSON reader now accepts the exported documents and existing numeric arrays while preserving the API and serialized numeric representation. Missing or malformed semitones fail explicitly. Quality, stacking-type, and search contract assertions now require HTTP 200; real queries returned ten Major chords and ten search results after the fix. Deterministic BSON tests reproduced the failure before the fix and passed afterward.

These checks establish local database-read compatibility, not full deployed host parity. The existing by-ID test still accepts a missing placeholder ID; it does not establish successful retrieval of a seeded chord by ID. Ingress migration and retirement remain subject to the earlier ADR-0005 evidence requirements.

Final verification: full solution build passed with 0 errors and 73 warnings. Full solution tests passed with 3,496 passed, 25 skipped, and 0 failed across 10 projects in 2 minutes 15 seconds, using isolated "bin/CodexIntegrationValidation/" outputs. GaApi contributed 174 passing tests. Scoped whitespace formatting and diff checks passed.

## REST wire-contract follow-up

Current GaApi source already emits grounding and trace in both REST JSON and the first REST SSE routing frame; the older surface inventory is not evidence of missing metadata. Deterministic HTTP tests now assert routing identity/confidence, grounding source/revision/query type, trace identity/step, history forwarding, and the SSE sequence: routing, answer, then `[DONE]`.

GaApi REST SSE now emits `X-Accel-Buffering: no`, matching its AG-UI sibling and GaChatbot.Api REST SSE. This asks compatible proxies not to buffer the stream; it does not prove how deployed ingress behaves. Buffered REST dispatch and routing-before-text remain intact for existing `ga-client` consumers. These tests establish the canonical host's contract, not complete cross-host behavioral parity or production readiness.

Verification for this follow-up: 4 REST SSE regression cases failed on the missing header before the change; all 8 REST history/metadata cases pass afterward. Full solution build passed (0 errors, 66 warnings); full solution tests passed (3496 passed, 25 skipped, 0 failed across 10 projects). Scoped formatting and diff checks passed.

## SignalR hub history follow-up (2026-09-13, Claude)

`ChatbotHub.SendMessage` stored normalized per-connection turns but constructed `ChatIntakeRequest` without `History`. `ProductionOrchestrator.AnswerAsync` (the non-streaming path the hub uses) passes only `req.History` to LLM and deterministic agents, so hub follow-ups reached agents with no conversation history; only routing enrichment could see prior turns through the session-scoped `ConversationHistoryStore`. The hub now forwards a snapshot of its stored turns, in order, and forwards `null` when none exist so first-message requests are unchanged. `ClearHistory` and rejected intakes (busy) continue to leave no forwarded turns.

Forwarding the hub's own store, rather than making `AnswerAsync` fall back to `ConversationHistoryStore`, preserves `ClearHistory` semantics: the orchestrator store is not cleared by that hub method and would otherwise resurrect cleared turns.

Verification: three deterministic hub tests (`Tests/Apps/GaApi.Tests/Hubs/ChatbotHubHistoryTests.cs`); the ordering test failed before the change and all three pass afterward. Chat-scoped GaApi tests pass (62). Full solution build passed with 0 errors. The full GaApi project run had 9 failures, all 30-second MongoDB/GraphQL integration timeouts with Docker stopped and port 27017 closed; they are environmental and unrelated to this change, and no services were started or restarted.

Remaining, not addressed here: REST requests that rely on the session cookie without client-supplied history still give agents no history on the non-streaming path; `ClearHistory` does not clear the orchestrator's session store used for routing enrichment; fallback remains message-only. Live hub behavior is unverified.

## AG-UI current-turn history follow-up (2026-09-13, Claude)

Both GaApi AG-UI actions (`agui/json` and `agui/stream`) forwarded every message, including the current user message, as `ChatIntakeRequest.History`. `ProductionOrchestrator` treats `History` as prior context: agents received the current question twice, and `RoutingContextEnricher` could select the current message as the "prior" user turn. GaChatbot.Api already excludes the current user message. An earlier HTTP test had pinned the duplicating behavior as the contract.

GaApi now treats the last user message as the current turn and forwards only the other nonblank messages, in order. A request carrying only the current message forwards an empty list, not `null`, so the orchestrator's session-store fallback is not newly enabled by this change.

Verification: the previous stream-only history test was replaced by four HTTP cases covering both routes (prior turns without the current message; a single message yields empty history). All four failed before the change and pass afterward. Chat-scoped GaApi tests pass (65). Full solution build passed with 0 errors. Scoped whitespace formatting and diff checks passed.

Open product decision: `ga-client` (`agUiChatService.ts`) sends only the current message, so its agents still receive no conversation history. Either the client sends the full AG-UI thread, or GaApi forwards `null` when no prior turns exist so the cookie-scoped `ConversationHistoryStore` supplies context. The second option can carry turns from an earlier conversation in the same browser into a new thread (the cookie lasts 30 days). Neither is implemented here.

## ga-client AG-UI thread follow-up (2026-09-13, Claude)

The open product decision above was resolved on the client, with user approval: `sendMessageAtom` now captures the stored chat messages before appending the new one, and `streamAgUiChat` posts the AG-UI thread (prior user/assistant turns, then the current message). System and blank messages are omitted, and only the most recent 12 turns are sent because chat messages persist in localStorage. GaApi's server-side contract is unchanged; the cookie-scoped session store is still not used as an AG-UI history fallback.

Verification: four new vitest cases (`Apps/ga-client/src/test/agUiChatService.test.ts`), including a store-level test that inspects the posted body; they failed before the change and pass afterward. The full ga-client vitest suite passes (68 passed, 1 skipped), `npm run build` passes, and ESLint passes on the changed files. `tsc -b` reports existing errors elsewhere in ga-client and ga-react-components but none in the changed files. Live browser behavior against a running GaApi was not exercised.

## History-aware fallback follow-up (2026-09-13, Claude)

`IFallbackChatHandler.AnswerAsync` previously accepted only the current message, so an enabled fallback answered follow-ups without context, whereas GaChatbot.Api's direct-chat fallback receives the request history. The interface now takes the caller's prior turns (`IReadOnlyList<ConversationTurn>?`, oldest first, excluding the current message). `FallbackChatApplicationService` forwards `ChatRequest.History`, and `GaApiFallbackChatHandler` maps the turns in order to the selected `IChatService` provider, all of which already accept conversation history. `NoOpFallbackChatHandler` ignores them. The interface and all implementations are in this repository; no wire contract changed. `Chatbot:Fallback:Enabled` remains false by default.

This establishes history forwarding for the fallback path only. Fallback trigger parity is still not established: GaChatbot.Api also falls back on empty answers, ungrounded "no matches" answers, orchestration exceptions, and a 60-second orchestration timeout, and it returns a user-facing message when the fallback times out. GaApi's decorator fires only on low confidence outside deterministic routes and returns the original response on fallback timeout.

Verification: two new tests (`FallbackChatApplicationServiceTests.ChatAsync_FallbackEnabled_LowConfidenceInner_ForwardsRequestHistory` and `ChatProviderAdapterTests.Fallback_ForwardsHistoryToProviderInOrder`) failed with `null` history before the change and pass afterward. Existing fallback mocks were updated to the new signature. Orchestration-scoped core tests pass (80), chat-scoped GaApi tests pass (66), and the full solution build passed with 0 errors. With Docker running again, the full solution suite passed: 3,504 passed, 23 skipped, 0 failed across 9 test projects (GaApi 181 passed).

## Declined intents and agent history follow-up (2026-09-13, Claude)

A live AG-UI run against GaApi with Ollama 0.34.0 fully on the GPU showed that conversational follow-ups never reached the LLM. The semantic intent router sent "What is my name…", "Which key did I say…", and "Describe his playing style" to `skill.rememberthis`, `skill.keyidentification`, and `skill.capo`. Each skill found none of its input and returned help text at confidence 0 to 0.3, identical with and without history. Enabling fallback would not help: `FallbackChatApplicationService` deliberately never fires on `orchestrator-skill-semantic` routes.

A second defect sat behind the first. On the non-streaming path (`AnswerAsync`, used by REST and `agui/json`), `ProductionOrchestrator` passes `ChatRequest.History` to the selected agent, but none of the five specialized agents (Theory, Tab, Technique, Composer, Critic) passed `AgentRequest.ConversationHistory` to the LLM. Only the streaming path sent prior turns.

Changes:

- `AgentResponse.Declined` and `IntentResult.Declined` mark a request that lacks the input shape a skill handles. `OrchestratorSkillIntent` forwards the flag. When an intent declines, both `AnswerAsync` and `AnswerStreamingAsync` record a `routing.declined` trace step and continue to the deterministic-agent and LLM agent paths instead of returning the skill's help text.
- Skills set the flag only where no recognizable input was found: the `CannotHandle` helpers of 11 pattern skills (including Capo, RelativeKey, AlternateTunings, OutsideNotes, the ICV and Grothendieck skills, SetTheoryEquivalence, and TheoryComparison), RememberThis without remember phrasing, and KeyIdentification and ProgressionCompletion when no chord symbols were extracted. Recognized but unresolvable requests (for example, capo fret 25) keep their deterministic errors, so the P1 #5 contract is unchanged for real skill failures.
- The five agents now pass `request.ConversationHistory` to `ChatAsync`. In `ChatWithCritiqueAsync` only the draft call receives history, because the critique and refinement prompts quote the draft.

Verification:

- Unit tests: `SkillDeclineTests` (4 cases), `OrchestratorSkillIntentDeclineTests` (2 cases), and `AgentConversationHistoryTests` (5 agents).
- `DeclinedIntentFallThroughTests` in GaChatbot.Api.Tests (4 cases) runs the production-wired orchestrator with a fake embedder, a fake chat client, a single fixed intent, and Ollama pointed at an unreachable port. It asserts that a declined intent falls through to an agent whose LLM call carries the prior turn, on both paths. Its non-streaming case failed until the agent fix landed.
- The full solution build passed with 0 errors. The full suite passed: 3,519 passed, 23 skipped, 0 failed across 9 test projects.
- Live, against an isolated GaApi on port 5299 using the new build and Ollama `llama3.2:3b` on the GPU (29/29 layers offloaded):
  - With history, the follow-ups answered "Your information is Zorblax and your favorite guitarist is Django Reinhardt", described Django Reinhardt's style, and said "You mentioned composing in the key of G flat major".
  - The same questions without history answered that no prior information or key was known.
  - In-scope prompts still route to their skills: "What shape do I play in E with capo 4?" and "What key is Am F C G in?". "What shape do I play in E with capo 25?" still returns the skill's invalid-fret error.

Not addressed:

- The LLM-routed answers come from a 3B model, and agent selection among Theory, Technique, Composer, and Critic is unchanged.
- `Tests/Apps/GaChatbot.Tests` is not in `AllProjects.slnx` and does not compile (`ExtensionsAINarrator.cs` CS0176), so the new orchestrator test lives in GaChatbot.Api.Tests.
- A prompt such as "I play a C shape with capo 25" is taken by the explicit-voicing fast path before semantic routing, and it returned HTTP 500 on the test instance because voicing indexing was disabled there.
