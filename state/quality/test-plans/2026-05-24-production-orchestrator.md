---
title: ProductionOrchestrator High-Value Test Plan
target: Common/GA.Business.Core.Orchestration/Services/ProductionOrchestrator.cs
status: draft
generated_at: 2026-05-24T19:30:00Z
generator: claude-opus-4-7
business_value_confidence: 0.90
effort_tshirt: L
---

# ProductionOrchestrator High-Value Test Plan

`ProductionOrchestrator` is the **top-level chat orchestrator** —
836 LOC unifying SemanticRouter, intent dispatch, skill foreach,
tab analysis, RAG, history, and per-request `ChatHookContext`
plumbing. Every chat request that isn't algebra-fast-pathed lands here.
Annotated `@ai:business-value conf=0.90`.

## Coverage gap summary

Today's only "test" is `ProductionOrchestratorHookPlumbingTests.cs` — a
**static-regex scan** that fails when a new `new ChatHookContext { ... }`
forgets `SessionId =`. The comment block explicitly says "a runtime test
would require most of the orchestrator's DI graph; cost/benefit doesn't
justify it." Per Karpathy rule #3 we will **not** rewrite the orchestrator
to be testable — instead, target the *boundary helpers* and a single
`WebApplicationFactory<Program>` golden-trace integration test that
exercises the full graph end-to-end with stubbed LLM.

Gaps:

- **`HasTabContent`** — comment block warns about pitch-class-set
  notation (`"0146"`), `"12-bar blues"`, and hyphenated dates being false
  positives. Pinned by `TabTokenizerTests.Tokenize_BareDigitProseInputs_KnownLimitation`
  at the tokenizer level, but not at the orchestrator level.
- **`EmitRoutingTrace`** — the trace-step schema is what the React
  `TracePanel` parses; no test asserts the attribute names
  (`routing.outcome`, `routing.top{i}.id`, etc.).
- **`ExplicitVoicingKeywords` list** — drift here breaks the dispatch
  threshold for voicing intents; no test pins the list size or membership.
- **`AnswerStreamingAsync` cookie-and-session flow** — `sessionId` falls
  back to `Guid.NewGuid().ToString("N")` when `req.SessionId` is null. No
  test asserts that the cookie/session pair flows into `historyStore` and
  is recoverable on the next request.
- **Hook short-circuit (`hookResult.Cancel`)** — when a hook returns
  `Cancel=true`, response is `("hook", 1f, "hook-blocked")` with the
  blocked-response text. No test exists.
- **Routing-context enrichment** — `routingContextEnricher.EnrichIfFollowUp`
  is called for both `AnswerAsync` and `AnswerStreamingAsync`; the
  2026-05-14 code review caught a missing call site. No test pins both
  call sites.
- **No end-to-end "stream + complete chunks" assertion** — `onToken`
  callbacks for a multi-word answer must produce the same concatenation as
  the returned `NaturalLanguageAnswer`.

## Test cases (8 proposed)

| # | Name | Type | What it covers | Fixtures / mocks | Overlaps |
|---|---|---|---|---|---|
| 1 | `HasTabContent_BareDigitProse_FalsePositiveKnownLimitation` | unit | Pin the documented limitation: `"0146"`, `"12-bar blues"`, `"2026-05-24"` all cause `HasTabContent` to return true (matches `TabTokenizerTests`). When fixed, this test flips to assert false. | direct call; real `TabTokenizer`. | overlaps with tokenizer pin. |
| 2 | `EmitRoutingTrace_MatchedSchema_HasTopNAttributes` | unit | When `EmitRoutingTrace(match with 3 candidates)` is called, the captured trace step has `routing.outcome=matched`, `routing.top1.id`, `routing.top2.id`, `routing.top3.id`, with `.base`, `.boost`, `.final`. | replace `IAgenticTraceCapture` with capture-list fake. | none. |
| 3 | `EmitRoutingTrace_BelowThreshold_EmitsFallthroughStep` | unit | `match == null` still emits a trace step with `routing.outcome == "below_threshold_or_unavailable"` and a `routing.note`. | same. | none. |
| 4 | `ExplicitVoicingKeywords_Membership_Stable` | unit | The static keyword list contains the 14 documented terms; pin against silent edits. | reflection or `InternalsVisibleTo` access. | none. |
| 5 | `AnswerStreamingAsync_NullSessionId_GeneratesGuid_AndStoresHistory` | integration | Calling with `req.SessionId == null` causes `historyStore.AddTurn` to be invoked with a fresh GUID-shaped string. | `WebApplicationFactory<Program>` with `IHarmonicChatOrchestrator` stub replaced by real, LLM gateway stubbed. | none. |
| 6 | `HookCancel_ReturnsBlockedResponse_WithHookRouting` | unit | A stub `IChatHook` returning `OnRequestReceived = HookResult.Block("nope")` causes the response to be `("hook", 1.0f, "hook-blocked")` with text `"nope"`. | minimal orchestrator constructed with one hook + null-pattern services. | none. |
| 7 | `RoutingContextEnricher_CalledOnBothPaths` | static-regex | Mirror the existing `ProductionOrchestratorHookPlumbingTests` pattern: scan `ProductionOrchestrator.cs` for `routingContextEnricher.EnrichIfFollowUp(` and assert exactly 2 call sites (one per public Answer*Async). | source-text scan. | extends existing static-pin pattern. |
| 8 | `AnswerStreamingAsync_OnTokenCallbacks_ReassembleToAnswer` | integration | All `onToken` invocations concatenated equal the returned `ChatResponse.NaturalLanguageAnswer` (modulo whitespace) for a stub LLM that emits a known multi-word answer. | `WebApplicationFactory<Program>` with stub LLM. | none. |

## Suggested file locations

- `Tests/Common/GA.Business.ML.Tests/Unit/ProductionOrchestratorBoundaryTests.cs`
  (cases #1, #2, #3, #4, #6 — boundary helpers, no DI graph needed).
- Extend `Tests/Common/GA.Business.ML.Tests/Unit/ProductionOrchestratorHookPlumbingTests.cs`
  with case #7 (static-regex scan, matches existing pattern).
- `Tests/Apps/GaApi.Tests/Orchestration/ProductionOrchestratorE2ETests.cs`
  (cases #5, #8 — `WebApplicationFactory` + stub LLM gateway).

## Effort estimate

**L** (large). The boundary tests (#1–#4, #6) are cheap once we add
`InternalsVisibleTo`. The two integration tests (#5, #8) require a real
`WebApplicationFactory` + LLM-gateway stub — there is no current pattern
for it in `Tests/Apps/GaApi.Tests/`, so the harness is the cost (≈150 LOC
helper, reusable for future orchestrator work). Estimate 3–5 dev-days.

## Rubric

This file is **deliberately partially-testable** by design (the static-pin
approach was the 2026-05-07 conscious decision). The plan respects that:
boundary helpers + one true E2E test, not a synthetic-DI integration
re-build. Case #7 (routing-enricher call-site count) is the highest-value
quick win — single regex, catches the exact 2026-05-14 review-caught bug.
