---
date: 2026-05-07
status: living
audience: engineers working on chatbot orchestration, F# DSL bridge, SignalR hub, Apps/GaChatbot.Api
supersedes_partial: docs/plans/2026-05-03-chatbot-skill-md-migration-completion.md
related:
  - docs/architecture/chat-surfaces.md
  - docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
  - docs/plans/2026-05-03-chatbot-ix-and-ga-dsl-scoping.md
one_way_doors: [public chat wire contract, canonical-host choice]
revisit_trigger:
  - any P0 item shipped → re-rank
  - public-demo SLA changes
  - new host introduced
---

# GA chatbot roadmap

Synthesised 2026-05-07 from the multi-LLM review (codex CLI second-opinion) + live smoke set against `https://demos.guitaralchemist.com/chatbot/` + this session's eight commits.

## TL;DR

Direction is right. What's missing is enforcement (tests + observability that prove what we think is true is true) and one decision (canonical host). The fixes are small. The order is fixed by dependencies.

## Foundations (sound, leave alone)

- **Five-layer dependency model** — Core / Domain / Analysis / AI-ML / Orchestration. Stable.
- **F# closure registry → `ga_dsl_eval` bridge** — primitive is correct; verified end-to-end as of session end.
- **`SemanticIntentRouter` over keyword regex** — correctly routes 5 of 6 smoke prompts. Misroute is a tuning concern, not architectural.
- **SKILL.md-driven Path B (`SkillMdDrivenSkill` + `SkillMdDrivenWrapperBase`)** — works; today's break was registry-side, not skill-side.
- **Algebra path via `IIxAlgebraService`** — fully grounded end-to-end through deployed UI as of session end.

## Now / P0 — close the gaps the smoke set exposed

### 1. ✅ Prove Path B actually invokes `ga_dsl_eval` — *closed 2026-05-07 (`d41ee4df`)*

After the 2026-05-07 registry-bootstrap fix, transpose / common-tones / diatonic-chords returned correct answers but `grounding=none`. The 2026-05-07 follow-up wired tool-invocation tracking into `SkillMdDrivenSkill` (`response.Messages` walk for `FunctionCallContent`), made `SkillMdDrivenWrapperBase` emit a `grounding.source: ga.dsl@<closureName>` sentinel in evidence and clamp confidence ≤0.5 with a warning when `ga_dsl_eval` was *not* invoked, and taught `OrchestratorSkillIntent` to lift the sentinel into `IntentGroundingEvidence` so the closure name surfaces on the chat wire as `grounding.queryType`. Confirmed live:

```
algebra-z-relation   → grounding=ix-compatible@z-relation
transpose            → grounding=ga.dsl@domain.transposeChord
common-tones         → grounding=ga.dsl@domain.commonTones
diatonic-chords      → grounding=ga.dsl@domain.diatonicChords
```

GaApi log line `SkillMdDrivenSkill [transpose] response length=63, tool calls=ga_dsl_eval` confirms the actual function call.

### 2. ✅ Pick canonical surface — *first cut closed 2026-05-07 (`947941c1`)*

Codex CLI second-opinion picked **C-prime**: extract `IChatApplicationService` into a host-neutral library, consume from GaApi first, freeze `GaChatbot.Api` until a concrete deploy reason emerges.

Shipped as the smallest viable cut:

- New `Common/GA.Business.Core.Orchestration/Abstractions/IChatApplicationService.cs` (narrowest possible — `Task<ChatResponse> ChatAsync(ChatRequest, CancellationToken)`).
- New `Common/GA.Business.Core.Orchestration/Services/HarmonicChatApplicationService.cs` — pass-through to `IHarmonicChatOrchestrator`.
- Registered in `AddChatbotOrchestration`.
- `GaApi.ChatbotController` and `GaApi.ChatbotHub` both depend on the new interface instead of `ProductionOrchestrator` directly. The deployed `/chatbot/` SignalR path goes through this surface.
- `GaChatbot.Api` keeps its richer `IChatApplicationService` (Trace, readiness, ChatExecutionResult) — codex's call is to keep it frozen rather than promote a non-ingressed host. Disambiguated via fully qualified type in `GaChatbot.Api/Extensions/ServiceCollectionExtensions.cs`.

P1 #7 (one canonical wire contract) is now unblocked: a future readiness probing / trace-assembly decorator wraps `HarmonicChatApplicationService` once and every GaApi surface inherits it.

### 3. ✅ Real integration test for `SkillMdDrivenSkill` — *closed 2026-05-07 (`e44f998d`)*

`Tests/Common/GA.Business.Core.Tests/AI/ChatbotOrchestrationBootstrapTests.cs` adds three NUnit tests that wire `AddChatbotOrchestration` against a real `IServiceCollection` and assert the canary closures resolve via `GaClosureRegistry.Global.TryGet` and `DslEvalMcpTools.ListClosures()`. Idempotency test covers multi-host wiring. Codex's "Done when" criterion verified: temporarily killing `GaClosureBootstrap.init()` makes all 3 tests fail; restoring returns them to passing.

## Soon / P1

### 4. ✅ Refactor 4 primitive `Grounding*` fields → `IntentGroundingEvidence` record — *closed 2026-05-07 (`367c85bf`)*

`IntentResult` now carries a single `IntentGroundingEvidence?` (record with `Source`, `Revision`, `QueryType`, `Facts`) instead of four optional primitives. `AlgebraIntent` and `OrchestratorSkillIntent` construct the record directly; `ProductionOrchestrator.BuildGrounding` is a one-line null-or-map. The next intent that needs grounding takes one parameter instead of four. Wire shape unchanged end-to-end.

### 5. ✅ Stop silent graceful degradation for deterministic skills — *closed 2026-05-07 (`c8a5750b`)*

P0 #1 already added the loud-failure half (confidence clamp + warning + evidence tag when `ga_dsl_eval` was not invoked). This closes the rest:

- New `ChatbotActivitySource.FailureReasons` closed taxonomy with 10 string-enum values mirroring `DslEvalResult.Error` wire codes (`closure-not-found`, `closure-not-exposed`, `missing-required-arg`, `arg-coerce-failed`, `closure-runtime-error`, `closure-timeout`, `closure-exception`, `skill-md-exception`, `empty-model-response`, `ga-dsl-eval-not-invoked`).
- New tags: `tool.name`, `tool.failure_reason`, `skill.name`, `closure.name`, `exception.type` (no `ex.Message` — codex's PII guidance).
- Tagged in `SkillMdDrivenSkill` empty-response + catch paths and `SkillMdDrivenWrapperBase` ga-dsl-eval-skipped + catch paths.
- Closure-side codes are enumerated but not tagged here — `DslEvalMcpTools` intentionally returns them as `DslEvalResult.Error` inside successful tool responses so the LLM can react. Future work: surface them at the orchestrator boundary if a Path B response carries the LLM's caught error payload.

### 6. ✅ Improve embedding router quality (#81) — *first cut closed 2026-05-07 (`a9220957`)*

The misroute (`Show me Drop 2 voicings of Cmaj7` → `skill.modes` at 0.71) was structural, not embeddings-quality. Codex's diagnosis: `ProductionOrchestrator.AnswerAsync` ran `SemanticIntentRouter` BEFORE `TrySelectDeterministicAgent`, so explicit voicing prompts could be stolen by close-call intent matches.

Reordered: `TrySelectDeterministicAgent` (the voicing guard with chord-literal + voicing-keyword regex) now runs first; `SemanticIntentRouter` falls through. Extracted the deterministic-agent dispatch body into `DispatchDeterministicAgentAsync` so both pre- and post-intent positions share one branch-tag / hook lifecycle / `ChatResponse` shape.

Verified live: `Show me Drop 2 voicings of Cmaj7` → `agent=voicing route=deterministic-voicing conf=0.92`. Modes / transpose / common-tones / algebra all unchanged.

Follow-up: formalize a `VoicingIntent` so voicing participates in the embedding router as a first-class `IIntent` — that lets the LLM-arbitration close-call path (issue #81 original framing) cover voicing alongside everything else. Still tracked.

### 7. ✅ One canonical chat-wire contract — *closed 2026-05-08 (`d40503bd` → `6a0f6679` → `d63b3843` → `a0c7d5f3`)*

GaApi now reaches wire-equivalent parity with GaChatbot.Api via the decorator stack `Harmonic → Traceable → Fallback → ReadinessGated` over the host-neutral `IChatApplicationService`. Three commits + one QA round, codex CLI 2026-05-08 design + QA review pairing throughout.

What's on the wire:
- `Grounding` (existing) — full record on JSON / SSE / SignalR.
- `Trace` (new) — `AgenticTrace` with ordered steps (`readiness.check` → `chat.request` → `orchestration.answer` → `fallback.skipped|fired`); per-request scope isolation verified concurrent and sequential.
- `tool.failure_reason` (existing) — closed taxonomy in `ChatbotActivitySource.FailureReasons`; commit 3 added `OrchestratorLowConfidenceFallback`.

Behaviour additions, all gated:
- **Readiness probing** — `IChatReadinessProbe` with `PermissiveChatReadinessProbe` default. Hosts override to gain real gating; permissive default just adds the trace step. Probe failures fail CLOSED via try/catch — a wedged probe can't take chat down.
- **Fallback** — config-gated `Chatbot:Fallback:Enabled` (default false). When enabled, fires only when ALL three gates pass: master switch, confidence below threshold, AND no deterministic-skill routing-method match. Final confidence clamped to 0; original routing metadata preserved in trace; `tool.failure_reason = orchestrator-low-confidence-fallback` tagged. **Codex P0 fix**: deterministic-skill check uses explicit `routing.method` (which survives the orchestrator's Activity scope) instead of the unreliable `Activity.Current.TagObjects` ambient readback.

Tests:
- `Tests/Common/GA.Business.Core.Tests/AI/FallbackChatApplicationServiceTests.cs` — 5 NUnit tests pin the safety guarantees codex called for: confidence-0 clamp, routing.method=fallback, deterministic-skill protection (the regression test for the codex P0).
- `Tests/Apps/GaChatbot.Api.Tests/Services/OrchestratedChatApplicationServiceTests.cs.AssertFallback` updated in lockstep — old test pinned the buggy `0.35f` confidence which violated P1 #5; now pins 0f.

GaChatbot.Api `0.35f` violation (codex Q4) fixed in lockstep at `Apps/GaChatbot.Api/Services/OrchestratedChatApplicationService.cs:210`.

Type-unification (codex risk-list item 1) closed in commit 1: `AgenticTrace` and `AgenticTraceStep` records moved out of `Apps/GaChatbot.Api/Services/IChatApplicationService.cs` into `Common/GA.Business.Core.Orchestration/Trace/`; both hosts share one type now.

Streaming (`AnswerStreamingAsync`) still calls `IHarmonicChatOrchestrator` directly because `IChatApplicationService` doesn't yet expose a streaming surface — that's the P2 #9 streaming-truth follow-up.

## Later / P2

### 8. Session-scoped `MemoryStore` (#82)

Already tracked. Re-enables retrieval safely.

### 9. Streaming truth in `AnswerStreamingAsync`

`ProductionOrchestrator.AnswerStreamingAsync` only true-streams when the selected agent is a `GuitarAlchemistAgentBase`. Tab / path / skill paths simulate streaming by word-splitting after the answer arrives. Frontends parse this as token streaming. Either fix it or own it explicitly in the AG-UI contract.

### 10. ✅ Cut or revive `GA.AI.Service` — *frozen 2026-05-07 (`<this commit>`)*

Codex's "don't leave a third option dangling" called for either delete or wire-for-a-real-consumer. The full project is bigger than just `ChatController` (8+ controllers covering AI, search, benchmarks, notebooks, tab analysis), so wholesale delete carries reference-value cost. Decision: **firm freeze** with a `DEPRECATED.md` that lists every controller, points at the canonical replacement, and pins hard rules ("do not add new code", "do not re-enable AppHost registration", "do not add ProjectReferences"). `chat-surfaces.md` updated to reflect the freeze. Two named decision triggers tell the next reviewer when to revisit: (a) concrete deploy reason for an AI-workload split, (b) next architecture review concludes reference value has decayed below maintenance cost.

## What this session shipped (8 commits, 2026-05-07)

| Commit | Scope |
|---|---|
| `f4792578` | test pages — chord picker + audio strum + sunburst breadcrumb + complete Forte catalog |
| `45f5f867` | `/chatbot/` SPA-shell trailing-slash rewrite middleware |
| `5d181bd5` | slot-build fix + `docs/solutions/architecture/2026-05-07-slot-build-stale-static-web-assets-manifest.md` |
| `72fa0bc6` | `IntentResult` grounding propagation through `ProductionOrchestrator` (algebra tests 88 → 90) |
| `d36704eb` | `chat-surfaces.md §0/§5b` + GaApi `ChatJsonResponse.Grounding` |
| `fd0cbe26` | `ChatbotHub` emits `grounding` in routing payload (deployed-demo wire parity) |
| `802498e3` | `GaClosureBootstrap.init()` at startup + `ClosureRegistryStartupCheck` canary |
| `d41ee4df` | P0 #1 — Path B `ga_dsl_eval` tool-invocation tracking + grounding sentinel + confidence clamp |
| `e44f998d` | P0 #3 — `ChatbotOrchestrationBootstrapTests` regression fixture (3 tests, codex's "done when" verified) |
| `367c85bf` | P1 #4 — `IntentGroundingEvidence` record collapses the four primitive grounding fields |
| `a9220957` | P1 #6 first cut — deterministic voicing guard precedes `SemanticIntentRouter` |
| `c8a5750b` | P1 #5 — `tool.failure_reason` trace tags + closed `FailureReasons` taxonomy |
| `947941c1` | P0 #2 first cut — host-neutral `IChatApplicationService` consumed by GaApi controller + hub |
| `9573a4a8` | P1 #6 follow-up — `VoicingIntent` as first-class `IIntent` |
| `0de47ee1` | P1 #6 QA fix — VoicingIntent example-prompt overlap with BeginnerChordsSkill |
| `e8561984` | P2 #10 — freeze `GA.AI.Service` with `DEPRECATED.md` |
| `44ceb101` | P1 #6 / P2 #10 QA round — DEPRECATED.md accuracy + `AddGuitarAlchemistAgents` lifetime alignment |
| `d40503bd` | P1 #7 commit 1 — `AgenticTrace` types + `IAgenticTraceCapture` + `TraceableChatApplicationService` decorator |
| `6a0f6679` | P1 #7 commit 2 — `IChatReadinessProbe` + `PermissiveChatReadinessProbe` + `ReadinessGatedChatApplicationService` |
| `d63b3843` | P1 #7 commit 3 — `IFallbackChatHandler` + `FallbackChatApplicationService` (default off, hard safety guarantees) |
| `a0c7d5f3` | P1 #7 QA round — explicit deterministic-skill check (codex P0), AG-UI uses IChatApplicationService, options validation, trace error tagging, GaChatbot.Api 0.35f→0 |

## Decision log

- **2026-05-07** — chose to keep `IntentResult` in `GA.Business.ML` (layer 4) and reconstruct `GroundingMetadata` at the orchestrator boundary rather than move `IntentResult` upward. Two-way door — revisit if more intents need typed grounding.
- **2026-05-07** — canonical surface decision: **C-prime** (codex pick, executed `947941c1`). Host-neutral `IChatApplicationService` lives in `Common/GA.Business.Core.Orchestration`; GaApi delegates; `GaChatbot.Api` frozen. Re-evaluate if a concrete deploy reason emerges for the second host. One-way door — moving away from the host-neutral surface is expensive once decorators stack on it.
- **2026-05-07** — closure-registry bootstrap added to `AddChatbotOrchestration` rather than `DslEvalMcpTools` static constructor. Two-way door — moveable if multi-host wiring demands it.
- **2026-05-07** — `AddGuitarAlchemistAgents` lifetimes aligned with the canonical uppercase helper: agents Transient, base-type registrations one-per-agent via factories, `SemanticRouter` Scoped. Two-way door but unlikely revert: matches the better helper, removes the captured-scope footgun. Codex CLI QA round.
- **2026-05-07** — `GA.AI.Service` frozen rather than deleted. Two-way door: revisit on the named decision triggers in the project's `DEPRECATED.md`.

## Out of scope here

- LLM model selection (`Anthropic:Claude` vs `Ollama` vs `Docker`) — covered by `llm-providers.md`.
- OPTIC-K voicing index correctness — owned by `docs/plans/2026-05-02-optick-sae-plan.md`.
- IXQL / Demerzel governance integration — owned by sibling repos and their own roadmaps.
